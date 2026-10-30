using System.ComponentModel.DataAnnotations;

namespace JMHConnect.Api.Models;

public sealed class PhotoGuardianConsent
{
    public int Id { get; set; }

    public int MemberProfileId { get; set; }

    public int GuardianContactId { get; set; }

    public PhotoConsentStatus ConsentStatus { get; set; } = PhotoConsentStatus.Granted;

    public DateTimeOffset GrantedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? WithdrawnUtc { get; set; }

    [Required, StringLength(450)]
    public string RecordedByUserId { get; set; } = string.Empty;

    [StringLength(512)]
    public string? EvidenceReference { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public MemberProfile MemberProfile { get; set; } = null!;
    public MemberContact GuardianContact { get; set; } = null!;
    public ApplicationUser RecordedByUser { get; set; } = null!;
}
