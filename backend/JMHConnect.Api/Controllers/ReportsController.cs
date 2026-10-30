using System.Text;
using JMHConnect.Api.Data;
using JMHConnect.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JMHConnect.Api.Controllers;

[ApiController]
[Authorize(Policy = "PastorOnly")]
[Route("api/reports")]
public sealed class ReportsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ReportSummaryResponse>> Summary([FromQuery] ReportDateFilter filter, CancellationToken cancellationToken)
    {
        if (!TryRange(filter, out var start, out var end, out var problem)) return BadRequest(problem);
        var attendance = db.DevotionAttendance.AsNoTracking().Where(item => item.DevotionSession!.ScheduledStartUtc >= start && item.DevotionSession.ScheduledStartUtc < end);
        return Ok(new ReportSummaryResponse(
            await db.MemberProfiles.CountAsync(item => item.ResidencyStatus == ResidencyStatus.Active, cancellationToken),
            await db.MemberProfiles.CountAsync(item => item.ResidencyStatus == ResidencyStatus.Archived, cancellationToken),
            await db.DevotionSessions.CountAsync(item => item.ScheduledStartUtc >= start && item.ScheduledStartUtc < end, cancellationToken),
            await attendance.CountAsync(item => item.Status == DevotionAttendanceStatus.Present, cancellationToken),
            await db.DutyAssignments.CountAsync(item => item.ScheduledForUtc >= start && item.ScheduledForUtc < end && item.Status == DutyAssignmentStatus.Assigned, cancellationToken),
            await db.DutyAssignments.CountAsync(item => item.ScheduledForUtc >= start && item.ScheduledForUtc < end && item.Status == DutyAssignmentStatus.Completed, cancellationToken),
            await db.CommunityEvents.CountAsync(item => item.StartsUtc >= start && item.StartsUtc < end && item.Status == CommunityEventStatus.Scheduled, cancellationToken), start, end));
    }

    [HttpGet("{kind}/csv")]
    public async Task<IActionResult> Csv(string kind, [FromQuery] ReportDateFilter filter, CancellationToken cancellationToken)
    {
        if (!TryRange(filter, out var start, out var end, out var problem)) return BadRequest(problem);
        var rows = kind.ToLowerInvariant() switch
        {
            "residents" => await ResidentRows(cancellationToken),
            "attendance" => await AttendanceRows(start, end, cancellationToken),
            "duties" => await DutyRows(start, end, cancellationToken),
            "events" => await EventRows(start, end, cancellationToken),
            _ => null
        };
        if (rows is null) return NotFound(new { message = "Choose residents, attendance, duties or events." });
        return File(Encoding.UTF8.GetBytes(ToCsv(rows)), "text/csv; charset=utf-8", $"jmh-{kind}-{start:yyyyMMdd}-{end.AddDays(-1):yyyyMMdd}.csv");
    }

    private async Task<List<string[]>> ResidentRows(CancellationToken token)
    {
        var rows = new List<string[]> { new[] { "Name", "Resident type", "Status", "Home town" } };
        var data = await db.MemberProfiles.AsNoTracking().OrderBy(item => item.FamilyName).ThenBy(item => item.GivenName)
            .Select(item => new ResidentReportRow(item.PreferredName ?? ((item.GivenName ?? "") + " " + (item.FamilyName ?? "")).Trim(), item.ResidentType.ToString() ?? string.Empty, item.ResidencyStatus.ToString() ?? string.Empty, item.HomeTown)).ToListAsync(token);
        rows.AddRange(data.Select(item => new[] { item.Name ?? string.Empty, item.ResidentType, item.Status, item.HomeTown ?? string.Empty }));
        return rows;
    }
    private async Task<List<string[]>> AttendanceRows(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
    {
        var rows = new List<string[]> { new[] { "Session", "Scheduled UTC", "Resident", "Status" } };
        rows.AddRange(await db.DevotionAttendance.AsNoTracking().Where(item => item.DevotionSession!.ScheduledStartUtc >= start && item.DevotionSession.ScheduledStartUtc < end)
            .OrderBy(item => item.DevotionSession!.ScheduledStartUtc).Select(item => new[] { item.DevotionSession!.Title, item.DevotionSession.ScheduledStartUtc.ToString("O"), item.MemberProfile!.PreferredName ?? ((item.MemberProfile.GivenName ?? "") + " " + (item.MemberProfile.FamilyName ?? "")).Trim(), item.Status.ToString() }).ToListAsync(token));
        return rows;
    }
    private async Task<List<string[]>> DutyRows(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
    {
        var rows = new List<string[]> { new[] { "Duty", "Scheduled UTC", "Assignee", "Status", "Due UTC" } };
        rows.AddRange(await db.DutyAssignments.AsNoTracking().Where(item => item.ScheduledForUtc >= start && item.ScheduledForUtc < end).OrderBy(item => item.ScheduledForUtc)
            .Select(item => new[] { item.Title, item.ScheduledForUtc.ToString("O"), item.AssignedMemberProfile!.PreferredName ?? ((item.AssignedMemberProfile.GivenName ?? "") + " " + (item.AssignedMemberProfile.FamilyName ?? "")).Trim(), item.Status.ToString(), item.DueUtc.HasValue ? item.DueUtc.Value.ToString("O") : "" }).ToListAsync(token));
        return rows;
    }
    private async Task<List<string[]>> EventRows(DateTimeOffset start, DateTimeOffset end, CancellationToken token)
    {
        var rows = new List<string[]> { new[] { "Event", "Starts UTC", "Ends UTC", "Location", "Status" } };
        rows.AddRange(await db.CommunityEvents.AsNoTracking().Where(item => item.StartsUtc >= start && item.StartsUtc < end).OrderBy(item => item.StartsUtc)
            .Select(item => new[] { item.Title, item.StartsUtc.ToString("O"), item.EndsUtc.ToString("O"), item.Location ?? "", item.Status.ToString() }).ToListAsync(token));
        return rows;
    }
    private static bool TryRange(ReportDateFilter filter, out DateTimeOffset start, out DateTimeOffset end, out ValidationProblemDetails problem)
    {
        start = (filter.FromUtc ?? DateTimeOffset.UtcNow.AddDays(-30)).ToUniversalTime(); end = (filter.ToUtc ?? DateTimeOffset.UtcNow.AddDays(1)).ToUniversalTime();
        problem = new ValidationProblemDetails(new Dictionary<string, string[]>());
        if (end <= start || end - start > TimeSpan.FromDays(366)) { problem.Errors.Add("range", ["Choose a date range of up to 366 days with an end after the start."]); return false; }
        return true;
    }
    private static string ToCsv(IEnumerable<string[]> rows) => string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(Csv))));
    // Prefix spreadsheet formula characters after quoting to prevent CSV formula execution in spreadsheet programs.
    private static string Csv(string value) { var safe = value.Length > 0 && "=+-@".Contains(value[0]) ? "'" + value : value; return '"' + safe.Replace("\"", "\"\"") + '"'; }
    private sealed record ResidentReportRow(string? Name, string ResidentType, string Status, string? HomeTown);
}

public sealed class ReportDateFilter { public DateTimeOffset? FromUtc { get; init; } public DateTimeOffset? ToUtc { get; init; } }
public sealed record ReportSummaryResponse(int ActiveResidents, int ArchivedResidents, int DevotionSessions, int PresentAttendance, int AssignedDuties, int CompletedDuties, int ScheduledEvents, DateTimeOffset FromUtc, DateTimeOffset ToUtc);
