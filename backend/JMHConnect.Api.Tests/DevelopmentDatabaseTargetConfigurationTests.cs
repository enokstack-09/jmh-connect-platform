using JMHConnect.Api.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace JMHConnect.Api.Tests;

public sealed class DevelopmentDatabaseTargetConfigurationTests
{
    [Fact]
    public void AcceptsExactLocalWindowsAuthenticationTarget()
    {
        var configuration = Configuration("JMHConnectAcceptance_20261030_01");
        DevelopmentDatabaseTargetConfiguration.Validate(
            new TargetHostEnvironment(Environments.Development), configuration,
            "Server=localhost;Database=JMHConnectAcceptance_20261030_01;Integrated Security=True;Encrypt=True");
    }

    [Theory]
    [InlineData("Development", "Server=localhost;Database=JMHConnectDev;Integrated Security=True;Encrypt=True")]
    [InlineData("Development", "Server=remote.example;Database=JMHConnectAcceptance_20261030_01;Integrated Security=True;Encrypt=True")]
    [InlineData("Development", "Server=localhost;Database=JMHConnectAcceptance_20261030_01;User ID=sa;Password=test;Encrypt=True")]
    [InlineData("Production", "Server=localhost;Database=JMHConnectAcceptance_20261030_01;Integrated Security=True;Encrypt=True")]
    public void RejectsWrongRuntimeTarget(string environment, string connectionString)
    {
        Assert.Throws<InvalidOperationException>(() => DevelopmentDatabaseTargetConfiguration.Validate(
            new TargetHostEnvironment(environment), Configuration("JMHConnectAcceptance_20261030_01"), connectionString));
    }

    private static IConfiguration Configuration(string expectedDatabase) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [DevelopmentDatabaseTargetConfiguration.ExpectedDatabaseKey] = expectedDatabase
        }).Build();

    private sealed class TargetHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "JMHConnect.Api.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
