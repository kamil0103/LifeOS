using System.Text.Json;
using System.Text.RegularExpressions;
using LifeOS.Application.DTOs.Portfolio;
using LifeOS.Application.Interfaces;
using LifeOS.Application.Services;
namespace LifeOS.Infrastructure.Professional;
public class GitHubRepositoryClient(IHttpClientFactory clients) : IGitHubRepositoryClient
{
    private async Task<JsonDocument> FetchAsync(string path, CancellationToken ct)
    {
        using var client = clients.CreateClient("PortfolioGitHub");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/" + path);
        request.Headers.UserAgent.ParseAdd("LifeOS-Portfolio-Importer");
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("GitHub could not return this repository or account. Check the name, visibility or API rate limit and try again.");
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is JsonException || ex is TaskCanceledException && !ct.IsCancellationRequested)
        { throw new InvalidOperationException("GitHub is temporarily unavailable. Try again."); }
    }
    public async Task<List<GitHubRepoInfoDto>> ListAsync(string username, CancellationToken ct)
    {
        username = username.Trim(); if (!Regex.IsMatch(username, "^[A-Za-z0-9][A-Za-z0-9-]{0,38}$")) throw new InvalidOperationException("Enter a valid GitHub username.");
        using var json = await FetchAsync($"users/{username}/repos?sort=updated&per_page=100", ct);
        return json.RootElement.EnumerateArray().Select(Map).OrderByDescending(r => r.UpdatedAt).ToList();
    }
    public async Task<GitHubRepoInfoDto> GetAsync(string repoUrl, CancellationToken ct)
    {
        var canonical = ProfessionalRecordRules.GitHub(repoUrl);
        using var json = await FetchAsync("repos/" + canonical["https://github.com/".Length..], ct); return Map(json.RootElement);
    }
    private static GitHubRepoInfoDto Map(JsonElement e) => new() { Name = e.GetProperty("name").GetString() ?? "", Description = e.TryGetProperty("description", out var d) ? d.GetString() : null,
        Language = e.TryGetProperty("language", out var l) ? l.GetString() : null, Stars = e.TryGetProperty("stargazers_count", out var s) ? s.GetInt32() : 0,
        HtmlUrl = e.GetProperty("html_url").GetString(), UpdatedAt = e.TryGetProperty("updated_at", out var u) && u.TryGetDateTimeOffset(out var date) ? date : DateTimeOffset.UtcNow };
}
