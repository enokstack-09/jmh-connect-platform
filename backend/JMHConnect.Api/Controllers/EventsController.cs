using System.ComponentModel.DataAnnotations;
using JMHConnect.Api.Data;
using JMHConnect.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JMHConnect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/events")]
public sealed class EventsController(ApplicationDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CommunityEventResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await db.CommunityEvents.AsNoTracking().OrderBy(item => item.StartsUtc)
            .Select(item => new CommunityEventResponse(item.Id, item.Title, item.Description, item.StartsUtc,
                item.EndsUtc, item.Location, item.Status)).ToListAsync(cancellationToken));

    [Authorize(Policy = "PastorOnly")]
    [HttpPost]
    public async Task<ActionResult<CommunityEventResponse>> Create(CommunityEventMutation request, CancellationToken cancellationToken)
    {
        if (!TryValidate(request, out var errors)) return BadRequest(new ValidationProblemDetails(errors));
        var now = DateTimeOffset.UtcNow;
        var actor = users.GetUserId(User)!;
        var item = new CommunityEvent
        {
            Title = request.Title.Trim(), Description = Trim(request.Description), Location = Trim(request.Location),
            StartsUtc = request.StartsUtc.ToUniversalTime(), EndsUtc = request.EndsUtc.ToUniversalTime(),
            Status = CommunityEventStatus.Scheduled, CreatedUtc = now, UpdatedUtc = now, CreatedByUserId = actor
        };
        AddAudit(item, "EventCreated", actor, now);
        db.CommunityEvents.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(List), new { id = item.Id }, ToResponse(item));
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CommunityEventMutation request, CancellationToken cancellationToken)
    {
        var item = await db.CommunityEvents.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        if (item is null) return NotFound(new { message = "The community event was not found." });
        if (item.Status == CommunityEventStatus.Cancelled) return Conflict(new { message = "A cancelled event cannot be edited." });
        if (!TryValidate(request, out var errors)) return BadRequest(new ValidationProblemDetails(errors));
        item.Title = request.Title.Trim();
        item.Description = Trim(request.Description);
        item.Location = Trim(request.Location);
        item.StartsUtc = request.StartsUtc.ToUniversalTime();
        item.EndsUtc = request.EndsUtc.ToUniversalTime();
        item.UpdatedUtc = DateTimeOffset.UtcNow;
        AddAudit(item, "EventUpdated", users.GetUserId(User)!, item.UpdatedUtc);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var item = await db.CommunityEvents.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        if (item is null) return NotFound(new { message = "The community event was not found." });
        if (item.Status == CommunityEventStatus.Cancelled) return Conflict(new { message = "The event is already cancelled." });
        item.Status = CommunityEventStatus.Cancelled;
        item.UpdatedUtc = DateTimeOffset.UtcNow;
        AddAudit(item, "EventCancelled", users.GetUserId(User)!, item.UpdatedUtc);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpGet("{id:int}/audit")]
    public async Task<ActionResult<IReadOnlyList<CommunityEventAuditResponse>>> Audit(int id, CancellationToken cancellationToken)
    {
        if (!await db.CommunityEvents.AnyAsync(item => item.Id == id, cancellationToken)) return NotFound();
        return Ok(await db.CommunityEventAuditEntries.AsNoTracking().Where(entry => entry.CommunityEventId == id)
            .OrderByDescending(entry => entry.OccurredUtc)
            .Select(entry => new CommunityEventAuditResponse(entry.Id, entry.ActionType, entry.OccurredUtc,
                entry.ActorUser == null ? "Unknown account" : entry.ActorUser.Email ?? "Unknown account"))
            .ToListAsync(cancellationToken));
    }

    private static bool TryValidate(CommunityEventMutation request, out Dictionary<string, string[]> errors)
    {
        errors = [];
        if (string.IsNullOrWhiteSpace(request.Title)) errors[nameof(request.Title)] = ["An event title is required."];
        if (request.StartsUtc == default) errors[nameof(request.StartsUtc)] = ["A start date and time is required."];
        if (request.EndsUtc == default || request.EndsUtc <= request.StartsUtc) errors[nameof(request.EndsUtc)] = ["The end must be after the start."];
        return errors.Count == 0;
    }
    private static void AddAudit(CommunityEvent item, string action, string actor, DateTimeOffset occurred) =>
        item.AuditEntries.Add(new CommunityEventAuditEntry { ActionType = action, ActorUserId = actor, OccurredUtc = occurred, CorrelationId = Guid.NewGuid() });
    private static CommunityEventResponse ToResponse(CommunityEvent item) => new(item.Id, item.Title, item.Description,
        item.StartsUtc, item.EndsUtc, item.Location, item.Status);
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CommunityEventMutation
{
    [Required, StringLength(200)] public string Title { get; init; } = string.Empty;
    [StringLength(2000)] public string? Description { get; init; }
    public DateTimeOffset StartsUtc { get; init; }
    public DateTimeOffset EndsUtc { get; init; }
    [StringLength(200)] public string? Location { get; init; }
}
public sealed record CommunityEventResponse(int Id, string Title, string? Description, DateTimeOffset StartsUtc,
    DateTimeOffset EndsUtc, string? Location, CommunityEventStatus Status);
public sealed record CommunityEventAuditResponse(long Id, string ActionType, DateTimeOffset OccurredUtc, string ActorEmail);
