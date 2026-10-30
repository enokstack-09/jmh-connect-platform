namespace JMHConnect.Api.Models;

public sealed class Announcement
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;
    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.Everyone;
    public AnnouncementStatus Status { get; set; } = AnnouncementStatus.Draft;
    public DateTimeOffset? PublishedUtc { get; set; }
    public DateTimeOffset? ArchivedUtc { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
    public ApplicationUser? CreatedByUser { get; set; }
    public ICollection<AnnouncementAuditEntry> AuditEntries { get; } = new List<AnnouncementAuditEntry>();
}
