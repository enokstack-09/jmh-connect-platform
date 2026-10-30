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
[Route("api/devotions")]
public sealed class DevotionsController(ApplicationDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DevotionSessionResponse>>> List(CancellationToken cancellationToken)
    {
        var isPastor = User.IsInRole(ApplicationRoles.Pastor);
        var activeResidents = isPastor
            ? await db.MemberProfiles.CountAsync(profile => profile.ResidencyStatus == ResidencyStatus.Active, cancellationToken)
            : 0;
        var sessions = await db.DevotionSessions.AsNoTracking()
            .OrderByDescending(session => session.ScheduledStartUtc)
            .Select(session => new DevotionSessionResponse(
                session.Id, session.Title, session.ScheduledStartUtc, session.DurationMinutes,
                session.Location, session.Notes, session.Status,
                isPastor ? new AttendanceSummaryResponse(
                    session.Attendance.Count(item => item.MemberProfile!.ResidencyStatus == ResidencyStatus.Active && item.Status == DevotionAttendanceStatus.Present),
                    session.Attendance.Count(item => item.MemberProfile!.ResidencyStatus == ResidencyStatus.Active && item.Status == DevotionAttendanceStatus.Absent),
                    session.Attendance.Count(item => item.MemberProfile!.ResidencyStatus == ResidencyStatus.Active && item.Status == DevotionAttendanceStatus.Excused),
                    activeResidents - session.Attendance.Count(item => item.MemberProfile!.ResidencyStatus == ResidencyStatus.Active)) : null))
            .ToListAsync(cancellationToken);
        return Ok(sessions);
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPost]
    public async Task<ActionResult<DevotionSessionResponse>> Create(DevotionSessionMutation request, CancellationToken cancellationToken)
    {
        if (!TryValidate(request, out var errors)) return BadRequest(new ValidationProblemDetails(errors));
        var now = DateTimeOffset.UtcNow;
        var session = new DevotionSession
        {
            Title = request.Title.Trim(), ScheduledStartUtc = request.ScheduledStartUtc.ToUniversalTime(),
            DurationMinutes = request.DurationMinutes, Location = Trim(request.Location), Notes = Trim(request.Notes),
            Status = DevotionSessionStatus.Scheduled, CreatedUtc = now, UpdatedUtc = now,
            CreatedByUserId = users.GetUserId(User)!
        };
        AddAudit(session, "DevotionCreated", session.CreatedByUserId, now);
        db.DevotionSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(List), new { id = session.Id }, ToResponse(session));
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, DevotionSessionMutation request, CancellationToken cancellationToken)
    {
        var session = await db.DevotionSessions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (session is null) return NotFound(new { message = "The devotion session was not found." });
        if (session.Status == DevotionSessionStatus.Cancelled) return Conflict(new { message = "A cancelled devotion session cannot be edited." });
        if (!TryValidate(request, out var errors)) return BadRequest(new ValidationProblemDetails(errors));
        session.Title = request.Title.Trim();
        session.ScheduledStartUtc = request.ScheduledStartUtc.ToUniversalTime();
        session.DurationMinutes = request.DurationMinutes;
        session.Location = Trim(request.Location);
        session.Notes = Trim(request.Notes);
        session.UpdatedUtc = DateTimeOffset.UtcNow;
        AddAudit(session, "DevotionUpdated", users.GetUserId(User)!, session.UpdatedUtc);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var session = await db.DevotionSessions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (session is null) return NotFound(new { message = "The devotion session was not found." });
        if (session.Status == DevotionSessionStatus.Cancelled) return Conflict(new { message = "The devotion session is already cancelled." });
        session.Status = DevotionSessionStatus.Cancelled;
        session.UpdatedUtc = DateTimeOffset.UtcNow;
        AddAudit(session, "DevotionCancelled", users.GetUserId(User)!, session.UpdatedUtc);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpGet("{id:int}/attendance")]
    public async Task<ActionResult<IReadOnlyList<AttendanceRosterItemResponse>>> Attendance(int id, CancellationToken cancellationToken)
    {
        if (!await db.DevotionSessions.AnyAsync(session => session.Id == id, cancellationToken)) return NotFound();
        var roster = await db.MemberProfiles.AsNoTracking()
            .Where(profile => profile.ResidencyStatus == ResidencyStatus.Active)
            .OrderBy(profile => profile.FamilyName).ThenBy(profile => profile.GivenName)
            .Select(profile => new AttendanceRosterItemResponse(
                profile.Id, profile.PreferredName ?? ((profile.GivenName ?? "") + " " + (profile.FamilyName ?? "")).Trim(),
                db.DevotionAttendance.Where(item => item.DevotionSessionId == id && item.MemberProfileId == profile.Id)
                    .Select(item => (DevotionAttendanceStatus?)item.Status).SingleOrDefault(),
                db.DevotionAttendance.Where(item => item.DevotionSessionId == id && item.MemberProfileId == profile.Id)
                    .Select(item => item.Notes).SingleOrDefault()))
            .ToListAsync(cancellationToken);
        return Ok(roster);
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPut("{id:int}/attendance")]
    public async Task<IActionResult> SaveAttendance(int id, SaveAttendanceRequest request, CancellationToken cancellationToken)
    {
        var session = await db.DevotionSessions.Include(item => item.Attendance)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (session is null) return NotFound(new { message = "The devotion session was not found." });
        if (session.Status == DevotionSessionStatus.Cancelled) return Conflict(new { message = "Attendance cannot be recorded for a cancelled session." });
        if (request.Entries.Select(entry => entry.MemberProfileId).Distinct().Count() != request.Entries.Count)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["entries"] = ["Each resident may appear only once."] }));
        if (request.Entries.Any(entry => entry.Status is not null && !Enum.IsDefined(entry.Status.Value)))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["entries"] = ["Attendance must be Present, Absent or Excused."] }));
        var requestedIds = request.Entries.Select(entry => entry.MemberProfileId).ToArray();
        var activeIds = await db.MemberProfiles.AsNoTracking()
            .Where(profile => requestedIds.Contains(profile.Id) && profile.ResidencyStatus == ResidencyStatus.Active)
            .Select(profile => profile.Id).ToListAsync(cancellationToken);
        if (activeIds.Count != requestedIds.Length)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["entries"] = ["Attendance may be recorded only for active residents."] }));

        var actorId = users.GetUserId(User)!;
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in request.Entries)
        {
            var attendance = session.Attendance.SingleOrDefault(item => item.MemberProfileId == entry.MemberProfileId);
            if (entry.Status is null)
            {
                if (attendance is not null) db.DevotionAttendance.Remove(attendance);
                continue;
            }
            if (attendance is null)
            {
                attendance = new DevotionAttendance { MemberProfileId = entry.MemberProfileId };
                session.Attendance.Add(attendance);
            }
            attendance.Status = entry.Status.Value;
            attendance.Notes = Trim(entry.Notes);
            attendance.RecordedByUserId = actorId;
            attendance.RecordedUtc = now;
        }
        AddAudit(session, "AttendanceRecorded", actorId, now);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("my-attendance")]
    public async Task<ActionResult<IReadOnlyList<MyAttendanceResponse>>> MyAttendance(CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User);
        var profile = await db.Users.AsNoTracking().Where(user => user.Id == userId)
            .Select(user => user.MemberProfile).SingleOrDefaultAsync(cancellationToken);
        if (profile is null) return NotFound(new { message = "No member profile is linked to this account." });
        if (profile.ResidencyStatus != ResidencyStatus.Active) return Forbid();
        var history = await db.DevotionAttendance.AsNoTracking()
            .Where(item => item.MemberProfileId == profile.Id)
            .OrderByDescending(item => item.DevotionSession!.ScheduledStartUtc)
            .Select(item => new MyAttendanceResponse(item.DevotionSessionId, item.DevotionSession!.Title,
                item.DevotionSession.ScheduledStartUtc, item.DevotionSession.Status, item.Status))
            .ToListAsync(cancellationToken);
        return Ok(history);
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpGet("{id:int}/audit")]
    public async Task<ActionResult<IReadOnlyList<DevotionAuditResponse>>> Audit(int id, CancellationToken cancellationToken)
    {
        if (!await db.DevotionSessions.AnyAsync(session => session.Id == id, cancellationToken)) return NotFound();
        return Ok(await db.DevotionSessionAuditEntries.AsNoTracking().Where(entry => entry.DevotionSessionId == id)
            .OrderByDescending(entry => entry.OccurredUtc)
            .Select(entry => new DevotionAuditResponse(entry.Id, entry.ActionType, entry.OccurredUtc,
                entry.ActorUser == null ? "Unknown account" : entry.ActorUser.Email ?? "Unknown account"))
            .ToListAsync(cancellationToken));
    }

    private static bool TryValidate(DevotionSessionMutation request, out Dictionary<string, string[]> errors)
    {
        errors = [];
        if (string.IsNullOrWhiteSpace(request.Title)) errors[nameof(request.Title)] = ["A session title is required."];
        if (request.ScheduledStartUtc == default) errors[nameof(request.ScheduledStartUtc)] = ["A session date and time is required."];
        if (request.DurationMinutes is < 15 or > 240) errors[nameof(request.DurationMinutes)] = ["Duration must be between 15 and 240 minutes."];
        return errors.Count == 0;
    }
    private static void AddAudit(DevotionSession session, string action, string actor, DateTimeOffset occurred) =>
        session.AuditEntries.Add(new DevotionSessionAuditEntry { ActionType = action, ActorUserId = actor, OccurredUtc = occurred, CorrelationId = Guid.NewGuid() });
    private static DevotionSessionResponse ToResponse(DevotionSession session) => new(session.Id, session.Title, session.ScheduledStartUtc,
        session.DurationMinutes, session.Location, session.Notes, session.Status, new AttendanceSummaryResponse(0, 0, 0, 0));
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class DevotionSessionMutation
{
    [Required, StringLength(200)] public string Title { get; init; } = string.Empty;
    public DateTimeOffset ScheduledStartUtc { get; init; }
    [Range(15, 240)] public short DurationMinutes { get; init; }
    [StringLength(200)] public string? Location { get; init; }
    [StringLength(1000)] public string? Notes { get; init; }
}
public sealed class SaveAttendanceRequest { public IReadOnlyList<AttendanceEntryRequest> Entries { get; init; } = []; }
public sealed class AttendanceEntryRequest
{
    public int MemberProfileId { get; init; }
    public DevotionAttendanceStatus? Status { get; init; }
    [StringLength(500)] public string? Notes { get; init; }
}
public sealed record AttendanceSummaryResponse(int Present, int Absent, int Excused, int Unrecorded);
public sealed record DevotionSessionResponse(int Id, string Title, DateTimeOffset ScheduledStartUtc, short DurationMinutes,
    string? Location, string? Notes, DevotionSessionStatus Status, AttendanceSummaryResponse? AttendanceSummary);
public sealed record AttendanceRosterItemResponse(int MemberProfileId, string DisplayName, DevotionAttendanceStatus? Status, string? Notes);
public sealed record MyAttendanceResponse(int SessionId, string Title, DateTimeOffset ScheduledStartUtc,
    DevotionSessionStatus SessionStatus, DevotionAttendanceStatus Status);
public sealed record DevotionAuditResponse(long Id, string ActionType, DateTimeOffset OccurredUtc, string ActorEmail);
