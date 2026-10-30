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
[Route("api/announcements")]
public sealed class AnnouncementsController(ApplicationDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AnnouncementResponse>>> List(CancellationToken cancellationToken)
    {
        var query = db.Announcements.AsNoTracking();
        if (!User.IsInRole(ApplicationRoles.Pastor))
        {
            var audiences = new List<AnnouncementAudience> { AnnouncementAudience.Everyone };
            if (User.IsInRole(ApplicationRoles.Member)) audiences.Add(AnnouncementAudience.Members);
            if (User.IsInRole(ApplicationRoles.Leader)) audiences.Add(AnnouncementAudience.Leaders);
            query = query.Where(item => item.Status == AnnouncementStatus.Published && audiences.Contains(item.Audience));
        }
        return Ok(await query.OrderByDescending(item => item.Priority).ThenByDescending(item => item.PublishedUtc ?? item.CreatedUtc)
            .Select(item => ToResponse(item)).ToListAsync(cancellationToken));
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPost]
    public async Task<ActionResult<AnnouncementResponse>> Create(AnnouncementMutation request, CancellationToken cancellationToken)
    {
        if (!TryValidate(request, out var errors)) return BadRequest(new ValidationProblemDetails(errors));
        var now = DateTimeOffset.UtcNow;
        var actor = users.GetUserId(User)!;
        var item = new Announcement { Title = request.Title.Trim(), Body = request.Body.Trim(), Priority = request.Priority,
            Audience = request.Audience, Status = AnnouncementStatus.Draft, CreatedUtc = now, UpdatedUtc = now, CreatedByUserId = actor };
        AddAudit(item, "AnnouncementCreated", actor, now);
        db.Announcements.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(List), new { id = item.Id }, ToResponse(item));
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, AnnouncementMutation request, CancellationToken cancellationToken)
    {
        var item = await db.Announcements.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        if (item is null) return NotFound(new { message = "The announcement was not found." });
        if (item.Status == AnnouncementStatus.Archived) return Conflict(new { message = "An archived announcement cannot be edited." });
        if (!TryValidate(request, out var errors)) return BadRequest(new ValidationProblemDetails(errors));
        item.Title = request.Title.Trim(); item.Body = request.Body.Trim(); item.Priority = request.Priority; item.Audience = request.Audience;
        item.UpdatedUtc = DateTimeOffset.UtcNow;
        AddAudit(item, "AnnouncementUpdated", users.GetUserId(User)!, item.UpdatedUtc);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id, CancellationToken cancellationToken) => await ChangeState(id, AnnouncementStatus.Draft, AnnouncementStatus.Published, "AnnouncementPublished", cancellationToken);

    [Authorize(Policy = "PastorOnly")]
    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id, CancellationToken cancellationToken) => await ChangeState(id, null, AnnouncementStatus.Archived, "AnnouncementArchived", cancellationToken);

    [Authorize(Policy = "PastorOnly")]
    [HttpGet("{id:int}/audit")]
    public async Task<ActionResult<IReadOnlyList<AnnouncementAuditResponse>>> Audit(int id, CancellationToken cancellationToken)
    {
        if (!await db.Announcements.AnyAsync(item => item.Id == id, cancellationToken)) return NotFound();
        return Ok(await db.AnnouncementAuditEntries.AsNoTracking().Where(item => item.AnnouncementId == id)
            .OrderByDescending(item => item.OccurredUtc).Select(item => new AnnouncementAuditResponse(item.Id, item.ActionType,
                item.OccurredUtc, item.ActorUser == null ? "Unknown account" : item.ActorUser.Email ?? "Unknown account"))
            .ToListAsync(cancellationToken));
    }

    private async Task<IActionResult> ChangeState(int id, AnnouncementStatus? expected, AnnouncementStatus target, string action, CancellationToken cancellationToken)
    {
        var item = await db.Announcements.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        if (item is null) return NotFound(new { message = "The announcement was not found." });
        if (expected is not null && item.Status != expected) return Conflict(new { message = "Only a draft announcement can be published." });
        if (target == AnnouncementStatus.Archived && item.Status == AnnouncementStatus.Archived) return Conflict(new { message = "The announcement is already archived." });
        var now = DateTimeOffset.UtcNow;
        item.Status = target; item.UpdatedUtc = now;
        if (target == AnnouncementStatus.Published) item.PublishedUtc = now;
        if (target == AnnouncementStatus.Archived) item.ArchivedUtc = now;
        AddAudit(item, action, users.GetUserId(User)!, now);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static bool TryValidate(AnnouncementMutation request, out Dictionary<string, string[]> errors)
    {
        errors = [];
        if (string.IsNullOrWhiteSpace(request.Title)) errors[nameof(request.Title)] = ["An announcement title is required."];
        if (string.IsNullOrWhiteSpace(request.Body)) errors[nameof(request.Body)] = ["Announcement text is required."];
        if (!Enum.IsDefined(request.Priority)) errors[nameof(request.Priority)] = ["Choose a valid priority."];
        if (!Enum.IsDefined(request.Audience)) errors[nameof(request.Audience)] = ["Choose a valid audience."];
        return errors.Count == 0;
    }
    private static void AddAudit(Announcement item, string action, string actor, DateTimeOffset occurred) =>
        item.AuditEntries.Add(new AnnouncementAuditEntry { ActionType = action, ActorUserId = actor, OccurredUtc = occurred, CorrelationId = Guid.NewGuid() });
    private static AnnouncementResponse ToResponse(Announcement item) => new(item.Id, item.Title, item.Body, item.Priority, item.Audience, item.Status, item.PublishedUtc, item.ArchivedUtc, item.CreatedUtc, item.UpdatedUtc);
}

public sealed class AnnouncementMutation
{
    [Required, StringLength(200)] public string Title { get; init; } = string.Empty;
    [Required, StringLength(4000)] public string Body { get; init; } = string.Empty;
    public AnnouncementPriority Priority { get; init; }
    public AnnouncementAudience Audience { get; init; }
}
public sealed record AnnouncementResponse(int Id, string Title, string Body, AnnouncementPriority Priority, AnnouncementAudience Audience,
    AnnouncementStatus Status, DateTimeOffset? PublishedUtc, DateTimeOffset? ArchivedUtc, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc);
public sealed record AnnouncementAuditResponse(long Id, string ActionType, DateTimeOffset OccurredUtc, string ActorEmail);
