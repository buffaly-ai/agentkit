using Buffaly.ProviderContracts;

namespace Buffaly.AgentKit.Providers;

/// <summary>Executes one explicit request through one registered provider executor.</summary>
public sealed class ProviderCompletionClient
{
    private readonly ProviderRegistry _registry;
    private readonly ProviderCatalogService _catalog;

    public ProviderCompletionClient(ProviderRegistry registry, ProviderCatalogService catalog)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public Task<BuffalyCompletionResult> AskModelAsync(
        string prompt,
        string systemPrompt,
        string provider,
        string model,
        string reasoningLevel,
        IReadOnlyDictionary<string, string>? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(systemPrompt);
        var messages = new List<BuffalyChatMessage>();
        if (systemPrompt.Length > 0)
            messages.Add(new BuffalyChatMessage { Role = "system", Content = systemPrompt });
        messages.Add(new BuffalyChatMessage { Role = "user", Content = prompt });
        return CompleteAsync(new BuffalyCompletionRequest
        {
            Provider = provider,
            ModelName = model,
            ReasoningLevel = reasoningLevel,
            Messages = messages,
            Tools = Array.Empty<BuffalyToolDefinition>(),
            Options = options ?? new Dictionary<string, string>()
        }, cancellationToken);
    }

    public async Task<BuffalyCompletionResult> CompleteAsync(BuffalyCompletionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Provider))
            throw new InvalidOperationException("Completion request Provider is required.");
        if (string.IsNullOrWhiteSpace(request.ModelName))
            throw new InvalidOperationException("Completion request ModelName is required.");
        var requestedSelection = new ProviderSelectionContract
        {
            Provider = request.Provider,
            Transport = ProviderCatalogDefaults.ProviderNativeTransport,
            ModelName = request.ModelName,
            ReasoningLevel = request.ReasoningLevel
        };
        ProviderSelectionContract selection = await _catalog.ResolveValidatedSelectionAsync(requestedSelection, cancellationToken).ConfigureAwait(false);
        IBuffalyCompletionExecutor executor = _registry.GetRequiredCompletionExecutor(selection.Provider);
        return await executor.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
