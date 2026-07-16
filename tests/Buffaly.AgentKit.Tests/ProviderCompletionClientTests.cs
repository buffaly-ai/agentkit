using Buffaly.AgentKit.Providers;
using Buffaly.ProviderContracts;
using Xunit;

namespace Buffaly.AgentKit.Tests;

public sealed class ProviderCompletionClientTests
{
    [Fact]
    public async Task CompleteAsync_PreservesTheCallerRequestAndExecutesExactlyOnce()
    {
        var executor = new CapturingExecutor();
        var registry = new ProviderRegistry();
        registry.AddCatalogSource(new TestCatalogSource());
        registry.AddCompletionExecutor(executor);
        var client = new ProviderCompletionClient(registry, new ProviderCatalogService(registry));
        var request = new BuffalyCompletionRequest
        {
            Provider = "test",
            ModelName = "medqa-model",
            ReasoningLevel = "medium",
            Messages = new[] { new BuffalyChatMessage { Role = "user", Content = "Question only" } },
            Tools = Array.Empty<BuffalyToolDefinition>()
        };

        BuffalyCompletionResult result = await client.CompleteAsync(request);

        Assert.True(result.Success);
        Assert.Same(request, executor.Request);
        Assert.Equal(1, executor.CallCount);
        Assert.Single(executor.Request!.Messages);
        Assert.Equal("user", executor.Request.Messages[0].Role);
        Assert.Empty(executor.Request.Tools);
    }

    [Fact]
    public async Task CompleteAsync_RejectsUnknownModelBeforeProviderExecution()
    {
        var executor = new CapturingExecutor();
        var registry = new ProviderRegistry();
        registry.AddCatalogSource(new TestCatalogSource());
        registry.AddCompletionExecutor(executor);
        var client = new ProviderCompletionClient(registry, new ProviderCatalogService(registry));

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.CompleteAsync(new BuffalyCompletionRequest
        {
            Provider = "test",
            ModelName = "unknown",
            ReasoningLevel = "medium"
        }));

        Assert.Contains("Requested model was not found", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, executor.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_RequiresExplicitProviderAndModel()
    {
        var executor = new CapturingExecutor();
        var registry = new ProviderRegistry();
        registry.AddCatalogSource(new TestCatalogSource());
        registry.AddCompletionExecutor(executor);
        var client = new ProviderCompletionClient(registry, new ProviderCatalogService(registry));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.CompleteAsync(new BuffalyCompletionRequest { ModelName = "medqa-model" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.CompleteAsync(new BuffalyCompletionRequest { Provider = "test" }));

        Assert.Equal(0, executor.CallCount);
    }

    [Fact]
    public void Registry_RejectsDuplicateProviderExecutors()
    {
        var registry = new ProviderRegistry();
        registry.AddCompletionExecutor(new CapturingExecutor());

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => registry.AddCompletionExecutor(new CapturingExecutor()));

        Assert.Contains("already registered", error.Message, StringComparison.Ordinal);
    }

    private sealed class CapturingExecutor : IBuffalyCompletionExecutor
    {
        public string Provider => "test";
        public int CallCount { get; private set; }
        public BuffalyCompletionRequest? Request { get; private set; }

        public Task<BuffalyCompletionResult> CompleteAsync(BuffalyCompletionRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            Request = request;
            return Task.FromResult(new BuffalyCompletionResult { Success = true, Text = "A" });
        }
    }

    private sealed class TestCatalogSource : IBuffalyProviderCatalogSource
    {
        public string Provider => "test";

        public Task<ProviderCatalogSourceResult> BuildCatalogAsync(BuffalyProviderCatalogContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ProviderCatalogSourceResult
            {
                ProviderItem = new ProviderCatalogItemContract
                {
                    Provider = "test",
                    DisplayName = "Test",
                    IsConfigured = true,
                    IsEnabled = true,
                    DefaultTransport = ProviderCatalogDefaults.ProviderNativeTransport,
                    DefaultModelName = "medqa-model",
                    Transports = new List<ProviderTransportContract>
                    {
                        new() { Provider = "test", Transport = ProviderCatalogDefaults.ProviderNativeTransport, DisplayName = ProviderCatalogDefaults.ProviderNativeDisplayName, IsDefault = true, IsEnabled = true }
                    },
                    Models = new List<ProviderModelContract>
                    {
                        new() { Provider = "test", Transport = ProviderCatalogDefaults.ProviderNativeTransport, ModelName = "medqa-model", DisplayName = "MedQA Model", IsDefault = true, SupportedInApi = true, DefaultReasoningLevel = "medium", SupportedReasoningLevels = new List<string> { "medium" } }
                    }
                },
                ReasoningLevelOptions = new List<ProviderReasoningLevelOptionContract> { new() { Value = "medium", Label = "Medium" } }
            });
        }
    }
}
