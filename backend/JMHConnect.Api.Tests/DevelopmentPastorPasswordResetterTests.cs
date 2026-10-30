using JMHConnect.Api.Bootstrap;
using JMHConnect.Api.Data;
using JMHConnect.Api.Models;
using JMHConnect.Api.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace JMHConnect.Api.Tests;

public sealed class DevelopmentPastorPasswordResetterTests
{
    [Fact]
    public async Task ResetsOnlyPastorPasswordAndInvalidatesExistingSessions()
    {
        await using var fixture = await PasswordResetFixture.CreateAsync();
        var before = await fixture.SnapshotAsync();
        var unrelatedBefore = await fixture.UnrelatedSnapshotAsync();

        await fixture.RunAsync();

        var after = await fixture.SnapshotAsync();
        Assert.NotEqual(before.PasswordHash, after.PasswordHash);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(before.MemberProfileId, after.MemberProfileId);
        Assert.Equal([ApplicationRoles.Pastor], after.Roles);
        AssertSnapshotEqual(unrelatedBefore, await fixture.UnrelatedSnapshotAsync());
        Assert.True(await fixture.CheckPasswordAsync("Replacement-pass!2026"));
        Assert.False(await fixture.CheckPasswordAsync("Original-pass!2026"));
        Assert.Equal(2, await fixture.UserCountAsync());
        Assert.Equal(2, await fixture.MemberProfileCountAsync());
        Assert.Equal([false, false], fixture.Console.PasswordVisibilityModes);
    }

    [Fact]
    public async Task AllowsExplicitShowChoiceForBothPasswordPrompts()
    {
        await using var fixture = await PasswordResetFixture.CreateAsync(showPassword: true);

        await fixture.RunAsync();

        Assert.Equal([true, true], fixture.Console.PasswordVisibilityModes);
    }

    [Fact]
    public async Task RejectionBeforeConfirmationLeavesExistingAccountUnchanged()
    {
        await using var fixture = await PasswordResetFixture.CreateAsync(confirmation: "no");
        var before = await fixture.SnapshotAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync());

