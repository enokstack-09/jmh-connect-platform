namespace JMHConnect.Api.Models;

public sealed class DevotionSession
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset ScheduledStartUtc { get; set; }
    public short DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public DevotionSessionStatus Status { get; set; } = DevotionSessionStatus.Scheduled;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
    public ApplicationUser? CreatedByUser { get; set; }
    public ICollection<DevotionAttendance> Attendance { get; } = new List<DevotionAttendance>();
    public ICollection<DevotionSessionAuditEntry> AuditEntries { get; } = new List<DevotionSessionAuditEntry>();
}
