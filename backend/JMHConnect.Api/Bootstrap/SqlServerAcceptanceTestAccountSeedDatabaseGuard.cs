using System.Data;
using System.Data.Common;
using JMHConnect.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace JMHConnect.Api.Bootstrap;

public sealed class SqlServerAcceptanceTestAccountSeedDatabaseGuard : IAcceptanceTestAccountSeedDatabaseGuard
{
    private const string OwnershipTable = "__JmhAcceptanceOwnership";
    private const string ApplicationLockSql = """
        DECLARE @result int;
        EXEC @result = sp_getapplock
            @Resource = N'JMHConnect:AcceptanceTestAccountSeed',
            @LockMode = N'Exclusive',
            @LockOwner = N'Transaction',
            @LockTimeout = 0;
        SELECT @result;
        """;

    public async Task<IFirstPastorBootstrapTransaction> AcquireVerifiedTransactionAsync(
        ApplicationDbContext db,
        AcceptanceTestAccountSeedRequest request,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlServer())
        {
            throw new InvalidOperationException("Acceptance account seeding requires the SQL Server Identity database provider.");
        }

        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException("The acceptance database is not available. Create and migrate only through the approved procedure.");
        }

        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        var connectedDatabase = await GetConnectedDatabaseNameAsync(connection, cancellationToken);
        if (!string.Equals(connectedDatabase, request.DatabaseName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Acceptance seeding connected to an unexpected database.");
        }

        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await AcquireApplicationLockAsync(connection, transaction, cancellationToken);
            await VerifyOwnershipAsync(connection, transaction, request, cancellationToken);

            var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
            if (!applied.Contains(SqlServerFirstPastorBootstrapDatabaseGuard.InitialIdentityMigrationId, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("InitialIdentity has not been applied to the acceptance database.");
            }

            if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            {
                throw new InvalidOperationException("The acceptance database has pending migrations.");
            }

            return new EfFirstPastorBootstrapTransaction(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private static async Task<string?> GetConnectedDatabaseNameAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_NAME();";
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task AcquireApplicationLockAsync(
        DbConnection connection,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = ApplicationLockSql;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) < 0)
        {
            throw new InvalidOperationException("Another acceptance account seed operation is already in progress.");
        }
    }

    private static async Task VerifyOwnershipAsync(
        DbConnection connection,
        IDbContextTransaction transaction,
        AcceptanceTestAccountSeedRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = $"""
            IF OBJECT_ID(N'dbo.{OwnershipTable}', N'U') IS NULL
                SELECT CAST(0 AS bit);
            ELSE
                SELECT CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.{OwnershipTable}
                    WHERE AcceptanceId = @ownershipId AND DatabaseName = @databaseName
                ) THEN 1 ELSE 0 END AS bit);
            """;

        var ownershipId = command.CreateParameter();
        ownershipId.ParameterName = "@ownershipId";
        ownershipId.Value = request.OwnershipId;
        command.Parameters.Add(ownershipId);

        var databaseName = command.CreateParameter();
        databaseName.ParameterName = "@databaseName";
        databaseName.Value = request.DatabaseName;
        command.Parameters.Add(databaseName);

        if (!Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken)))
        {
            throw new InvalidOperationException("The acceptance database ownership marker is missing or does not match.");
        }
    }
}
