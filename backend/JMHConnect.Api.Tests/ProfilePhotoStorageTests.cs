using JMHConnect.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace JMHConnect.Api.Tests;

public sealed class ProfilePhotoStorageTests
{
    [Fact]
    public async Task LocalStorageUsesRandomKeysAndPreventsPathEscape()
    {
        var root = Path.Combine(Path.GetTempPath(), $"JmhConnectPhotoStorage-{Guid.NewGuid():N}");
        var photoRoot = Path.Combine(root, "private-photos");
        Directory.CreateDirectory(root);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["MemberPhotos:StoragePath"] = photoRoot })
                .Build();
            var storage = new LocalProfilePhotoStorage(configuration, new TestEnvironment(root));
            await using var source = new MemoryStream([1, 2, 3, 4]);

            var key = await storage.SaveAsync(source, ".png", CancellationToken.None);
            Assert.EndsWith(".png", key, StringComparison.Ordinal);
            Assert.DoesNotContain("..", key, StringComparison.Ordinal);
            await using (var opened = await storage.OpenReadAsync(key, CancellationToken.None))
            {
                Assert.NotNull(opened);
                using var copy = new MemoryStream();
                await opened!.CopyToAsync(copy);
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, copy.ToArray());
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => storage.OpenReadAsync("../secret.png", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveAsync(new MemoryStream([1]), ".svg", CancellationToken.None));

            await storage.DeleteIfExistsAsync(key, CancellationToken.None);
            Assert.Null(await storage.OpenReadAsync(key, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "JMHConnect.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
