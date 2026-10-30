namespace JMHConnect.Api.Bootstrap;

public sealed record AcceptanceTestAccountSeedRequest
{
    public const string SeedFlag = "--seed-acceptance-test-accounts";
    public const string DatabaseFlag = "--database";
    public const string OwnershipFlag = "--ownership-guid";
    public const string AcceptanceDatabaseName = "JMHConnectAcceptance_20261030_01";
    public static readonly Guid AcceptanceOwnershipId = Guid.Parse("474c2a9b-f3d0-46d1-b59d-1d765a6ef92e");

    private AcceptanceTestAccountSeedRequest()
    {
    }

    public string DatabaseName => AcceptanceDatabaseName;

    public Guid OwnershipId => AcceptanceOwnershipId;

    public static AcceptanceTestAccountSeedRequest? Parse(IReadOnlyList<string> arguments)
    {
        var requested = arguments.Contains(SeedFlag, StringComparer.Ordinal);
        if (!requested)
        {
            if (arguments.Contains(OwnershipFlag, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"{OwnershipFlag} is valid only with {SeedFlag}.");
            }

            return null;
        }

        var expectedOwnership = AcceptanceOwnershipId.ToString("D");
        if (arguments.Count != 5 ||
            !string.Equals(arguments[0], SeedFlag, StringComparison.Ordinal) ||
            !string.Equals(arguments[1], DatabaseFlag, StringComparison.Ordinal) ||
            !string.Equals(arguments[2], AcceptanceDatabaseName, StringComparison.Ordinal) ||
            !string.Equals(arguments[3], OwnershipFlag, StringComparison.Ordinal) ||
            !string.Equals(arguments[4], expectedOwnership, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Acceptance seeding accepts only: {SeedFlag} {DatabaseFlag} {AcceptanceDatabaseName} {OwnershipFlag} {expectedOwnership}.");
        }

        return new AcceptanceTestAccountSeedRequest();
    }

    public static string[] RemoveSeedArguments(IReadOnlyList<string> arguments) =>
        arguments.Where(argument =>
                !string.Equals(argument, SeedFlag, StringComparison.Ordinal) &&
                !string.Equals(argument, DatabaseFlag, StringComparison.Ordinal) &&
                !string.Equals(argument, AcceptanceDatabaseName, StringComparison.Ordinal) &&
                !string.Equals(argument, OwnershipFlag, StringComparison.Ordinal) &&
                !string.Equals(argument, AcceptanceOwnershipId.ToString("D"), StringComparison.Ordinal))
            .ToArray();
}
