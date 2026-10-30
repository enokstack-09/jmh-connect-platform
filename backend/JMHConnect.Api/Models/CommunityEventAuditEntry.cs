namespace JMHConnect.Api.Models;

public sealed class CommunityEventAuditEntry
{
    public long Id { get; set; }
    public int CommunityEventId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public DateTimeOffset OccurredUtc { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
    public CommunityEvent? CommunityEvent { get; set; }
    public ApplicationUser? ActorUser { get; set; }
}
