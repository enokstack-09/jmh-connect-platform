namespace JMHConnect.Api.Models;

public sealed class DutyAssignment
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int AssignedMemberProfileId { get; set; }
    public DateTimeOffset ScheduledForUtc { get; set; }
    public DateTimeOffset? DueUtc { get; set; }
    public DutyAssignmentStatus Status { get; set; } = DutyAssignmentStatus.Assigned;
    public DateTimeOffset? CompletedUtc { get; set; }
    public string? CompletedByUserId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
    public MemberProfile? AssignedMemberProfile { get; set; }
    public ApplicationUser? CompletedByUser { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }
    public ICollection<DutyAssignmentAuditEntry> AuditEntries { get; } = new List<DutyAssignmentAuditEntry>();
}
