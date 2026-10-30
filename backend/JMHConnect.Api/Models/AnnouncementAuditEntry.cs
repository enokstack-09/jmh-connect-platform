namespace JMHConnect.Api.Models;

public sealed class AnnouncementAuditEntry
{
    public long Id { get; set; }
    public int AnnouncementId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public DateTimeOffset OccurredUtc { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
    public Announcement? Announcement { get; set; }
    public ApplicationUser? ActorUser { get; set; }
}
