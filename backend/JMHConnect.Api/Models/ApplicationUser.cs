using Microsoft.AspNetCore.Identity;

namespace JMHConnect.Api.Models;

public sealed class ApplicationUser : IdentityUser
{
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool IsEnabled { get; set; } = true;

    public int? MemberProfileId { get; set; }

    public MemberProfile? MemberProfile { get; set; }
}
