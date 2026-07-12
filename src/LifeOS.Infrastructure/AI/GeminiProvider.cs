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
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

        var requestBody = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = $"{systemPrompt}\n\n{userPrompt}" } } }
            },
            generationConfig = new { temperature = 0.7, maxOutputTokens = 4096 }
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
        var enhancedPrompt = userPrompt + "\n\nRespond ONLY with valid JSON. Do not include markdown formatting, backticks, or explanatory text.";
        var result = await CompleteAsync(systemPrompt, enhancedPrompt, ct);
        
        // Clean up common markdown wrappers
        result = result.Trim();
        if (result.StartsWith("```json")) result = result[7..];
        if (result.StartsWith("```")) result = result[3..];
        if (result.EndsWith("```")) result = result[..^3];
        
        return result.Trim();
    }
}
