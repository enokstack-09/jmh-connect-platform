using System.ComponentModel.DataAnnotations;

namespace JMHConnect.Api.Models;

public sealed class WorkerResidentDetails
{
    public int MemberProfileId { get; set; }

    [StringLength(200)]
    public string? EmployerName { get; set; }

    [StringLength(150)]
    public string? JobTitle { get; set; }

    [StringLength(200)]
    public string? WorkLocation { get; set; }

    [StringLength(50)]
    public string? WorkPhone { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public byte[] RowVersion { get; set; } = [];

    public MemberProfile MemberProfile { get; set; } = null!;
}
