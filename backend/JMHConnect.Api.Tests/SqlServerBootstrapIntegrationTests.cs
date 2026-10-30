using JMHConnect.Api.Bootstrap;
using JMHConnect.Api.Data;
using JMHConnect.Api.Models;
using JMHConnect.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace JMHConnect.Api.Tests;

// Intentionally skipped: this fixture creates and drops only a database it names and marks itself.
public sealed class SqlServerBootstrapIntegrationTests
{
    private const string SkipReason = "Requires separate approval: creates and deletes a fixture-owned JMHConnectBootstrapIntegration_<guid> SQL Server database.";

    [Fact(Skip = SkipReason)]
    public async Task RejectsAConfiguredDatabaseOtherThanTheFixtureDatabase()
    {
        await using var fixture = await SqlServerBootstrapFixture.CreateAsync();
        await using var wrongTarget = await fixture.CreateProviderAsync(fixture.ConnectionStringFor("master"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => FirstPastorBootstrapper.RunAsync(wrongTarget, fixture.BootstrapRequest, new SqlServerBootstrapFixture.TestConsole()));
        Assert.Contains($"localhost/{fixture.DatabaseName}", error.Message, StringComparison.Ordinal);
    }

    [Fact(Skip = SkipReason)]
    public async Task SqlServerGuardRejectsAnActualConnectedDatabaseNameMismatch()
    {
        await using var fixture = await SqlServerBootstrapFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IFirstPastorBootstrapDatabaseGuard>().AcquireVerifiedTransactionAsync(
                scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), "DifferentDatabase", CancellationToken.None));
        Assert.Contains("unexpected database", error.Message, StringComparison.Ordinal);
    }

    [Fact(Skip = SkipReason)]
    public async Task RejectsWhenInitialIdentityIsMissingFromMigrationHistory()
    {
        await using var fixture = await SqlServerBootstrapFixture.CreateAsync();
        await fixture.ExecuteAsync("DELETE FROM [__EFMigrationsHistory];");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunBootstrapAsync());
        Assert.Contains("InitialIdentity", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, await fixture.CountAsync("AspNetUsers"));
        Assert.Equal(0, await fixture.CountAsync("AspNetRoles"));
    }

    [Fact(Skip = SkipReason)]
    public async Task RollsBackRolesWhenSqlServerRejectsTheAccountInsert()
    {
        await using var fixture = await SqlServerBootstrapFixture.CreateAsync();
        await fixture.ExecuteAsync("""CREATE TRIGGER [TR_RejectBootstrapUserInsert] ON [dbo].[AspNetUsers] INSTEAD OF INSERT AS BEGIN THROW 51000, 'Test-only bootstrap user insert rejection.', 1; END;""");
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.RunBootstrapAsync());
        Assert.Equal(0, await fixture.CountAsync("AspNetUsers"));
        Assert.Equal(0, await fixture.CountAsync("AspNetRoles"));
    }

    [Fact(Skip = SkipReason)]
    public async Task SqlServerApplicationLockRejectsActualContention()
    {
        await using var fixture = await SqlServerBootstrapFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var guard = scope.ServiceProvider.GetRequiredService<IFirstPastorBootstrapDatabaseGuard>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using (await guard.AcquireVerifiedTransactionAsync(db, fixture.DatabaseName, CancellationToken.None))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunBootstrapAsync());
            Assert.Contains("already in progress", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        await fixture.RunBootstrapAsync();
        Assert.Equal(1, await fixture.CountAsync("AspNetUsers"));
        Assert.Equal(3, await fixture.CountAsync("AspNetRoles"));
    }

    [Fact(Skip = SkipReason)]
    public async Task CreatesOneEnabledConfirmedAccountWithOnlyThePastorRole()
    {
        await using var fixture = await SqlServerBootstrapFixture.CreateAsync();
        await fixture.RunBootstrapAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync();
        var roles = await (from userRole in db.UserRoles join role in db.Roles on userRole.RoleId equals role.Id select role.Name).ToListAsync();
        Assert.True(user.IsEnabled);
        Assert.True(user.EmailConfirmed);
        Assert.Equal([ApplicationRoles.Pastor], roles);
    }

#if RUN_PASSWORD_RECOVERY_SQL_INTEGRATION
    [Fact]
#else
    [Fact(Skip = SkipReason)]
