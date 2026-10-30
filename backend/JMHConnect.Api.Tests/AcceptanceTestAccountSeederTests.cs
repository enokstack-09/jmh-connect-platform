using JMHConnect.Api.Bootstrap;
using JMHConnect.Api.Data;
using JMHConnect.Api.Models;
using JMHConnect.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace JMHConnect.Api.Tests;

public sealed class AcceptanceTestAccountSeederTests
{
    private static readonly string[] ExactArguments =
    [
        "--seed-acceptance-test-accounts",
        "--database",
        "JMHConnectAcceptance_20261030_01",
        "--ownership-guid",
        "474c2a9b-f3d0-46d1-b59d-1d765a6ef92e"
    ];

    [Fact]
    public void ParserAcceptsOnlyExactTargetAndOwnershipMarker()
    {
        var request = Assert.IsType<AcceptanceTestAccountSeedRequest>(AcceptanceTestAccountSeedRequest.Parse(ExactArguments));

        Assert.Equal("JMHConnectAcceptance_20261030_01", request.DatabaseName);
        Assert.Equal(Guid.Parse("474c2a9b-f3d0-46d1-b59d-1d765a6ef92e"), request.OwnershipId);

        Assert.Throws<InvalidOperationException>(() => AcceptanceTestAccountSeedRequest.Parse(
            [ExactArguments[0], ExactArguments[1], "JMHConnectDev", ExactArguments[3], ExactArguments[4]]));
        Assert.Throws<InvalidOperationException>(() => AcceptanceTestAccountSeedRequest.Parse(
            [ExactArguments[0], ExactArguments[1], ExactArguments[2], ExactArguments[3], Guid.NewGuid().ToString("D")]));
        Assert.Throws<InvalidOperationException>(() => AcceptanceTestAccountSeedRequest.Parse(
            [.. ExactArguments, "--urls", "http://localhost:5080"]));
        Assert.Throws<InvalidOperationException>(() => AcceptanceTestAccountSeedRequest.Parse(
            ["--ownership-guid", ExactArguments[4]]));
    }

    [Theory]
    [InlineData("Production", "Server=localhost;Database=JMHConnectAcceptance_20261030_01;Integrated Security=True;Encrypt=True")]
    [InlineData("Development", "Server=remote.example;Database=JMHConnectAcceptance_20261030_01;Integrated Security=True;Encrypt=True")]
    [InlineData("Development", "Server=(localdb)\\MSSQLLocalDB;Database=JMHConnectAcceptance_20261030_01;Integrated Security=True;Encrypt=True")]
    [InlineData("Development", "Server=localhost;Database=JMHConnectDev;Integrated Security=True;Encrypt=True")]
    [InlineData("Development", "Server=localhost;Database=JMHConnectAcceptance_20261030_01;User ID=sa;Password=Not-A-Real-Secret!;Encrypt=True")]
    [InlineData("Development", "Server=localhost;Database=JMHConnectAcceptance_20261030_01;Integrated Security=True;Encrypt=False")]
    public void ConfigurationRejectsUnsafeEnvironmentOrConnection(string environment, string connectionString)
    {
        var configuration = Configuration(connectionString);

        Assert.Throws<InvalidOperationException>(() => AcceptanceTestAccountSeedConfiguration.Validate(
            new TestHostEnvironment(environment),
            configuration,
            Request()));
    }

    [Fact]
    public async Task CreatesExactlyOnePastorAndOneProfileLinkedMemberThroughIdentity()
    {
        await using var fixture = await SeedFixture.CreateAsync();

        await fixture.RunAsync();

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = await db.Users.OrderBy(user => user.Email).ToListAsync();
        Assert.Equal(2, users.Count);
        Assert.All(users, user =>
        {
            Assert.True(user.EmailConfirmed);
            Assert.True(user.IsEnabled);
            Assert.NotNull(user.PasswordHash);
        });

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var pastor = users.Single(user => user.Email == "acceptance-pastor@example.test");
        var member = users.Single(user => user.Email == "acceptance-member@example.test");
        Assert.Equal([ApplicationRoles.Pastor], await userManager.GetRolesAsync(pastor));
        Assert.Equal([ApplicationRoles.Member], await userManager.GetRolesAsync(member));
        Assert.NotNull(member.MemberProfileId);
        Assert.Equal(member.MemberProfileId, (await db.MemberProfiles.SingleAsync()).Id);
        Assert.Equal(ApplicationRoles.All.OrderBy(role => role), await db.Roles.Select(role => role.Name!).OrderBy(role => role).ToListAsync());
        Assert.Equal(Request().OwnershipId, fixture.Guard.Request?.OwnershipId);
        Assert.Equal(Request().DatabaseName, fixture.Guard.Request?.DatabaseName);
    }

