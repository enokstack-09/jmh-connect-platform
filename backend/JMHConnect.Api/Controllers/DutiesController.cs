using System.ComponentModel.DataAnnotations;
using JMHConnect.Api.Data;
using JMHConnect.Api.Models;
using JMHConnect.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JMHConnect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/duties")]
public sealed class DutiesController(ApplicationDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DutyResponse>>> List(CancellationToken cancellationToken)
    {
        var query = db.DutyAssignments.AsNoTracking();
        if (!User.IsInRole(ApplicationRoles.Pastor))
        {
            if (!User.IsInRole(ApplicationRoles.Member)) return Forbid();
            var userId = users.GetUserId(User);
            var link = await db.Users.AsNoTracking().Where(user => user.Id == userId)
                .Select(user => new { user.MemberProfileId, Status = user.MemberProfile == null ? (ResidencyStatus?)null : user.MemberProfile.ResidencyStatus })
                .SingleOrDefaultAsync(cancellationToken);
            if (link?.MemberProfileId is null) return NotFound(new { message = "No member profile is linked to this account." });
            if (link.Status != ResidencyStatus.Active) return Forbid();
            query = query.Where(duty => duty.AssignedMemberProfileId == link.MemberProfileId.Value);
        }

        return Ok(await query.OrderBy(duty => duty.Status).ThenBy(duty => duty.ScheduledForUtc)
            .Select(duty => new DutyResponse(duty.Id, duty.Title, duty.Description, duty.AssignedMemberProfileId,
                duty.AssignedMemberProfile!.PreferredName ?? ((duty.AssignedMemberProfile.GivenName ?? "") + " " + (duty.AssignedMemberProfile.FamilyName ?? "")).Trim(),
                duty.ScheduledForUtc, duty.DueUtc, duty.Status, duty.CompletedUtc))
            .ToListAsync(cancellationToken));
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpGet("assignees")]
    public async Task<ActionResult<IReadOnlyList<DutyAssigneeResponse>>> Assignees(CancellationToken cancellationToken) =>
        Ok(await db.MemberProfiles.AsNoTracking().Where(profile => profile.ResidencyStatus == ResidencyStatus.Active)
            .OrderBy(profile => profile.FamilyName).ThenBy(profile => profile.GivenName)
            .Select(profile => new DutyAssigneeResponse(profile.Id,
                profile.PreferredName ?? ((profile.GivenName ?? "") + " " + (profile.FamilyName ?? "")).Trim()))
            .ToListAsync(cancellationToken));

    [Authorize(Policy = "PastorOnly")]
    [HttpPost]
    public async Task<ActionResult<DutyResponse>> Create(DutyMutation request, CancellationToken cancellationToken)
    {
        if (!TryValidate(request, out var errors)) return BadRequest(new ValidationProblemDetails(errors));
        var member = await ActiveMemberAsync(request.AssignedMemberProfileId, cancellationToken);
        if (member is null) return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(request.AssignedMemberProfileId)] = ["Duties may be assigned only to active residents."] }));
        var now = DateTimeOffset.UtcNow;
        var actor = users.GetUserId(User)!;
        var duty = new DutyAssignment
        {
            Title = request.Title.Trim(), Description = Trim(request.Description), AssignedMemberProfileId = member.Id,
            ScheduledForUtc = request.ScheduledForUtc.ToUniversalTime(), DueUtc = request.DueUtc?.ToUniversalTime(),
            Status = DutyAssignmentStatus.Assigned, CreatedUtc = now, UpdatedUtc = now, CreatedByUserId = actor
        };
        AddAudit(duty, "DutyCreated", actor, now);
        db.DutyAssignments.Add(duty);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(List), new { id = duty.Id }, ToResponse(duty, DisplayName(member)));
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, DutyMutation request, CancellationToken cancellationToken)
    {
        var duty = await db.DutyAssignments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (duty is null) return NotFound(new { message = "The duty assignment was not found." });
        if (duty.Status != DutyAssignmentStatus.Assigned) return Conflict(new { message = "Only assigned duties can be edited." });
        if (!TryValidate(request, out var errors)) return BadRequest(new ValidationProblemDetails(errors));
        if (await ActiveMemberAsync(request.AssignedMemberProfileId, cancellationToken) is null)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(request.AssignedMemberProfileId)] = ["Duties may be assigned only to active residents."] }));
        duty.Title = request.Title.Trim();
        duty.Description = Trim(request.Description);
        duty.AssignedMemberProfileId = request.AssignedMemberProfileId;
        duty.ScheduledForUtc = request.ScheduledForUtc.ToUniversalTime();
        duty.DueUtc = request.DueUtc?.ToUniversalTime();
        duty.UpdatedUtc = DateTimeOffset.UtcNow;
        AddAudit(duty, "DutyUpdated", users.GetUserId(User)!, duty.UpdatedUtc);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var duty = await db.DutyAssignments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (duty is null) return NotFound(new { message = "The duty assignment was not found." });
        if (duty.Status != DutyAssignmentStatus.Assigned) return Conflict(new { message = "Only assigned duties can be cancelled." });
        duty.Status = DutyAssignmentStatus.Cancelled;
        duty.UpdatedUtc = DateTimeOffset.UtcNow;
        AddAudit(duty, "DutyCancelled", users.GetUserId(User)!, duty.UpdatedUtc);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, CancellationToken cancellationToken)
    {
        if (!User.IsInRole(ApplicationRoles.Member)) return Forbid();
        var userId = users.GetUserId(User)!;
        var user = await db.Users.AsNoTracking().Where(item => item.Id == userId)
            .Select(item => new { item.MemberProfileId, Status = item.MemberProfile == null ? (ResidencyStatus?)null : item.MemberProfile.ResidencyStatus })
            .SingleOrDefaultAsync(cancellationToken);
        if (user?.MemberProfileId is null) return NotFound(new { message = "No member profile is linked to this account." });
        if (user.Status != ResidencyStatus.Active) return Forbid();
        var duty = await db.DutyAssignments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (duty is null) return NotFound(new { message = "The duty assignment was not found." });
        if (duty.AssignedMemberProfileId != user.MemberProfileId.Value) return Forbid();
        if (duty.Status != DutyAssignmentStatus.Assigned) return Conflict(new { message = "Only an assigned duty can be marked complete." });
        var now = DateTimeOffset.UtcNow;
        duty.Status = DutyAssignmentStatus.Completed;
        duty.CompletedUtc = now;
        duty.CompletedByUserId = userId;
        duty.UpdatedUtc = now;
        AddAudit(duty, "DutyCompleted", userId, now);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpGet("{id:int}/audit")]
    public async Task<ActionResult<IReadOnlyList<DutyAuditResponse>>> Audit(int id, CancellationToken cancellationToken)
    {
        if (!await db.DutyAssignments.AnyAsync(item => item.Id == id, cancellationToken)) return NotFound();
        return Ok(await db.DutyAssignmentAuditEntries.AsNoTracking().Where(entry => entry.DutyAssignmentId == id)
            .OrderByDescending(entry => entry.OccurredUtc)
            .Select(entry => new DutyAuditResponse(entry.Id, entry.ActionType, entry.OccurredUtc,
                entry.ActorUser == null ? "Unknown account" : entry.ActorUser.Email ?? "Unknown account"))
            .ToListAsync(cancellationToken));
    }

    private Task<MemberProfile?> ActiveMemberAsync(int id, CancellationToken cancellationToken) => db.MemberProfiles
        .SingleOrDefaultAsync(profile => profile.Id == id && profile.ResidencyStatus == ResidencyStatus.Active, cancellationToken);
    private static bool TryValidate(DutyMutation request, out Dictionary<string, string[]> errors)
    {
        errors = [];
        if (string.IsNullOrWhiteSpace(request.Title)) errors[nameof(request.Title)] = ["A duty title is required."];
        if (request.AssignedMemberProfileId <= 0) errors[nameof(request.AssignedMemberProfileId)] = ["Choose an active resident."];
        if (request.ScheduledForUtc == default) errors[nameof(request.ScheduledForUtc)] = ["A scheduled date and time is required."];
        if (request.DueUtc is not null && request.DueUtc < request.ScheduledForUtc) errors[nameof(request.DueUtc)] = ["The due time cannot be before the scheduled time."];
        return errors.Count == 0;
    }
    private static void AddAudit(DutyAssignment duty, string action, string actor, DateTimeOffset occurred) =>
        duty.AuditEntries.Add(new DutyAssignmentAuditEntry { ActionType = action, ActorUserId = actor, OccurredUtc = occurred, CorrelationId = Guid.NewGuid() });
    private static DutyResponse ToResponse(DutyAssignment duty, string name) => new(duty.Id, duty.Title, duty.Description,
        duty.AssignedMemberProfileId, name, duty.ScheduledForUtc, duty.DueUtc, duty.Status, duty.CompletedUtc);
    private static string DisplayName(MemberProfile profile) => profile.PreferredName ?? $"{profile.GivenName} {profile.FamilyName}".Trim();
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class DutyMutation
{
    [Required, StringLength(200)] public string Title { get; init; } = string.Empty;
    [StringLength(1000)] public string? Description { get; init; }
    public int AssignedMemberProfileId { get; init; }
    public DateTimeOffset ScheduledForUtc { get; init; }
    public DateTimeOffset? DueUtc { get; init; }
}
public sealed record DutyResponse(int Id, string Title, string? Description, int AssignedMemberProfileId,
    string AssignedMemberDisplayName, DateTimeOffset ScheduledForUtc, DateTimeOffset? DueUtc,
    DutyAssignmentStatus Status, DateTimeOffset? CompletedUtc);
public sealed record DutyAssigneeResponse(int Id, string DisplayName);
public sealed record DutyAuditResponse(long Id, string ActionType, DateTimeOffset OccurredUtc, string ActorEmail);
