namespace JMHConnect.Api.Models;

public sealed class DevotionSessionAuditEntry
{
    public long Id { get; set; }
    public int DevotionSessionId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public DateTimeOffset OccurredUtc { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
    public DevotionSession? DevotionSession { get; set; }
    public ApplicationUser? ActorUser { get; set; }
}