    [Fact]
    public async Task NeverDisplaysOrStoresPlaintextPasswordsInConsoleOutput()
    {
        await using var fixture = await SeedFixture.CreateAsync();

        await fixture.RunAsync();

        Assert.Equal([false, false, false, false], fixture.Console.PasswordVisibilityModes);
        Assert.DoesNotContain("Pastor-pass!2026", fixture.Console.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Member-pass!2026", fixture.Console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsNonEmptyUserStoreWithoutChangingIt()
    {
        await using var fixture = await SeedFixture.CreateAsync();
        await fixture.CreateExistingUserAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync());

        Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(await db.Users.ToListAsync());
        Assert.Empty(await db.Roles.ToListAsync());
        Assert.Empty(await db.MemberProfiles.ToListAsync());
    }

    [Fact]
    public async Task GuardFailurePreventsAnyDatabaseChanges()
    {
        await using var fixture = await SeedFixture.CreateAsync(new RecordingGuard("ownership marker mismatch"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync());

        Assert.Contains("ownership", error.Message, StringComparison.OrdinalIgnoreCase);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Roles.ToListAsync());
        Assert.Empty(await db.MemberProfiles.ToListAsync());
    }

    private static IConfiguration Configuration(string connectionString) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:IdentityDatabase"] = connectionString
        }).Build();

    private static AcceptanceTestAccountSeedRequest Request() =>
        AcceptanceTestAccountSeedRequest.Parse(ExactArguments)!;

    private sealed class SeedFixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;

        private SeedFixture(ServiceProvider provider, RecordingGuard guard, TestConsole console)
        {
            this.provider = provider;
            Guard = guard;
            Console = console;
        }

        public IServiceProvider Services => provider;
        public RecordingGuard Guard { get; }
        public TestConsole Console { get; }

        public static async Task<SeedFixture> CreateAsync(RecordingGuard? guard = null)
        {
            guard ??= new RecordingGuard();
            var databaseRoot = new InMemoryDatabaseRoot();
            var databaseName = $"AcceptanceSeed-{Guid.NewGuid():N}";
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(Environments.Development));
            services.AddSingleton(Configuration(
                "Server=localhost;Database=JMHConnectAcceptance_20261030_01;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"));
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName, databaseRoot));
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
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddSingleton<IAcceptanceTestAccountSeedDatabaseGuard>(guard);

            var provider = services.BuildServiceProvider(validateScopes: true);
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
            return new SeedFixture(provider, guard, new TestConsole());
        }

        public Task RunAsync() => AcceptanceTestAccountSeeder.RunAsync(provider, Request(), Console);

        public async Task CreateExistingUserAsync()
        {
            await using var scope = provider.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var result = await users.CreateAsync(new ApplicationUser
            {
                UserName = "existing@example.test",
                Email = "existing@example.test",
                EmailConfirmed = true
            }, "Existing-pass!2026");
            Assert.True(result.Succeeded);
        }

        public ValueTask DisposeAsync() => provider.DisposeAsync();
    }

    private sealed class TestConsole : IFirstPastorBootstrapConsole
    {
        public Queue<string> Lines { get; } = new(["acceptance-pastor@example.test", "acceptance-member@example.test"]);
        public Queue<string> Passwords { get; } = new([
            "Pastor-pass!2026", "Pastor-pass!2026", "Member-pass!2026", "Member-pass!2026"]);
        public List<bool> PasswordVisibilityModes { get; } = [];
        public string Output { get; private set; } = string.Empty;
        public bool IsInputRedirected => false;
        public void Write(string value) => Output += value;
        public void WriteLine(string value = "") => Output += value + Environment.NewLine;
        public string? ReadLine() => Lines.Dequeue();
        public string ReadPassword(bool showPassword)
        {
            PasswordVisibilityModes.Add(showPassword);
            return Passwords.Dequeue();
        }
    }

    private sealed class RecordingGuard(string? failure = null) : IAcceptanceTestAccountSeedDatabaseGuard
    {
        public AcceptanceTestAccountSeedRequest? Request { get; private set; }

        public Task<IFirstPastorBootstrapTransaction> AcquireVerifiedTransactionAsync(
            ApplicationDbContext db,
            AcceptanceTestAccountSeedRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return failure is null
                ? Task.FromResult<IFirstPastorBootstrapTransaction>(new TestTransaction())
                : Task.FromException<IFirstPastorBootstrapTransaction>(new InvalidOperationException(failure));
        }
    }

    private sealed class TestTransaction : IFirstPastorBootstrapTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "JMHConnect.Api.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