#endif
    public async Task PasswordRecoveryResetsOnlyTheFixturePastorAndChangesTheSecurityStamp()
    {
        var fixture = await SqlServerBootstrapFixture.CreateAsync();
        try
        {
            await fixture.RunBootstrapAsync();
            var before = await fixture.PastorSnapshotAsync();

            await fixture.RunPasswordRecoveryAsync();

            var after = await fixture.PastorSnapshotAsync();
            Assert.NotEqual(before.PasswordHash, after.PasswordHash);
            Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
            Assert.Equal([ApplicationRoles.Pastor], after.Roles);
            Assert.False(await fixture.CheckPastorPasswordAsync("Bootstrap-pass!2026"));
            Assert.True(await fixture.CheckPastorPasswordAsync("Replacement-pass!2026"));
        }
        finally
        {
            await fixture.DisposeAsync();
            Assert.True(fixture.CleanupVerified);
        }
    }

#if RUN_PASSWORD_RECOVERY_SQL_INTEGRATION
    [Fact]
#else
    [Fact(Skip = SkipReason)]
#endif
    public async Task PasswordRecoveryRollsBackWhenSqlServerRejectsTheUserUpdate()
    {
        var fixture = await SqlServerBootstrapFixture.CreateAsync();
        try
        {
            await fixture.RunBootstrapAsync();
            var before = await fixture.PastorSnapshotAsync();
            await fixture.ExecuteAsync("""CREATE TRIGGER [TR_RejectPastorPasswordReset] ON [dbo].[AspNetUsers] INSTEAD OF UPDATE AS BEGIN THROW 51001, 'Test-only password reset rejection.', 1; END;""");

            await Assert.ThrowsAnyAsync<Exception>(() => fixture.RunPasswordRecoveryAsync());

            Assert.Equal(before.PasswordHash, (await fixture.PastorSnapshotAsync()).PasswordHash);
        }
        finally
        {
            await fixture.DisposeAsync();
            Assert.True(fixture.CleanupVerified);
        }
    }

#if RUN_PASSWORD_RECOVERY_SQL_INTEGRATION
    [Fact]
#else
    [Fact(Skip = SkipReason)]
#endif
    public async Task PasswordRecoveryWorksWithMultipleActiveResultSetsEnabled()
    {
        var fixture = await SqlServerBootstrapFixture.CreateAsync();
        try
        {
            await fixture.RunBootstrapAsync();
            var marsConnection = new SqlConnectionStringBuilder(fixture.ConnectionString)
            {
                MultipleActiveResultSets = true
            }.ConnectionString;
            await using var marsProvider = await fixture.CreateProviderAsync(marsConnection);

            await fixture.RunPasswordRecoveryAsync(marsProvider);

            Assert.True(await fixture.CheckPastorPasswordAsync("Replacement-pass!2026"));
        }
        finally
        {
            await fixture.DisposeAsync();
            Assert.True(fixture.CleanupVerified);
        }
    }
}

