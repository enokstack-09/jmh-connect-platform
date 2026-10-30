namespace JMHConnect.Api.Services;

public sealed class LocalProfilePhotoStorage(IConfiguration configuration, IWebHostEnvironment environment)
    : IProfilePhotoStorage
{
    private readonly string rootPath = Path.GetFullPath(
        configuration["MemberPhotos:StoragePath"]
        ?? Path.Combine(environment.ContentRootPath, "App_Data", "member-photos"));

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        if (extension is not (".jpg" or ".png" or ".webp"))
            throw new InvalidOperationException("Unsupported profile photo extension.");
        Directory.CreateDirectory(rootPath);
        var storageKey = $"{Guid.NewGuid():N}{extension}";
        var path = Resolve(storageKey);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await content.CopyToAsync(output, cancellationToken);
        return storageKey;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storageKey);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || Path.GetFileName(storageKey) != storageKey)
            throw new InvalidOperationException("Invalid profile photo storage key.");

        var path = Path.GetFullPath(Path.Combine(rootPath, storageKey));
        if (!path.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Profile photo path escaped its storage root.");
        return path;
    }
}
