using LifeOS.Application.Interfaces;

namespace LifeOS.Infrastructure.AI;

public class AiProviderFactory
{
    private readonly IEnumerable<IAiProvider> _providers;
    private readonly string _preferredProvider;

    public AiProviderFactory(IEnumerable<IAiProvider> providers, string preferredProvider)
    {
        _providers = providers;
        _preferredProvider = preferredProvider.ToLowerInvariant();
    }

    public IAiProvider GetProvider()
    {
        var provider = _preferredProvider switch
        {
            "gemini" => _providers.FirstOrDefault(p => p is GeminiProvider),
            "ollama" => _providers.FirstOrDefault(p => p is OllamaProvider),
            _ => null
        };

        if (provider != null && provider.IsAvailable)
            return provider;

        // Fallback to first available
        return _providers.FirstOrDefault(p => p.IsAvailable)
            ?? throw new InvalidOperationException("No AI provider is available. Configure GEMINI_API_KEY or Ollama.");
    }
}