public sealed class SqlServerMemberManagementMigrationIntegrationTests
{
    private const string SkipReason = "Requires separate approval: creates and deletes a fixture-owned JMHConnectBootstrapIntegration_<guid> SQL Server database.";

#if RUN_MEMBER_MANAGEMENT_SQL_INTEGRATION
    [Fact]
#else
    [Fact(Skip = SkipReason)]
#endif
    public async Task AppliesBothMemberManagementMigrationsWithIdentityCompatibilityAndExpectedConstraints()
    {
        var fixture = await SqlServerBootstrapFixture.CreateAsync();
        try
        {
            await using var connection = new SqlConnection(fixture.ConnectionString);
            await connection.OpenAsync();
            Assert.Equal(fixture.DatabaseName, connection.Database);

            Assert.Equal(1, await ScalarAsync(connection, "SELECT COUNT(*) FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261024052834_AddMemberManagementPhase1';"));
            Assert.Equal(1, await ScalarAsync(connection, "SELECT COUNT(*) FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261024060330_AddMemberProfileHomeTown';"));
            Assert.Equal(1, await ScalarAsync(connection, "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'MemberProfiles' AND COLUMN_NAME = N'HomeTown' AND DATA_TYPE = N'nvarchar' AND CHARACTER_MAXIMUM_LENGTH = 100 AND IS_NULLABLE = N'YES';"));
            Assert.Equal(6, await ScalarAsync(connection, "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME IN (N'MemberContacts', N'MemberProfileAuditEntries', N'MemberProfilePhotos', N'PhotoGuardianConsents', N'StudentResidentDetails', N'WorkerResidentDetails');"));
            Assert.Equal(1, await ScalarAsync(connection, "SELECT COUNT(*) FROM sys.foreign_keys WHERE name = N'FK_AspNetUsers_MemberProfiles_MemberProfileId' AND delete_referential_action_desc = N'NO_ACTION';"));
            Assert.Equal(1, await ScalarAsync(connection, "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND name = N'IX_AspNetUsers_MemberProfileId';"));
            Assert.Equal(1, await ScalarAsync(connection, "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME IN (N'AspNetUsers', N'AspNetRoles', N'AspNetUserRoles', N'MemberProfiles');"));
        }
        finally
        {
            await fixture.DisposeAsync();
            Assert.True(fixture.CleanupVerified);
        }
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}

public sealed class SqlServerBootstrapFixtureSafetyTests
{
    [Theory]
    [InlineData("Server=localhost;Integrated Security=True;Encrypt=True;Database=JMHConnectDev")]
    [InlineData("Server=localhost;Integrated Security=True;Encrypt=True;Database=master")]
    [InlineData("Server=remote.example;Integrated Security=True;Encrypt=True")]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Integrated Security=True;Encrypt=True")]
    [InlineData("Server=localhost;User ID=sa;Password=not-a-secret;Encrypt=True")]
    public void RejectsUnsafeOrDatabaseTargetedServerConfiguration(string connectionString) =>
        Assert.Throws<InvalidOperationException>(() => SqlServerBootstrapFixtureSafety.Create(connectionString));

    [Fact]
    public void GeneratesDistinctFixtureOwnedDatabaseNames()
    {
        var first = SqlServerBootstrapFixtureSafety.Create("Server=localhost;Integrated Security=True;Encrypt=True");
        var second = SqlServerBootstrapFixtureSafety.Create("Server=localhost;Integrated Security=True;Encrypt=True");
        Assert.Matches("^JMHConnectBootstrapIntegration_[0-9a-f]{32}$", first.DatabaseName);
        Assert.NotEqual(first.DatabaseName, second.DatabaseName);
        Assert.Equal("localhost", new SqlConnectionStringBuilder(first.ServerConnectionString).DataSource);
    }
}

internal sealed class SqlServerBootstrapFixture : IAsyncDisposable
{
    private const string EnvironmentVariable = "JMH_BOOTSTRAP_INTEGRATION_SQL_SERVER";
    private const string OwnershipTable = "__JmhBootstrapIntegrationOwnership";
    private const string CleanupLockSql = "DECLARE @result int; EXEC @result = sp_getapplock @Resource = @resource, @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0; SELECT @result;";
    private ServiceProvider? provider;
    private readonly SqlServerBootstrapFixtureSafety safety;
    private bool databaseCreated;
    private int databaseId;

    private SqlServerBootstrapFixture(SqlServerBootstrapFixtureSafety safety) { this.safety = safety; }
    public IServiceProvider Services => provider ?? throw new InvalidOperationException("Fixture has not initialized.");
    public string ConnectionString => safety.DatabaseConnectionString;
    public string DatabaseName => safety.DatabaseName;
    public bool CleanupVerified { get; private set; }
    public FirstPastorBootstrapRequest BootstrapRequest => FirstPastorBootstrapRequest.CreateForIntegrationTest(DatabaseName);
    public DevelopmentPastorPasswordResetRequest PasswordResetRequest => DevelopmentPastorPasswordResetRequest.CreateForIntegrationTest(DatabaseName);

