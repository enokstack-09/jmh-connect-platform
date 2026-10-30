using System.ComponentModel.DataAnnotations;

namespace JMHConnect.Api.Models;

public sealed class MemberContact
{
    public int Id { get; set; }

    public int MemberProfileId { get; set; }

    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Relationship { get; set; } = string.Empty;

    [StringLength(50)]
    public string? PhoneNumber { get; set; }

    [EmailAddress, StringLength(254)]
    public string? Email { get; set; }

    public bool IsEmergencyContact { get; set; }

    public bool IsGuardian { get; set; }

    [Range(0, short.MaxValue)]
    public short SortOrder { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public byte[] RowVersion { get; set; } = [];

    public MemberProfile MemberProfile { get; set; } = null!;
}