        Assert.Contains("not confirmed", error.Message, StringComparison.OrdinalIgnoreCase);
        AssertSnapshotEqual(before, await fixture.SnapshotAsync());
        Assert.Equal(0, fixture.Guard.AcquireCount);
    }

    [Fact]
    public async Task InvalidPasswordIsRejectedBeforeTheDatabaseLockAndLeavesDataUnchanged()
    {
        await using var fixture = await PasswordResetFixture.CreateAsync(
            password: "short", confirmationPassword: "short");
        var before = await fixture.SnapshotAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync());

        AssertSnapshotEqual(before, await fixture.SnapshotAsync());
        Assert.Equal(0, fixture.Guard.AcquireCount);
    }

    [Fact]
    public async Task RequiresExactlyOneEnabledConfirmedPastorAccount()
    {
        await using var fixture = await PasswordResetFixture.CreateAsync();
        await fixture.CreateAdditionalPastorAsync();
        var before = await fixture.SnapshotAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync());

        Assert.Contains("only Pastor", error.Message, StringComparison.Ordinal);
        AssertSnapshotEqual(before, await fixture.SnapshotAsync());
        Assert.Equal(0, fixture.Guard.AcquireCount);
    }

    [Fact]
    public async Task RejectsOutsideDevelopmentWithoutChangingTheAccount()
    {
        await using var fixture = await PasswordResetFixture.CreateAsync(environment: Environments.Production);
        var before = await fixture.SnapshotAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync());

        Assert.Contains("Development", error.Message, StringComparison.Ordinal);
        AssertSnapshotEqual(before, await fixture.SnapshotAsync());
        Assert.Equal(0, fixture.Guard.AcquireCount);
    }

    [Fact]
    public async Task ExistingSqlLockRejectsContentionWithoutChangingTheAccount()
    {
        var guard = new ContentionGuard();
        await using var fixture = await PasswordResetFixture.CreateAsync(guard: guard);
        await using var heldLock = await guard.AcquireVerifiedTransactionAsync(null!, FirstPastorBootstrapRequest.DevelopmentDatabaseName, CancellationToken.None);
        var before = await fixture.SnapshotAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync());

        Assert.Contains("already in progress", error.Message, StringComparison.OrdinalIgnoreCase);
        AssertSnapshotEqual(before, await fixture.SnapshotAsync());
    }

    [Fact]
    public void RequiresExactRecoveryArguments()
    {
        Assert.NotNull(DevelopmentPastorPasswordResetRequest.Parse(
            ["--reset-first-pastor-password", "--database", "JMHConnectDev"]));
        Assert.Throws<InvalidOperationException>(() => DevelopmentPastorPasswordResetRequest.Parse(
            ["--reset-first-pastor-password", "--database", "JMHConnect"]));
        Assert.Throws<InvalidOperationException>(() => DevelopmentPastorPasswordResetRequest.Parse(
            ["--reset-first-pastor-password"]));
        Assert.Throws<InvalidOperationException>(() => DevelopmentPastorPasswordResetRequest.Parse(
            ["--reset-first-pastor-password", "--database", "JMHConnectDev", "--urls", "http://localhost:5080"]));
    }

    private static void AssertSnapshotEqual(AccountSnapshot expected, AccountSnapshot actual)
    {
        Assert.Equal(expected.PasswordHash, actual.PasswordHash);
        Assert.Equal(expected.SecurityStamp, actual.SecurityStamp);
        Assert.Equal(expected.MemberProfileId, actual.MemberProfileId);
        Assert.Equal(expected.Roles, actual.Roles);
    }

    private sealed class PasswordResetFixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;

        private PasswordResetFixture(ServiceProvider provider, TestConsole console, TestGuard guard)
        {
            this.provider = provider;
            Console = console;
            Guard = guard;
        }

        public TestConsole Console { get; }
        public TestGuard Guard { get; }

        public static async Task<PasswordResetFixture> CreateAsync(
            string environment = "Development",
            bool showPassword = false,
            string password = "Replacement-pass!2026",
            string? confirmationPassword = null,
            string confirmation = "RESET JMHConnectDev",
            TestGuard? guard = null)
        {
            var services = new ServiceCollection();
            var databaseRoot = new InMemoryDatabaseRoot();
            var databaseName = $"PasswordResetTests-{Guid.NewGuid():N}";
            services.AddLogging();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environment));
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:IdentityDatabase"] = "Server=localhost;Database=JMHConnectDev;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
                })
                .Build());
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(
                databaseName, databaseRoot));
            services.AddIdentityCore<ApplicationUser>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.Password.RequiredLength = 12;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            var effectiveGuard = guard ?? new TestGuard();
            services.AddSingleton<IFirstPastorBootstrapDatabaseGuard>(effectiveGuard);
            var provider = services.BuildServiceProvider(validateScopes: true);
            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Database.EnsureCreatedAsync();
                var profile = new MemberProfile();
                db.MemberProfiles.Add(profile);
                var unrelatedProfile = new MemberProfile();
                db.MemberProfiles.Add(unrelatedProfile);
                await db.SaveChangesAsync();

                var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                foreach (var role in ApplicationRoles.All)
                {
                    Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
                }

                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = new ApplicationUser
                {
                    UserName = "pastor@example.test",
                    Email = "pastor@example.test",
                    EmailConfirmed = true,
                    IsEnabled = true,
                    MemberProfileId = profile.Id
                };
                Assert.True((await users.CreateAsync(user, "Original-pass!2026")).Succeeded);
                Assert.True((await users.AddToRoleAsync(user, ApplicationRoles.Pastor)).Succeeded);
                var unrelatedUser = new ApplicationUser
                {
                    UserName = "leader@example.test",
                    Email = "leader@example.test",
                    EmailConfirmed = true,
                    IsEnabled = true,
                    MemberProfileId = unrelatedProfile.Id
                };
                Assert.True((await users.CreateAsync(unrelatedUser, "Unrelated-leader!2026")).Succeeded);
                Assert.True((await users.AddToRoleAsync(unrelatedUser, ApplicationRoles.Leader)).Succeeded);
            }

            var visibility = showPassword ? "SHOW" : string.Empty;
            return new PasswordResetFixture(
                provider,
                new TestConsole(
                    new Queue<string>(["pastor@example.test", visibility, confirmation]),
                    new Queue<string>([password, confirmationPassword ?? password])),
                effectiveGuard);
        }

        public Task RunAsync() => DevelopmentPastorPasswordResetter.RunAsync(
            provider,
            DevelopmentPastorPasswordResetRequest.Parse(["--reset-first-pastor-password", "--database", "JMHConnectDev"]),
            Console);

        public async Task<AccountSnapshot> SnapshotAsync()
        {
            using var scope = provider.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync("pastor@example.test");
            Assert.NotNull(user);
            return new AccountSnapshot(user.PasswordHash, user.SecurityStamp, user.MemberProfileId,
                (await users.GetRolesAsync(user)).OrderBy(role => role).ToArray());
        }

        public async Task<bool> CheckPasswordAsync(string password)
        {
            using var scope = provider.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync("pastor@example.test");
            Assert.NotNull(user);
            return await users.CheckPasswordAsync(user, password);
        }

        public async Task<AccountSnapshot> UnrelatedSnapshotAsync()
        {
            using var scope = provider.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync("leader@example.test");
            Assert.NotNull(user);
            return new AccountSnapshot(user.PasswordHash, user.SecurityStamp, user.MemberProfileId,
                (await users.GetRolesAsync(user)).OrderBy(role => role).ToArray());
        }

        public async Task<int> UserCountAsync()
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync();
        }

        public async Task<int> MemberProfileCountAsync()
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().MemberProfiles.CountAsync();
        }

        public async Task CreateAdditionalPastorAsync()
        {
            using var scope = provider.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "other@example.test", Email = "other@example.test", EmailConfirmed = true };
            Assert.True((await users.CreateAsync(user, "Other-pastor!2026")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, ApplicationRoles.Pastor)).Succeeded);
        }

        public ValueTask DisposeAsync() => provider.DisposeAsync();
    }

    private sealed record AccountSnapshot(string? PasswordHash, string? SecurityStamp, int? MemberProfileId, string[] Roles);

    private sealed class TestConsole(Queue<string> lines, Queue<string> passwords) : IFirstPastorBootstrapConsole
    {
        public List<bool> PasswordVisibilityModes { get; } = [];
        public bool IsInputRedirected => false;
        public void Write(string value) { }
        public void WriteLine(string value = "") { }
        public string? ReadLine() => lines.Dequeue();
        public string ReadPassword(bool showPassword)
        {
            PasswordVisibilityModes.Add(showPassword);
            return passwords.Dequeue();
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "JMHConnect.Api.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private class TestGuard : IFirstPastorBootstrapDatabaseGuard
    {
        public int AcquireCount { get; private set; }
        public virtual Task<IFirstPastorBootstrapTransaction> AcquireVerifiedTransactionAsync(
            ApplicationDbContext db,
            string expectedDatabaseName,
            CancellationToken cancellationToken)
        {
            AcquireCount++;
            return Task.FromResult<IFirstPastorBootstrapTransaction>(new TestTransaction());
        }
    }

    private sealed class ContentionGuard : TestGuard
    {
        private bool held;

        public override Task<IFirstPastorBootstrapTransaction> AcquireVerifiedTransactionAsync(
            ApplicationDbContext db,
            string expectedDatabaseName,
            CancellationToken cancellationToken)
        {
            if (held)
            {
                return Task.FromException<IFirstPastorBootstrapTransaction>(
                    new InvalidOperationException("Another first Pastor bootstrap operation is already in progress."));
            }

            held = true;
            return Task.FromResult<IFirstPastorBootstrapTransaction>(new ReleasingTransaction(this));
        }

        private void Release() => held = false;

        private sealed class ReleasingTransaction(ContentionGuard guard) : IFirstPastorBootstrapTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public ValueTask DisposeAsync()
            {
                guard.Release();
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class TestTransaction : IFirstPastorBootstrapTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
