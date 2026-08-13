namespace LifeOS.Application.Interfaces;

public interface IGoogleDocsService
{
    /// <summary>True when the global service account key is present on the server.</summary>
    bool IsConfigured { get; }

    /// <summary>The service account email users must share their Google Doc with.</summary>
    string? ServiceAccountEmail { get; }

    /// <summary>
    /// Validates the global credentials and doc access by reading the document title.
    /// </summary>
    Task<string> TestConnectionAsync(string documentId, CancellationToken ct = default);

    /// <summary>
    /// Replaces the entire document body with the given journal content.
    /// </summary>
    Task SyncJournalAsync(string documentId, string content, CancellationToken ct = default);
}
