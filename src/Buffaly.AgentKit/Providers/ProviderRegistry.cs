using Buffaly.ProviderContracts;

namespace Buffaly.AgentKit.Providers;

/// <summary>Holds the provider components registered by Buffaly provider modules.</summary>
public sealed class ProviderRegistry : IBuffalyProviderRegistry
{
    private readonly Dictionary<string, IBuffalyProviderCatalogSource> _catalogSources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IBuffalyCompletionExecutor> _completionExecutors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IBuffalyTextToSpeechCatalogSource> _textToSpeechCatalogSources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IBuffalyTextToSpeechExecutor> _textToSpeechExecutors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IBuffalyEmbeddingCatalogSource> _embeddingCatalogSources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IBuffalyEmbeddingExecutor> _embeddingExecutors = new(StringComparer.Ordinal);

    public IReadOnlyList<IBuffalyProviderCatalogSource> CatalogSources => _catalogSources.Values.ToArray();

    public void AddCatalogSource(IBuffalyProviderCatalogSource source) => Add(_catalogSources, source, source?.Provider, nameof(source));
    public void AddCompletionExecutor(IBuffalyCompletionExecutor executor) => Add(_completionExecutors, executor, executor?.Provider, nameof(executor));
    public void AddTextToSpeechCatalogSource(IBuffalyTextToSpeechCatalogSource source) => Add(_textToSpeechCatalogSources, source, source?.Provider, nameof(source));
    public void AddTextToSpeechExecutor(IBuffalyTextToSpeechExecutor executor) => Add(_textToSpeechExecutors, executor, executor?.Provider, nameof(executor));
    public void AddEmbeddingCatalogSource(IBuffalyEmbeddingCatalogSource source) => Add(_embeddingCatalogSources, source, source?.Provider, nameof(source));
    public void AddEmbeddingExecutor(IBuffalyEmbeddingExecutor executor) => Add(_embeddingExecutors, executor, executor?.Provider, nameof(executor));

    public IBuffalyCompletionExecutor GetRequiredCompletionExecutor(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required.", nameof(provider));
        if (!_completionExecutors.TryGetValue(provider, out IBuffalyCompletionExecutor? executor))
            throw new InvalidOperationException("No completion executor is registered for provider: " + provider);
        return executor;
    }

    private static void Add<T>(Dictionary<string, T> components, T? component, string? provider, string parameterName) where T : class
    {
        ArgumentNullException.ThrowIfNull(component, parameterName);
        if (string.IsNullOrWhiteSpace(provider))
            throw new InvalidOperationException("Provider component Provider is required.");
        if (!components.TryAdd(provider, component))
            throw new InvalidOperationException("A provider component is already registered for provider: " + provider);
    }
}
