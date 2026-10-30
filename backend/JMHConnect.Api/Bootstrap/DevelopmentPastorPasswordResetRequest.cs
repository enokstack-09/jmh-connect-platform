namespace JMHConnect.Api.Bootstrap;

public sealed record DevelopmentPastorPasswordResetRequest
{
    public const string ResetFlag = "--reset-first-pastor-password";

    private DevelopmentPastorPasswordResetRequest(string databaseName)
    {
        DatabaseName = databaseName;
    }

    public string DatabaseName { get; }

    public static DevelopmentPastorPasswordResetRequest Parse(IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 3 ||
            !string.Equals(arguments[0], ResetFlag, StringComparison.Ordinal) ||
            !string.Equals(arguments[1], FirstPastorBootstrapRequest.DatabaseFlag, StringComparison.Ordinal) ||
            !string.Equals(arguments[2], FirstPastorBootstrapRequest.DevelopmentDatabaseName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Password recovery accepts only: {ResetFlag} {FirstPastorBootstrapRequest.DatabaseFlag} {FirstPastorBootstrapRequest.DevelopmentDatabaseName}.");
        }

        return new DevelopmentPastorPasswordResetRequest(FirstPastorBootstrapRequest.DevelopmentDatabaseName);
    }

    public static string[] RemoveResetArguments(IReadOnlyList<string> arguments)
    {
        _ = Parse(arguments);
        return [];
    }

    internal static DevelopmentPastorPasswordResetRequest CreateForIntegrationTest(string databaseName) =>
        new(databaseName);
}
