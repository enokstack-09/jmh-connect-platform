using System.Data;
using System.Data.Common;
using JMHConnect.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace JMHConnect.Api.Bootstrap;

public sealed class SqlServerFirstPastorBootstrapDatabaseGuard : IFirstPastorBootstrapDatabaseGuard
{
    public const string InitialIdentityMigrationId = "20261013082133_InitialIdentity";

    private const string ApplicationLockSql = """
        DECLARE @result int;
        EXEC @result = sp_getapplock
            @Resource = N'JMHConnect:FirstPastorBootstrap',
            @LockMode = N'Exclusive',
            @LockOwner = N'Transaction',
            @LockTimeout = 0;
        SELECT @result;
        """;

    public async Task<IFirstPastorBootstrapTransaction> AcquireVerifiedTransactionAsync(
        ApplicationDbContext db,
        string expectedDatabaseName,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlServer())
        {
            throw new InvalidOperationException("First Pastor bootstrap requires the SQL Server Identity database provider.");
        }

        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "The configured Identity database is not available. Apply the approved schema before bootstrapping.");
        }

        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        var connectedDatabase = await GetConnectedDatabaseNameAsync(connection, cancellationToken);
        if (!string.Equals(connectedDatabase, expectedDatabaseName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Bootstrap connected to an unexpected database.");
        }

        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await AcquireApplicationLockAsync(connection, transaction, cancellationToken);

            var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
            if (!applied.Contains(InitialIdentityMigrationId, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("InitialIdentity has not been applied to the configured database.");
            }

            var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pending.Any())
            {
                throw new InvalidOperationException("The Identity database has pending migrations. Apply them before bootstrapping.");
            }

            return new EfFirstPastorBootstrapTransaction(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private static async Task<string?> GetConnectedDatabaseNameAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_NAME();";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToString(result);
    }

    private static async Task AcquireApplicationLockAsync(
        DbConnection connection,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = ApplicationLockSql;
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (result < 0)
        {
            throw new InvalidOperationException("Another first Pastor bootstrap operation is already in progress.");
        }
    }
}
