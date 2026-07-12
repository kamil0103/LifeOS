using LifeOS.Application.Interfaces;

namespace LifeOS.Infrastructure.PDF;

public class LocalDocumentStorage : IDocumentStorage
{
    private readonly string _basePath;

    public LocalDocumentStorage(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    public Task<string> SaveAsync(byte[] content, string filename, string userId, CancellationToken ct = default)
    {
        var userDir = Path.Combine(_basePath, userId);
        Directory.CreateDirectory(userDir);
        var path = Path.Combine(userDir, filename);
        File.WriteAllBytes(path, content);
        return Task.FromResult(path);
    }

    public Task<byte[]> LoadAsync(string storagePath, CancellationToken ct = default)
    {
        return Task.FromResult(File.ReadAllBytes(storagePath));
    }

    public void Delete(string storagePath)
    {
        if (File.Exists(storagePath))
            File.Delete(storagePath);
    }
}
