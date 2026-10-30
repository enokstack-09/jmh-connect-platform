using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

namespace JMHConnect.Api.Bootstrap;

public static class FirstPastorBootstrapConfiguration
{
    public static void Validate(
        IHostEnvironment environment,
        IConfiguration configuration,
        FirstPastorBootstrapRequest request)
        => Validate(environment, configuration, request.DatabaseName);

    public static void Validate(
        IHostEnvironment environment,
        IConfiguration configuration,
        string expectedDatabaseName)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("First Pastor bootstrap is permitted only in the Development environment.");
        }

        var connectionString = configuration.GetConnectionString("IdentityDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("First Pastor bootstrap requires an explicit IdentityDatabase connection string.");
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
            !string.Equals(connection.InitialCatalog, expectedDatabaseName, StringComparison.Ordinal) ||
            !connection.IntegratedSecurity ||
            !string.Equals(Convert.ToString(connection["Encrypt"]), "True", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Development Identity maintenance requires encrypted Windows Authentication to localhost/{expectedDatabaseName}.");
        }
    }
}
