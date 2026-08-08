using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace ERP.AI.Knowledge.Storage;

public class LocalDocumentStorage : IDocumentStorage
{
    private readonly string _baseStoragePath;

    public LocalDocumentStorage(IConfiguration configuration)
    {
        var configuredPath = configuration["Knowledge:StoragePath"];
        _baseStoragePath = !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "knowledge", "documents");

        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    public async Task<StoredDocumentFile> SaveAsync(string documentId, string originalFileName, Stream contentStream, CancellationToken cancellationToken = default)
    {
        var safeDocumentId = Path.GetFileName(documentId);
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var safeOriginalName = Path.GetFileNameWithoutExtension(originalFileName);
        var storedFileName = $"{safeDocumentId}{extension}";

        var documentFolder = Path.Combine(_baseStoragePath, safeDocumentId);
        var originalFolder = Path.Combine(documentFolder, "original");

        if (!Directory.Exists(originalFolder))
        {
            Directory.CreateDirectory(originalFolder);
        }

        var fullPath = Path.Combine(originalFolder, storedFileName);

        contentStream.Position = 0;
        using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await contentStream.CopyToAsync(fileStream, cancellationToken);
        await fileStream.FlushAsync(cancellationToken);

        contentStream.Position = 0;
        var fileHash = await CalculateHashAsync(contentStream, cancellationToken);
        var fileSize = new FileInfo(fullPath).Length;

        var relativePath = Path.Combine(safeDocumentId, "original", storedFileName);

        return new StoredDocumentFile(storedFileName, relativePath, fullPath, fileSize, fileHash);
    }

    public Task<Stream> OpenReadAsync(string documentId, string storedFileName, CancellationToken cancellationToken = default)
    {
        var safeDocumentId = Path.GetFileName(documentId);
        var safeFileName = Path.GetFileName(storedFileName);
        var fullPath = Path.Combine(_baseStoragePath, safeDocumentId, "original", safeFileName);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Stored document file '{safeFileName}' not found.");
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var safeDocumentId = Path.GetFileName(documentId);
        var documentFolder = Path.Combine(_baseStoragePath, safeDocumentId);

        if (Directory.Exists(documentFolder))
        {
            Directory.Delete(documentFolder, recursive: true);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string documentId, string storedFileName, CancellationToken cancellationToken = default)
    {
        var safeDocumentId = Path.GetFileName(documentId);
        var safeFileName = Path.GetFileName(storedFileName);
        var fullPath = Path.Combine(_baseStoragePath, safeDocumentId, "original", safeFileName);

        return Task.FromResult(File.Exists(fullPath));
    }

    public async Task<string> CalculateHashAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var originalPosition = stream.CanSeek ? stream.Position : 0;
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();

        if (stream.CanSeek)
        {
            stream.Position = originalPosition;
        }

        return hashString;
    }
}
