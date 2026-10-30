using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

namespace JMHConnect.Api.Data;

public static class DevelopmentDatabaseTargetConfiguration
{
    public const string ExpectedDatabaseKey = "DatabaseSafety:ExpectedDatabaseName";

    public static void Validate(IHostEnvironment environment, IConfiguration configuration, string connectionString)
    {
        var expectedDatabase = configuration[ExpectedDatabaseKey];
        if (string.IsNullOrWhiteSpace(expectedDatabase)) return;
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("DatabaseSafety:ExpectedDatabaseName is a Development-only safety assertion.");

        SqlConnectionStringBuilder connection;
        try { connection = new SqlConnectionStringBuilder(connectionString); }
        catch (ArgumentException) { throw new InvalidOperationException("The configured IdentityDatabase connection string is invalid."); }

        if (!string.Equals(connection.DataSource, "localhost", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(connection.InitialCatalog, expectedDatabase, StringComparison.Ordinal) ||
            !connection.IntegratedSecurity)
        {
            throw new InvalidOperationException(
                $"The IdentityDatabase connection does not match the asserted localhost Windows-authentication target '{expectedDatabase}'.");
        }
    }
}
