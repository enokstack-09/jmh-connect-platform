namespace JMHConnect.Api.Models;

/// <summary>
/// A member record may exist without an Identity login account.
/// Profile fields will be added with the approved member-management design.
/// </summary>
public sealed class MemberProfile
{
    public int Id { get; set; }

    public string? GivenName { get; set; }

    public string? FamilyName { get; set; }

    public string? PreferredName { get; set; }

    public string? HomeTown { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public bool IsBirthdayVisible { get; set; }

    public DateTimeOffset? BirthdayVisibilityUpdatedUtc { get; set; }

    public string? BirthdayVisibilityUpdatedByUserId { get; set; }

    public ResidentType? ResidentType { get; set; }

    public ResidencyStatus ResidencyStatus { get; set; } = ResidencyStatus.Active;

    public DateTimeOffset? ArchivedUtc { get; set; }

    public string? ArchivedByUserId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public byte[] RowVersion { get; set; } = [];

    public ApplicationUser? LoginAccount { get; set; }
    public ApplicationUser? ArchivedByUser { get; set; }
    public ApplicationUser? BirthdayVisibilityUpdatedByUser { get; set; }
    public StudentResidentDetails? StudentDetails { get; set; }
    public WorkerResidentDetails? WorkerDetails { get; set; }
    public ICollection<MemberContact> Contacts { get; } = new List<MemberContact>();
    public ICollection<MemberProfilePhoto> Photos { get; } = new List<MemberProfilePhoto>();
    public ICollection<PhotoGuardianConsent> PhotoGuardianConsents { get; } = new List<PhotoGuardianConsent>();
    public ICollection<MemberProfileAuditEntry> AuditEntries { get; } = new List<MemberProfileAuditEntry>();
    public ICollection<DevotionAttendance> DevotionAttendance { get; } = new List<DevotionAttendance>();
    public ICollection<DutyAssignment> DutyAssignments { get; } = new List<DutyAssignment>();
}
