namespace LifeOS.Application.Interfaces;

public interface IGoogleDocsService
{
    /// <summary>
    /// Validates the service account credentials and doc access by reading the document title.
    /// </summary>
    Task<string> TestConnectionAsync(string serviceAccountJson, string documentId, CancellationToken ct = default);

    /// <summary>
    /// Replaces the entire document body with the given journal content.
    /// </summary>
    Task SyncJournalAsync(string serviceAccountJson, string documentId, string content, CancellationToken ct = default);
}
