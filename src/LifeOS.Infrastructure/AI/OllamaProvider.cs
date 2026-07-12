using System.Text;
using System.Text.Json;
using LifeOS.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LifeOS.Infrastructure.AI;

public class OllamaProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<OllamaProvider> _logger;

    public OllamaProvider(HttpClient httpClient, IConfiguration config, ILogger<OllamaProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public bool IsAvailable
    {
        get
        {
            try
            {
                var baseUrl = _config["Ai:OllamaBaseUrl"] ?? "http://localhost:11434";
                var response = _httpClient.GetAsync($"{baseUrl}/api/tags").Result;
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var baseUrl = _config["Ai:OllamaBaseUrl"] ?? "http://localhost:11434";
        var model = _config["Ai:OllamaModel"] ?? "llama3.2";
        var url = $"{baseUrl}/api/generate";

        var requestBody = new
        {
            model,
            prompt = $"{systemPrompt}\n\n{userPrompt}",
            stream = false,
            options = new { temperature = 0.7 }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("Calling Ollama API with model {Model}...", model);
        var response = await _httpClient.PostAsync(url, content, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Ollama API error: {Status} - {Body}", response.StatusCode, responseJson);
            throw new InvalidOperationException($"Ollama API error: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseJson);
        var text = doc.RootElement.GetProperty("response").GetString();

        return text ?? string.Empty;
    }

    public async Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var enhancedPrompt = userPrompt + "\n\nRespond ONLY with valid JSON. Do not include markdown formatting, backticks, or explanatory text.";
        var result = await CompleteAsync(systemPrompt, enhancedPrompt, ct);
        
        result = result.Trim();
        if (result.StartsWith("```json")) result = result[7..];
        if (result.StartsWith("```")) result = result[3..];
        if (result.EndsWith("```")) result = result[..^3];
        
        return result.Trim();
    }
}
