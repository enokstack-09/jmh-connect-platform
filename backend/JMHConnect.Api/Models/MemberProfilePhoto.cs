using System.ComponentModel.DataAnnotations;

namespace JMHConnect.Api.Models;

public sealed class MemberProfilePhoto
{
    public int Id { get; set; }

    public int MemberProfileId { get; set; }

    [Required, StringLength(512)]
    public string StorageKey { get; set; } = string.Empty;

    [StringLength(255)]
    public string? OriginalFileName { get; set; }

    [Required, StringLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long FileSizeBytes { get; set; }

    public PhotoApprovalStatus ApprovalStatus { get; set; } = PhotoApprovalStatus.Pending;

    public DateTimeOffset? ApprovedUtc { get; set; }

    [StringLength(450)]
    public string? ApprovedByUserId { get; set; }

    public bool IsCurrent { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    [StringLength(450)]
    public string? CreatedByUserId { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public MemberProfile MemberProfile { get; set; } = null!;
    public ApplicationUser? ApprovedByUser { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }
}
