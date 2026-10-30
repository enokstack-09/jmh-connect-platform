namespace JMHConnect.Api.Models;

public sealed class DevotionAttendance
{
    public long Id { get; set; }
    public int DevotionSessionId { get; set; }
    public int MemberProfileId { get; set; }
    public DevotionAttendanceStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset RecordedUtc { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
    public DevotionSession? DevotionSession { get; set; }
    public MemberProfile? MemberProfile { get; set; }
    public ApplicationUser? RecordedByUser { get; set; }
}
