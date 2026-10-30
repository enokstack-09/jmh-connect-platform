using System.ComponentModel.DataAnnotations;

namespace JMHConnect.Api.Models;

public sealed class MemberProfileAuditEntry
{
    public long Id { get; set; }

    public int MemberProfileId { get; set; }

    [StringLength(450)]
    public string? ActorUserId { get; set; }

    [Required, StringLength(80)]
    public string ActionType { get; set; } = string.Empty;

    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;

    public Guid CorrelationId { get; set; }

    public string? DetailsJson { get; set; }

    public MemberProfile MemberProfile { get; set; } = null!;
    public ApplicationUser? ActorUser { get; set; }
}
