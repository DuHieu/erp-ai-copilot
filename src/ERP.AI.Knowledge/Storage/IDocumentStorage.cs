namespace ERP.AI.Knowledge.Storage;

public record StoredDocumentFile(
    string StoredFileName,
    string RelativePath,
    string FullPath,
    long FileSize,
    string FileHash
);

public interface IDocumentStorage
{
    Task<StoredDocumentFile> SaveAsync(string documentId, string originalFileName, Stream contentStream, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string documentId, string storedFileName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string documentId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string documentId, string storedFileName, CancellationToken cancellationToken = default);
    Task<string> CalculateHashAsync(Stream stream, CancellationToken cancellationToken = default);
}
