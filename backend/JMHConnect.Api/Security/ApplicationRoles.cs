namespace JMHConnect.Api.Security;

public static class ApplicationRoles
{
    public const string Pastor = "Pastor";
    public const string Leader = "Leader";
    public const string Member = "Member";

    public static readonly IReadOnlyList<string> All = [Pastor, Leader, Member];
    public static readonly IReadOnlyList<string> AccountCreationRoles = [Leader, Member];
}
