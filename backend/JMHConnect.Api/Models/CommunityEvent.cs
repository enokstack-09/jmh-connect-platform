namespace JMHConnect.Api.Models;

public sealed class CommunityEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset StartsUtc { get; set; }
    public DateTimeOffset EndsUtc { get; set; }
    public string? Location { get; set; }
    public CommunityEventStatus Status { get; set; } = CommunityEventStatus.Scheduled;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
    public ApplicationUser? CreatedByUser { get; set; }
    public ICollection<CommunityEventAuditEntry> AuditEntries { get; } = new List<CommunityEventAuditEntry>();
}
