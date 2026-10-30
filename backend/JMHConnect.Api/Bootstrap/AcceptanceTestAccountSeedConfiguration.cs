using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

namespace JMHConnect.Api.Bootstrap;

public static class AcceptanceTestAccountSeedConfiguration
{
    public static void Validate(
        IHostEnvironment environment,
        IConfiguration configuration,
        AcceptanceTestAccountSeedRequest request)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Acceptance account seeding is permitted only in the Development environment.");
        }

        var connectionString = configuration.GetConnectionString("IdentityDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Acceptance account seeding requires an explicit IdentityDatabase connection string.");
        }

        SqlConnectionStringBuilder connection;
        try
        {
            connection = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("The configured IdentityDatabase connection string is invalid.");
        }

        if (!string.Equals(connection.DataSource, "localhost", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(connection.InitialCatalog, request.DatabaseName, StringComparison.Ordinal) ||
            !connection.IntegratedSecurity ||
            !string.IsNullOrWhiteSpace(connection.UserID) ||
            !string.IsNullOrWhiteSpace(connection.Password) ||
            !string.Equals(Convert.ToString(connection["Encrypt"]), "True", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Acceptance account seeding requires encrypted Windows Authentication to localhost/{request.DatabaseName}.");
        }
    }
}
