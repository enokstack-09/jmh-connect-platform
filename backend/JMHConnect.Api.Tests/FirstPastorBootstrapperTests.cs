using JMHConnect.Api.Bootstrap;
using JMHConnect.Api.Data;
using JMHConnect.Api.Models;
using JMHConnect.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace JMHConnect.Api.Tests;

public sealed class FirstPastorBootstrapperTests
{
    [Fact]
    public async Task RejectsBootstrapOutsideDevelopment()
    {
        await using var fixture = await BootstrapFixture.CreateAsync(Environments.Production);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync());

        Assert.Contains("Development", error.Message, StringComparison.Ordinal);
        Assert.Empty(await fixture.UsersAsync());
        Assert.Empty(await fixture.RolesAsync());
    }

    [Theory]
    [InlineData("Server=remote.example;Database=JMHConnectDev;Integrated Security=True;Encrypt=True")]
    [InlineData("Server=localhost;Database=JMHConnect;Integrated Security=True;Encrypt=True")]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Database=JMHConnectDev;Integrated Security=True;Encrypt=True")]
    public void RejectsRemoteAndWrongDatabaseConnectionStrings(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDatabase"] = connectionString
            })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(() =>
            FirstPastorBootstrapConfiguration.Validate(
                new TestHostEnvironment(Environments.Development),
                configuration,
                BootstrapRequest()));

        Assert.Contains("localhost/JMHConnectDev", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsWhenAnyUserAlreadyExists()
    {
        await using var fixture = await BootstrapFixture.CreateAsync();
        await fixture.CreateUserAsync("existing@example.test", "Existing-user!2026");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync());

        Assert.Contains("contains no users", error.Message, StringComparison.Ordinal);
        Assert.Single(await fixture.UsersAsync());
        Assert.Empty(await fixture.RolesAsync());
    }

    [Fact]
    public async Task RejectsUnauthorizedRoleWithoutChangingRoles()
    {
        await using var fixture = await BootstrapFixture.CreateAsync();
        await fixture.CreateRoleAsync("Unexpected");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync());

        Assert.Contains("unrecognized role", error.Message, StringComparison.Ordinal);
        Assert.Equal(["Unexpected"], await fixture.RolesAsync());
        Assert.Empty(await fixture.UsersAsync());
    }

    [Fact]
    public async Task RejectsWhenMigrationGuardReportsPendingMigrations()
    {
        await using var fixture = await BootstrapFixture.CreateAsync(guard: new ThrowingGuard("The Identity database has pending migrations."));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync());

        Assert.Contains("pending migrations", error.Message, StringComparison.Ordinal);
        Assert.Empty(await fixture.UsersAsync());
        Assert.Empty(await fixture.RolesAsync());
    }

    [Fact]
    public async Task RejectsInvalidPasswordBeforeChangingRolesOrUsers()
    {
        await using var fixture = await BootstrapFixture.CreateAsync();
        fixture.Console.Passwords.Clear();
        fixture.Console.Passwords.Enqueue("short");
        fixture.Console.Passwords.Enqueue("short");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync());

        Assert.Contains("password", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await fixture.UsersAsync());
        Assert.Empty(await fixture.RolesAsync());
        Assert.Equal(0, fixture.Guard.AcquireCount);
    }

    [Fact]
    public async Task ConcurrentBootstrapAttemptsCreateOnlyOnePastor()
    {
        var guard = new SerializingGuard();
        await using var fixture = await BootstrapFixture.CreateAsync(guard: guard);
        var firstConsole = new TestConsole();
        var secondConsole = new TestConsole();

        var first = FirstPastorBootstrapper.RunAsync(fixture.Services, BootstrapRequest(), firstConsole);
        var second = FirstPastorBootstrapper.RunAsync(fixture.Services, BootstrapRequest(), secondConsole);
        var results = await Task.WhenAll(
            first.ContinueWith(task => task.Exception?.GetBaseException()),
            second.ContinueWith(task => task.Exception?.GetBaseException()));

        Assert.Single(results, result => result is null);
        Assert.Single(results, result => result is InvalidOperationException);
        Assert.Single(await fixture.UsersAsync());
        Assert.Equal(ApplicationRoles.All.OrderBy(role => role), await fixture.RolesAsync());
        Assert.Equal([ApplicationRoles.Pastor], await fixture.UserRolesAsync());
    }

    [Fact]
    public async Task CreatesEnabledConfirmedPastorWithOnlyPastorRole()
    {
        await using var fixture = await BootstrapFixture.CreateAsync();

        await fixture.RunAsync();

        var user = Assert.Single(await fixture.UsersAsync());
        Assert.True(user.IsEnabled);
        Assert.True(user.EmailConfirmed);
        Assert.Equal([ApplicationRoles.Pastor], await fixture.UserRolesAsync());
        Assert.Equal(ApplicationRoles.All.OrderBy(role => role), await fixture.RolesAsync());
    }

    [Fact]
    public async Task KeepsPasswordInputHiddenByDefault()
    {
        await using var fixture = await BootstrapFixture.CreateAsync();

        await fixture.RunAsync();

        Assert.Equal([false, false], fixture.Console.PasswordVisibilityModes);
    }

    [Fact]
    public async Task AllowsAnExplicitVisiblePasswordChoiceForBothPasswordPrompts()
    {
        await using var fixture = await BootstrapFixture.CreateAsync();
        fixture.Console.Lines.Clear();
        fixture.Console.Lines.Enqueue("pastor@example.test");
        fixture.Console.Lines.Enqueue("SHOW");

        await fixture.RunAsync();

        Assert.Equal([true, true], fixture.Console.PasswordVisibilityModes);
    }

    [Fact]
    public void RequiresExactBootstrapArguments()
    {
        Assert.NotNull(FirstPastorBootstrapRequest.Parse(
            ["--bootstrap-first-pastor", "--database", "JMHConnectDev"]));
        Assert.Throws<InvalidOperationException>(() =>
            FirstPastorBootstrapRequest.Parse(["--bootstrap-first-pastor"]));
        Assert.Throws<InvalidOperationException>(() =>
            FirstPastorBootstrapRequest.Parse(["--database", "JMHConnectDev"]));
        Assert.Throws<InvalidOperationException>(() =>
            FirstPastorBootstrapRequest.Parse(["--bootstrap-first-pastor", "--database", "JMHConnect"]));
        Assert.Throws<InvalidOperationException>(() =>
            FirstPastorBootstrapRequest.Parse(["--bootstrap-first-pastor", "--database", "JMHConnectDev", "--urls", "http://localhost:5080"]));
        Assert.Throws<InvalidOperationException>(() =>
            FirstPastorBootstrapRequest.Parse(["--database", "JMHConnectDev", "--bootstrap-first-pastor"]));
        Assert.Throws<InvalidOperationException>(() =>
            FirstPastorBootstrapRequest.Parse(["--bootstrap-first-pastor", "--bootstrap-first-pastor", "--database", "JMHConnectDev"]));
    }

    private static FirstPastorBootstrapRequest BootstrapRequest() =>
        FirstPastorBootstrapRequest.Parse(["--bootstrap-first-pastor", "--database", "JMHConnectDev"])!;

    private sealed class BootstrapFixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly string databaseName;

        private BootstrapFixture(ServiceProvider provider, string databaseName, TestConsole console, TestGuard guard)
        {
            this.provider = provider;
            this.databaseName = databaseName;
            Console = console;
            Guard = guard;
        }

        public IServiceProvider Services => provider;
        public TestConsole Console { get; }
        public TestGuard Guard { get; }

        public static async Task<BootstrapFixture> CreateAsync(
            string environment = "Development",
            IFirstPastorBootstrapDatabaseGuard? guard = null)
        {
            var databaseName = $"JmhBootstrapTests-{Guid.NewGuid():N}";
            var effectiveGuard = guard as TestGuard ?? new TestGuard();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environment));
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:IdentityDatabase"] = "Server=localhost;Database=JMHConnectDev;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
                })
                .Build());
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
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
            services.AddSingleton<IFirstPastorBootstrapDatabaseGuard>(effectiveGuard);

            var provider = services.BuildServiceProvider(validateScopes: true);
            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
            return new BootstrapFixture(provider, databaseName, new TestConsole(), effectiveGuard);
        }

        public Task RunAsync() => FirstPastorBootstrapper.RunAsync(Services, BootstrapRequest(), Console);

        public async Task CreateUserAsync(string email, string password)
        {
            using var scope = provider.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var result = await manager.CreateAsync(new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true }, password);
            Assert.True(result.Succeeded);
        }

        public async Task CreateRoleAsync(string name)
        {
            using var scope = provider.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var result = await manager.CreateAsync(new IdentityRole(name));
            Assert.True(result.Succeeded);
        }

        public async Task<List<ApplicationUser>> UsersAsync()
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.ToListAsync();
        }

        public async Task<List<string>> RolesAsync()
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Roles
                .Select(role => role.Name!)
                .OrderBy(name => name)
                .ToListAsync();
        }

        public async Task<List<string>> UserRolesAsync()
        {
            using var scope = provider.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await manager.Users.SingleAsync();
            return (await manager.GetRolesAsync(user)).OrderBy(role => role).ToList();
        }

        public ValueTask DisposeAsync() => provider.DisposeAsync();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "JMHConnect.Api.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class TestConsole : IFirstPastorBootstrapConsole
    {
        public Queue<string> Lines { get; } = new(["pastor@example.test", string.Empty]);
        public Queue<string> Passwords { get; } = new(["Bootstrap-pass!2026", "Bootstrap-pass!2026"]);
        public List<bool> PasswordVisibilityModes { get; } = [];
        public bool IsInputRedirected => false;
        public void Write(string value) { }
        public void WriteLine(string value = "") { }
        public string? ReadLine() => Lines.Dequeue();
        public string ReadPassword(bool showPassword)
        {
            PasswordVisibilityModes.Add(showPassword);
            return Passwords.Dequeue();
        }
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

    private sealed class ThrowingGuard(string message) : TestGuard
    {
        public override Task<IFirstPastorBootstrapTransaction> AcquireVerifiedTransactionAsync(
            ApplicationDbContext db,
            string expectedDatabaseName,
            CancellationToken cancellationToken) =>
            Task.FromException<IFirstPastorBootstrapTransaction>(new InvalidOperationException(message));
    }

    private sealed class SerializingGuard : TestGuard
    {
        private readonly SemaphoreSlim semaphore = new(1, 1);

        public override async Task<IFirstPastorBootstrapTransaction> AcquireVerifiedTransactionAsync(
            ApplicationDbContext db,
            string expectedDatabaseName,
            CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken);
            return new ReleasingTransaction(semaphore);
        }
    }

    private sealed class TestTransaction : IFirstPastorBootstrapTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ReleasingTransaction(SemaphoreSlim semaphore) : IFirstPastorBootstrapTransaction
    {
        private bool released;
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync()
        {
            if (!released)
            {
                semaphore.Release();
                released = true;
            }

            return ValueTask.CompletedTask;
        }
    }
}
