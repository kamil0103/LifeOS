using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Services;
using LifeOS.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LifeOS.Infrastructure.Services;

public class GoogleDocsService : IGoogleDocsService
{
    private readonly IConfiguration _config;
    private readonly ILogger<GoogleDocsService> _logger;

    public GoogleDocsService(IConfiguration config, ILogger<GoogleDocsService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>True when a global service account key file is present on the server.</summary>
    public bool IsConfigured => TryLoadJson(out _);

    /// <summary>The service account email (shown to users so they can share their doc with it).</summary>
    public string? ServiceAccountEmail
    {
        get
        {
            if (!TryLoadJson(out var json)) return null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("client_email", out var email) ? email.GetString() : null;
            }
            catch { return null; }
        }
    }

    private string GetKeyPath() =>
        _config["Google:ServiceAccountPath"] ?? "/app/data/secrets/google-service-account.json";

    private bool TryLoadJson(out string json)
    {
        json = string.Empty;
        try
        {
            var path = GetKeyPath();
            if (!File.Exists(path)) return false;
            json = File.ReadAllText(path);
            return !string.IsNullOrWhiteSpace(json);
        }
        catch
        {
            return false;
        }
    }

    private DocsService CreateClient()
    {
        if (!TryLoadJson(out var json))
            throw new InvalidOperationException($"Google service account key not found on the server (expected at {GetKeyPath()}).");

        var credential = GoogleCredential.FromJson(json)
            .CreateScoped(DocsService.Scope.Documents);

        return new DocsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "LifeOS"
        });
    }

    public async Task<string> TestConnectionAsync(string documentId, CancellationToken ct = default)
    {
        var service = CreateClient();
        var doc = await service.Documents.Get(documentId).ExecuteAsync(ct);
        return doc.Title ?? "(untitled)";
    }

    public async Task SyncContentAsync(string documentId, string content, CancellationToken ct = default, string section = "journal")
    {
        var service = CreateClient();
        var doc = await service.Documents.Get(documentId).ExecuteAsync(ct);
        var runs = doc.Body?.Content?
            .Where(c => c.Paragraph != null)
            .SelectMany(c => c.Paragraph.Elements ?? new List<ParagraphElement>())
            .Where(e => e.TextRun?.Content != null && e.StartIndex.HasValue)
            .Select(e => (Index: e.StartIndex!.Value, Text: e.TextRun!.Content!))
            .ToList() ?? new();
        var endIndex = doc.Body?.Content?.Where(c => c.EndIndex.HasValue)
            .Select(c => c.EndIndex!.Value).DefaultIfEmpty(1).Max() ?? 1;
        var region = LifeOS.Application.Services.ManagedExport.Find(runs, section, Math.Max(1, endIndex - 1));
        var requests = new List<Request>();
        if (region.End > region.Start)
            requests.Add(new Request { DeleteContentRange = new DeleteContentRangeRequest
            { Range = new Google.Apis.Docs.v1.Data.Range { StartIndex = region.Start, EndIndex = region.End } } });
        requests.Add(new Request { InsertText = new InsertTextRequest
        { Location = new Location { Index = region.Start }, Text = LifeOS.Application.Services.ManagedExport.Wrap(content, section) } });
        await service.Documents.BatchUpdate(new BatchUpdateDocumentRequest
        {
            Requests = requests,
            WriteControl = new WriteControl { RequiredRevisionId = doc.RevisionId }
        }, documentId).ExecuteAsync(ct);
        _logger.LogInformation("Synced LifeOS {Section} export", section);
    }
}
