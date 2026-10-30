using System.ComponentModel.DataAnnotations;

namespace JMHConnect.Api.Models;

public sealed class StudentResidentDetails
{
    public int MemberProfileId { get; set; }

    [StringLength(200)]
    public string? SchoolName { get; set; }

    [StringLength(100)]
    public string? EducationLevel { get; set; }

    [StringLength(100)]
    public string? StudentIdentifier { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public byte[] RowVersion { get; set; } = [];

    public MemberProfile MemberProfile { get; set; } = null!;
}
