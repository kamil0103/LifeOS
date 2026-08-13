using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Services;
using LifeOS.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LifeOS.Infrastructure.Services;

public class GoogleDocsService : IGoogleDocsService
{
    private readonly ILogger<GoogleDocsService> _logger;

    public GoogleDocsService(ILogger<GoogleDocsService> logger)
    {
        _logger = logger;
    }

    private DocsService CreateClient(string serviceAccountJson)
    {
        var credential = GoogleCredential.FromJson(serviceAccountJson)
            .CreateScoped(DocsService.Scope.Documents);

        return new DocsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "LifeOS"
        });
    }

    public async Task<string> TestConnectionAsync(string serviceAccountJson, string documentId, CancellationToken ct = default)
    {
        var service = CreateClient(serviceAccountJson);
        var doc = await service.Documents.Get(documentId).ExecuteAsync(ct);
        return doc.Title ?? "(untitled)";
    }

    public async Task SyncJournalAsync(string serviceAccountJson, string documentId, string content, CancellationToken ct = default)
    {
        var service = CreateClient(serviceAccountJson);

        // Read current doc to find content length
        var doc = await service.Documents.Get(documentId).ExecuteAsync(ct);
        var endIndex = doc.Body?.Content?
            .Where(c => c.EndIndex.HasValue)
            .Select(c => c.EndIndex!.Value)
            .DefaultIfEmpty(1)
            .Max() ?? 1;

        var requests = new List<Request>();

        // Delete existing content (keep the structural newline at the end)
        if (endIndex > 2)
        {
            requests.Add(new Request
            {
                DeleteContentRange = new DeleteContentRangeRequest
                {
                    Range = new Google.Apis.Docs.v1.Data.Range
                    {
                        StartIndex = 1,
                        EndIndex = endIndex - 1
                    }
                }
            });
        }

        // Insert new content
        requests.Add(new Request
        {
            InsertText = new InsertTextRequest
            {
                Location = new Location { Index = 1 },
                Text = content
            }
        });

        await service.Documents.BatchUpdate(new BatchUpdateDocumentRequest
        {
            Requests = requests
        }, documentId).ExecuteAsync(ct);

        _logger.LogInformation("Synced journal to Google Doc {DocumentId}", documentId);
    }
}
