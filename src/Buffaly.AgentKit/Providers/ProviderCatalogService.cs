using Buffaly.ProviderContracts;

namespace Buffaly.AgentKit.Providers;

/// <summary>Builds and validates the provider-driven model catalog without session state.</summary>
public sealed class ProviderCatalogService
{
    private const string CatalogVersion = "provider-contracts-v1";
    private readonly ProviderRegistry _registry;
    private readonly IReadOnlyDictionary<string, string> _settings;

    public ProviderCatalogService(ProviderRegistry registry, IReadOnlyDictionary<string, string>? settings = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _settings = settings ?? new Dictionary<string, string>();
    }

    public async Task<ProviderCatalogContract> GetProviderCatalogAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ProviderCatalogSourceResult>();
        foreach (IBuffalyProviderCatalogSource source in _registry.CatalogSources)
        {
            try
            {
                results.Add(await source.BuildCatalogAsync(new BuffalyProviderCatalogContext { Settings = _settings }, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception exception)
            {
                results.Add(new ProviderCatalogSourceResult { Error = "Provider '" + source.Provider + "' unavailable: " + exception.Message });
            }
        }

        var catalog = new ProviderCatalogContract { CatalogVersion = CatalogVersion };
        foreach (ProviderCatalogSourceResult result in results)
        {
            if (result.ProviderItem is not null)
                catalog.Providers.Add(result.ProviderItem);
            catalog.ReasoningLevelOptions.AddRange(result.ReasoningLevelOptions);
        }
        catalog.Error = string.Join(" | ", results.Select(result => result.Error).Where(error => !string.IsNullOrEmpty(error)));
        catalog.ReasoningLevelOptions = catalog.ReasoningLevelOptions.GroupBy(option => option.Value, StringComparer.Ordinal).Select(group => group.First()).ToList();
        if (catalog.Providers.Count == 0)
            throw new InvalidOperationException("Provider catalog does not contain any provider rows.");

        catalog.DefaultSelection = ValidateAndResolveSelection(catalog, new ProviderSelectionContract());
        return catalog;
    }

    public async Task<ProviderSelectionContract> ResolveValidatedSelectionAsync(ProviderSelectionContract selection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ProviderCatalogContract catalog = await GetProviderCatalogAsync(cancellationToken).ConfigureAwait(false);
        return ValidateAndResolveSelection(catalog, selection);
    }

    internal static ProviderSelectionContract ValidateAndResolveSelection(ProviderCatalogContract catalog, ProviderSelectionContract selection)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(selection);
        ProviderCatalogItemContract provider = ResolveProvider(catalog, selection.Provider);
        ProviderTransportContract transport = ResolveTransport(provider, selection.Transport);
        ProviderModelContract model = ResolveModel(provider, transport, selection.ModelName);
        string reasoningLevel = ResolveReasoning(model, selection.ReasoningLevel);
        return new ProviderSelectionContract { Provider = provider.Provider, Transport = transport.Transport, ModelName = model.ModelName, ReasoningLevel = reasoningLevel };
    }

    private static ProviderCatalogItemContract ResolveProvider(ProviderCatalogContract catalog, string requestedProvider)
    {
        ProviderCatalogItemContract? provider = string.IsNullOrEmpty(requestedProvider)
            ? catalog.Providers.FirstOrDefault(row => row.IsEnabled)
            : catalog.Providers.FirstOrDefault(row => string.Equals(row.Provider, requestedProvider, StringComparison.Ordinal));
        if (provider is null)
            throw new InvalidOperationException(string.IsNullOrEmpty(requestedProvider) ? "Provider catalog does not contain any enabled providers." : "Requested provider was not found in provider catalog: " + requestedProvider);
        if (!provider.IsEnabled)
            throw new InvalidOperationException("Requested provider is disabled: " + provider.Provider);
        return provider;
    }

    private static ProviderTransportContract ResolveTransport(ProviderCatalogItemContract provider, string requestedTransport)
    {
        if (provider.Transports.Count == 0)
            throw new InvalidOperationException("Provider does not expose any transports: " + provider.Provider);
        string transportName = string.IsNullOrEmpty(requestedTransport) ? provider.DefaultTransport : requestedTransport;
        ProviderTransportContract? transport = provider.Transports.FirstOrDefault(row => string.Equals(row.Transport, transportName, StringComparison.Ordinal));
        if (transport is null)
            throw new InvalidOperationException("Requested transport was not found for provider: " + provider.Provider + "/" + transportName);
        if (!transport.IsEnabled)
            throw new InvalidOperationException("Requested transport is disabled for provider: " + provider.Provider);
        return transport;
    }

    private static ProviderModelContract ResolveModel(ProviderCatalogItemContract provider, ProviderTransportContract transport, string requestedModel)
    {
        List<ProviderModelContract> models = provider.Models.Where(row => string.Equals(row.Transport, transport.Transport, StringComparison.Ordinal)).ToList();
        if (models.Count == 0)
            throw new InvalidOperationException("No models are available for provider transport: " + provider.Provider + "/" + transport.Transport);
        string modelName = string.IsNullOrEmpty(requestedModel) ? provider.DefaultModelName : requestedModel;
        ProviderModelContract? model = models.FirstOrDefault(row => string.Equals(row.ModelName, modelName, StringComparison.Ordinal));
        model ??= string.IsNullOrEmpty(modelName) ? models.FirstOrDefault(row => row.IsDefault) : null;
        if (model is null)
            throw new InvalidOperationException("Requested model was not found for provider transport: " + provider.Provider + "/" + transport.Transport + "/" + modelName);
        return model;
    }

    private static string ResolveReasoning(ProviderModelContract model, string? requestedReasoning)
    {
        if (model.SupportedReasoningLevels.Count == 0 && string.IsNullOrEmpty(model.DefaultReasoningLevel))
            return string.Empty;
        string reasoning = string.IsNullOrEmpty(requestedReasoning) ? model.DefaultReasoningLevel : requestedReasoning;
        if (!model.SupportedReasoningLevels.Contains(reasoning, StringComparer.Ordinal))
            throw new InvalidOperationException("Reasoning level '" + reasoning + "' is not supported by model '" + model.ModelName + "'.");
        return reasoning;
    }
}