    public static async Task<SqlServerBootstrapFixture> CreateAsync()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariable) ?? throw new InvalidOperationException($"Set {EnvironmentVariable} only for an approved integration-test run.");
        var fixture = new SqlServerBootstrapFixture(SqlServerBootstrapFixtureSafety.Create(configured));
        try
        {
            await fixture.CreateOwnedDatabaseAsync();
            fixture.provider = BuildProvider(fixture.ConnectionString);
            await using var scope = fixture.provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
            return fixture;
        }
        catch
        {
            if (fixture.provider is not null) await fixture.provider.DisposeAsync();
            await fixture.TryDeleteOwnedDatabaseAsync();
            throw;
        }
    }

    public Task<ServiceProvider> CreateProviderAsync(string connectionString) => Task.FromResult(BuildProvider(connectionString));
    public Task RunBootstrapAsync(TestConsole? console = null) => FirstPastorBootstrapper.RunAsync(Services, BootstrapRequest, console ?? new TestConsole());
    public Task RunPasswordRecoveryAsync(IServiceProvider? services = null) => DevelopmentPastorPasswordResetter.RunAsync(
        services ?? Services,
        PasswordResetRequest,
        new ResetConsole(DatabaseName));
    public async Task<PastorSnapshot> PastorSnapshotAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync("pastor@example.test") ?? throw new InvalidOperationException("Fixture Pastor is missing.");
        return new PastorSnapshot(user.PasswordHash, user.SecurityStamp, (await users.GetRolesAsync(user)).OrderBy(role => role).ToArray());
    }
    public async Task<bool> CheckPastorPasswordAsync(string password)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync("pastor@example.test") ?? throw new InvalidOperationException("Fixture Pastor is missing.");
        return await users.CheckPasswordAsync(user, password);
    }
    public async Task ExecuteAsync(string sql)
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlRawAsync(sql);
    }
    public async Task<int> CountAsync(string tableName)
    {
        if (tableName is not ("AspNetUsers" or "AspNetRoles")) throw new ArgumentOutOfRangeException(nameof(tableName));
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return tableName == "AspNetUsers" ? await db.Users.CountAsync() : await db.Roles.CountAsync();
    }
    public string ConnectionStringFor(string databaseName) => new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = databaseName }.ConnectionString;
    public async ValueTask DisposeAsync()
    {
        if (provider is not null) await provider.DisposeAsync();
        await TryDeleteOwnedDatabaseAsync();
    }

    private async Task CreateOwnedDatabaseAsync()
    {
        await using var server = new SqlConnection(safety.ServerConnectionString);
        await server.OpenAsync();
        await using (var exists = new SqlCommand("SELECT DB_ID(@name);", server))
        {
            exists.Parameters.AddWithValue("@name", DatabaseName);
            if (await exists.ExecuteScalarAsync() is not DBNull and not null) throw new InvalidOperationException("Generated integration-test database name already exists; refusing to use it.");
        }
        await using (var create = new SqlCommand($"CREATE DATABASE [{DatabaseName}];", server)) await create.ExecuteNonQueryAsync();
        databaseCreated = true;
        await using var database = new SqlConnection(ConnectionString);
        await database.OpenAsync();
        await using var marker = new SqlCommand($"""CREATE TABLE [dbo].[{OwnershipTable}] ([FixtureId] uniqueidentifier NOT NULL PRIMARY KEY, [DatabaseName] nvarchar(128) NOT NULL); INSERT INTO [dbo].[{OwnershipTable}] ([FixtureId], [DatabaseName]) VALUES (@fixtureId, @databaseName); SELECT DB_ID();""", database);
        marker.Parameters.AddWithValue("@fixtureId", safety.FixtureId);
        marker.Parameters.AddWithValue("@databaseName", DatabaseName);
        databaseId = Convert.ToInt32(await marker.ExecuteScalarAsync());
    }

    private async Task TryDeleteOwnedDatabaseAsync()
    {
        if (!databaseCreated || databaseId == 0)
        {
            return;
        }

        try
        {
            await using var server = new SqlConnection(safety.ServerConnectionString);
            await server.OpenAsync();
            await using var lockCommand = new SqlCommand(CleanupLockSql, server);
            lockCommand.Parameters.AddWithValue("@resource", $"JMHConnect:IntegrationFixtureCleanup:{DatabaseName}");
            if (Convert.ToInt32(await lockCommand.ExecuteScalarAsync()) < 0)
            {
                Console.Error.WriteLine($"Integration fixture left database '{DatabaseName}' in place because cleanup ownership lock could not be acquired.");
                return;
            }

            // Revalidate immediately before the destructive command while holding the fixture cleanup lock.
            if (!await HasOwnershipMarkerAsync())
            {
                Console.Error.WriteLine($"Integration fixture left database '{DatabaseName}' in place because ownership could not be verified.");
                return;
            }

            await using var drop = new SqlCommand($"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}];", server);
            await drop.ExecuteNonQueryAsync();
            databaseCreated = false;
            CleanupVerified = true;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Integration fixture left database '{DatabaseName}' in place because cleanup failed: {error.GetType().Name}.");
            throw;
        }
    }

    private async Task<bool> HasOwnershipMarkerAsync()
    {
        try
        {
            await using var database = new SqlConnection(ConnectionString);
            await database.OpenAsync();
            await using var marker = new SqlCommand($"""SELECT CASE WHEN DB_ID() = @databaseId AND DB_NAME() = @databaseName AND EXISTS (SELECT 1 FROM [dbo].[{OwnershipTable}] WHERE [FixtureId] = @fixtureId AND [DatabaseName] = @databaseName) THEN 1 ELSE 0 END;""", database);
            marker.Parameters.AddWithValue("@databaseId", databaseId);
            marker.Parameters.AddWithValue("@databaseName", DatabaseName);
            marker.Parameters.AddWithValue("@fixtureId", safety.FixtureId);
            return Convert.ToInt32(await marker.ExecuteScalarAsync()) == 1;
        }
        catch (SqlException) { return false; }
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddDataProtection(); services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:IdentityDatabase"] = connectionString }).Build());
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddIdentityCore<ApplicationUser>(options => { options.User.RequireUniqueEmail = true; options.Password.RequiredLength = 12; options.Password.RequireDigit = true; options.Password.RequireLowercase = true; options.Password.RequireUppercase = true; options.Password.RequireNonAlphanumeric = true; }).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        services.AddSingleton<IFirstPastorBootstrapDatabaseGuard, SqlServerFirstPastorBootstrapDatabaseGuard>();
        return services.BuildServiceProvider(validateScopes: true);
    }
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development; public string ApplicationName { get; set; } = "JMHConnect.Api.Tests"; public string ContentRootPath { get; set; } = Path.GetTempPath(); public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
    public sealed class TestConsole(string email = "pastor@example.test") : IFirstPastorBootstrapConsole
    {
        private readonly Queue<string> passwords = new(["Bootstrap-pass!2026", "Bootstrap-pass!2026"]); private readonly Queue<string> lines = new([email, string.Empty]); public bool IsInputRedirected => false; public void Write(string value) { } public void WriteLine(string value = "") { } public string? ReadLine() => lines.Dequeue(); public string ReadPassword(bool showPassword) => passwords.Dequeue();
    }
    private sealed class ResetConsole : IFirstPastorBootstrapConsole
    {
        private readonly Queue<string> lines;
        private readonly Queue<string> passwords = new(["Replacement-pass!2026", "Replacement-pass!2026"]);
        public ResetConsole(string databaseName) => lines = new Queue<string>(["pastor@example.test", string.Empty, $"RESET {databaseName}"]);
        public bool IsInputRedirected => false;
        public void Write(string value) { }
        public void WriteLine(string value = "") { }
        public string? ReadLine() => lines.Dequeue();
        public string ReadPassword(bool showPassword) => passwords.Dequeue();
    }
    public sealed record PastorSnapshot(string? PasswordHash, string? SecurityStamp, string[] Roles);
}

