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
[Route("api/birthdays")]
public sealed class BirthdaysController(ApplicationDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    // This deliberately returns month/day only. Full DOB, birth year and calculated age are never family-directory data.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BirthdayResponse>>> List(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var birthdays = await db.MemberProfiles.AsNoTracking()
            .Where(profile => profile.ResidencyStatus == ResidencyStatus.Active && profile.IsBirthdayVisible && profile.DateOfBirth != null)
            .Select(profile => new BirthdaySource(profile.Id,
                profile.PreferredName ?? ((profile.GivenName ?? "") + " " + (profile.FamilyName ?? "")).Trim(),
                profile.DateOfBirth!.Value.Month, profile.DateOfBirth.Value.Day))
            .ToListAsync(cancellationToken);
        return Ok(birthdays.Select(item => new BirthdayResponse(item.MemberProfileId, item.DisplayName, item.Month, item.Day,
                NextOccurrence(item.Month, item.Day, today)))
            .OrderBy(item => item.NextOccurrence).ThenBy(item => item.DisplayName).ToList());
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpGet("manage")]
    public async Task<ActionResult<IReadOnlyList<BirthdayManagementResponse>>> Manage(CancellationToken cancellationToken) =>
        Ok(await db.MemberProfiles.AsNoTracking()
            .Where(profile => profile.ResidencyStatus == ResidencyStatus.Active)
            .OrderBy(profile => profile.FamilyName).ThenBy(profile => profile.GivenName)
            .Select(profile => new BirthdayManagementResponse(profile.Id,
                profile.PreferredName ?? ((profile.GivenName ?? "") + " " + (profile.FamilyName ?? "")).Trim(),
                profile.DateOfBirth != null, profile.IsBirthdayVisible))
            .ToListAsync(cancellationToken));

    [Authorize(Policy = "PastorOnly")]
    [HttpPut("{id:int}/visibility")]
    public async Task<IActionResult> SetVisibility(int id, BirthdayVisibilityRequest request, CancellationToken cancellationToken)
    {
        var profile = await db.MemberProfiles.Include(item => item.AuditEntries)
            .SingleOrDefaultAsync(item => item.Id == id && item.ResidencyStatus == ResidencyStatus.Active, cancellationToken);
        if (profile is null) return NotFound(new { message = "The active resident was not found." });
        if (request.IsBirthdayVisible && profile.DateOfBirth is null)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["isBirthdayVisible"] = ["A birthday cannot be shared until a date of birth is on record."] }));

        var actor = users.GetUserId(User)!;
        var now = DateTimeOffset.UtcNow;
        profile.IsBirthdayVisible = request.IsBirthdayVisible;
        profile.BirthdayVisibilityUpdatedUtc = now;
        profile.BirthdayVisibilityUpdatedByUserId = actor;
        profile.AuditEntries.Add(new MemberProfileAuditEntry
        {
            ActionType = "BirthdayVisibilityUpdated", ActorUserId = actor, OccurredUtc = now,
            DetailsJson = $"{{\"isBirthdayVisible\":{request.IsBirthdayVisible.ToString().ToLowerInvariant()}}}", CorrelationId = Guid.NewGuid()
        });
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static DateOnly NextOccurrence(int month, int day, DateOnly today)
    {
        var dayThisYear = Math.Min(day, DateTime.DaysInMonth(today.Year, month));
        var candidate = new DateOnly(today.Year, month, dayThisYear);
        if (candidate >= today) return candidate;
        var nextYearDay = Math.Min(day, DateTime.DaysInMonth(today.Year + 1, month));
        return new DateOnly(today.Year + 1, month, nextYearDay);
    }

    private sealed record BirthdaySource(int MemberProfileId, string DisplayName, int Month, int Day);
}

public sealed class BirthdayVisibilityRequest { public bool IsBirthdayVisible { get; init; } }
public sealed record BirthdayResponse(int MemberProfileId, string DisplayName, int Month, int Day, DateOnly NextOccurrence);
public sealed record BirthdayManagementResponse(int MemberProfileId, string DisplayName, bool HasBirthdayOnRecord, bool IsBirthdayVisible);
