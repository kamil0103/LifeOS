using System.Text;
using System.Text.Json;
using LifeOS.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LifeOS.Infrastructure.AI;

public class GeminiProvider : IAiProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<GeminiProvider> _logger;

    public GeminiProvider(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<GeminiProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public bool IsAvailable => !string.IsNullOrEmpty(_config["Ai:GeminiApiKey"]);

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var apiKey = _config["Ai:GeminiApiKey"]!;
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

        var requestBody = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = $"{systemPrompt}\n\n{userPrompt}" } } }
            },
            generationConfig = new { temperature = 0.2, maxOutputTokens = 4096 }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("Calling Gemini API...");
        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsync(url, content, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini API error: {Status} - {Body}", response.StatusCode, responseJson);
            throw new InvalidOperationException($"Gemini API error: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseJson);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        return text ?? string.Empty;
    }

    public async Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var apiKey = _config["Ai:GeminiApiKey"]!;
        var model = _config["Ai:GeminiModel"] ?? "gemini-2.5-flash";
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        // Try with JSON mode first
        var requestBody = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = $"{systemPrompt}\n\n{userPrompt}" } } }
            },
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = 8192,
                responseMimeType = "application/json"
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Calling Gemini JSON API (model={Model})...", model);
        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsync(url, content, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gemini JSON mode failed ({Status}), falling back to text mode. Body: {Body}", response.StatusCode, responseJson);
            
            // Fallback: call without JSON mode
            var enhancedPrompt = userPrompt + "\n\nRespond ONLY with valid JSON. Do not include markdown formatting, backticks, or explanatory text.";
            return await CompleteAsync(systemPrompt, enhancedPrompt, ct);
        }

        using var doc = JsonDocument.Parse(responseJson);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        var result = text ?? string.Empty;
        _logger.LogInformation("Gemini JSON response length: {Length}", result.Length);
        _logger.LogDebug("Gemini JSON raw response: {Response}", result.Substring(0, Math.Min(500, result.Length)));
        
        return result.Trim();
    }
}