internal sealed class SqlServerBootstrapFixtureSafety
{
    private SqlServerBootstrapFixtureSafety(SqlConnectionStringBuilder builder, string databaseName, Guid fixtureId)
    {
        DatabaseName = databaseName; FixtureId = fixtureId; builder.InitialCatalog = string.Empty; ServerConnectionString = builder.ConnectionString; builder.InitialCatalog = databaseName; DatabaseConnectionString = builder.ConnectionString;
    }
    public string DatabaseName { get; } public Guid FixtureId { get; } public string ServerConnectionString { get; } public string DatabaseConnectionString { get; }
    public static SqlServerBootstrapFixtureSafety Create(string configuredConnection)
    {
        var builder = new SqlConnectionStringBuilder(configuredConnection);
        if (!string.Equals(builder.DataSource, "localhost", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(builder.InitialCatalog) || !builder.IntegratedSecurity || !builder.Encrypt || !string.IsNullOrWhiteSpace(builder.UserID) || !string.IsNullOrWhiteSpace(builder.Password) || !string.IsNullOrWhiteSpace(builder.AttachDBFilename)) throw new InvalidOperationException("Integration tests require only an encrypted Windows Authentication localhost SQL Server configuration with no database specified.");
        var fixtureId = Guid.NewGuid(); return new SqlServerBootstrapFixtureSafety(builder, $"JMHConnectBootstrapIntegration_{fixtureId:N}", fixtureId);
    }
}
