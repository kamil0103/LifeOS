namespace LifeOS.Application.Interfaces;

public interface IAiProvider
{
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
    Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
    bool IsAvailable { get; }
}
