namespace JMHConnect.Api.Bootstrap;

public sealed record FirstPastorBootstrapRequest
{
    public const string BootstrapFlag = "--bootstrap-first-pastor";
    public const string DatabaseFlag = "--database";
    public const string DevelopmentDatabaseName = "JMHConnectDev";

    private FirstPastorBootstrapRequest(string databaseName)
    {
        DatabaseName = databaseName;
    }

    public string DatabaseName { get; }

    public static FirstPastorBootstrapRequest? Parse(IReadOnlyList<string> arguments)
    {
        var bootstrapRequested = arguments.Contains(BootstrapFlag, StringComparer.Ordinal);
        var databaseSpecified = arguments.Contains(DatabaseFlag, StringComparer.Ordinal);
        if (!bootstrapRequested)
        {
            if (databaseSpecified)
            {
                throw new InvalidOperationException($"{DatabaseFlag} is valid only with {BootstrapFlag}.");
            }

            return null;
        }

        if (arguments.Count != 3 ||
            !string.Equals(arguments[0], BootstrapFlag, StringComparison.Ordinal) ||
            !string.Equals(arguments[1], DatabaseFlag, StringComparison.Ordinal) ||
            !string.Equals(arguments[2], DevelopmentDatabaseName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Bootstrap accepts only: {BootstrapFlag} {DatabaseFlag} {DevelopmentDatabaseName}.");
        }

        return new FirstPastorBootstrapRequest(DevelopmentDatabaseName);
    }

    public static string[] RemoveBootstrapArguments(IReadOnlyList<string> arguments)
    {
        if (Parse(arguments) is not null)
        {
            return [];
        }

        return arguments.ToArray();
    }

    internal static FirstPastorBootstrapRequest CreateForIntegrationTest(string databaseName) =>
        new(databaseName);
}
