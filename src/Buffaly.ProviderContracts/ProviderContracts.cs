using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Buffaly.ProviderContracts;

public interface IBuffalyProviderModule
{
	void Register(IBuffalyProviderRegistry registry);
}

public interface IBuffalyProviderRegistry
{
	void AddCatalogSource(IBuffalyProviderCatalogSource source);
	void AddCompletionExecutor(IBuffalyCompletionExecutor executor);
	void AddTextToSpeechCatalogSource(IBuffalyTextToSpeechCatalogSource source);
	void AddTextToSpeechExecutor(IBuffalyTextToSpeechExecutor executor);
	void AddEmbeddingCatalogSource(IBuffalyEmbeddingCatalogSource source) { }
	void AddEmbeddingExecutor(IBuffalyEmbeddingExecutor executor) { }
}

public interface IBuffalyProviderCatalogSource
{
	string Provider { get; }
	Task<ProviderCatalogSourceResult> BuildCatalogAsync(BuffalyProviderCatalogContext context, CancellationToken cancellationToken);
}

public interface IBuffalyCompletionExecutor
{
	string Provider { get; }
	Task<BuffalyCompletionResult> CompleteAsync(BuffalyCompletionRequest request, CancellationToken cancellationToken);
}

public sealed class BuffalyProviderCatalogContext
{
	public string SessionKey { get; init; } = string.Empty;
	public string ClientVersion { get; init; } = string.Empty;
	public IReadOnlyDictionary<string, string> Settings { get; init; } = new Dictionary<string, string>();
}

public static class ProviderCatalogDefaults
{
	public const string ProviderNativeTransport = "provider_native";
	public const string ProviderNativeDisplayName = "Provider Native";

	public static string ToReasoningLabel(string reasoningLevel)
	{
		string normalized = (reasoningLevel ?? string.Empty).Trim();
		if (string.Equals(normalized, "low", StringComparison.Ordinal))
			return "Low";
		if (string.Equals(normalized, "medium", StringComparison.Ordinal))
			return "Medium";
		if (string.Equals(normalized, "high", StringComparison.Ordinal))
			return "High";
		return normalized;
	}
}

public sealed class ProviderModelSettings
{
	[JsonPropertyName("SupportedReasoningLevels")]
	public List<string> SupportedReasoningLevels { get; set; } = new List<string>();

	[JsonPropertyName("DefaultReasoningLevel")]
	public string DefaultReasoningLevel { get; set; } = string.Empty;
}

public sealed class ProviderCatalogSourceResult
{
	public static ProviderCatalogSourceResult Empty() => new ProviderCatalogSourceResult();

	public ProviderCatalogItemContract? ProviderItem { get; set; }
	public List<ProviderReasoningLevelOptionContract> ReasoningLevelOptions { get; set; } = new List<ProviderReasoningLevelOptionContract>();
	public string Error { get; set; } = string.Empty;
}

public sealed class ProviderCatalogContract
{
	[JsonPropertyName("CatalogVersion")]
	public string CatalogVersion { get; set; } = string.Empty;

	[JsonPropertyName("Providers")]
	public List<ProviderCatalogItemContract> Providers { get; set; } = new List<ProviderCatalogItemContract>();

	[JsonPropertyName("DefaultSelection")]
	public ProviderSelectionContract DefaultSelection { get; set; } = new ProviderSelectionContract();

	[JsonPropertyName("ReasoningLevelOptions")]
	public List<ProviderReasoningLevelOptionContract> ReasoningLevelOptions { get; set; } = new List<ProviderReasoningLevelOptionContract>();

	[JsonPropertyName("Error")]
	public string Error { get; set; } = string.Empty;
}

public sealed class ProviderCatalogItemContract
{
	[JsonPropertyName("Provider")]
	public string Provider { get; set; } = string.Empty;

	[JsonPropertyName("DisplayName")]
	public string DisplayName { get; set; } = string.Empty;

	[JsonPropertyName("IsConfigured")]
	public bool IsConfigured { get; set; }

	[JsonPropertyName("IsEnabled")]
	public bool IsEnabled { get; set; }

	[JsonPropertyName("DefaultTransport")]
	public string DefaultTransport { get; set; } = string.Empty;

	[JsonPropertyName("DefaultModelName")]
	public string DefaultModelName { get; set; } = string.Empty;

	[JsonPropertyName("Transports")]
	public List<ProviderTransportContract> Transports { get; set; } = new List<ProviderTransportContract>();

	[JsonPropertyName("Models")]
	public List<ProviderModelContract> Models { get; set; } = new List<ProviderModelContract>();
}

public sealed class ProviderTransportContract
{
	[JsonPropertyName("Provider")]
	public string Provider { get; set; } = string.Empty;

	[JsonPropertyName("Transport")]
	public string Transport { get; set; } = string.Empty;

	[JsonPropertyName("DisplayName")]
	public string DisplayName { get; set; } = string.Empty;

	[JsonPropertyName("IsDefault")]
	public bool IsDefault { get; set; }

	[JsonPropertyName("IsEnabled")]
	public bool IsEnabled { get; set; }
}

public sealed class ProviderModelContract
{
	[JsonPropertyName("Provider")]
	public string Provider { get; set; } = string.Empty;

	[JsonPropertyName("Transport")]
	public string Transport { get; set; } = string.Empty;

	[JsonPropertyName("ModelName")]
	public string ModelName { get; set; } = string.Empty;

	[JsonPropertyName("DisplayName")]
	public string DisplayName { get; set; } = string.Empty;

	[JsonPropertyName("Visibility")]
	public string Visibility { get; set; } = string.Empty;

	[JsonPropertyName("SupportedInApi")]
	public bool SupportedInApi { get; set; }

	[JsonPropertyName("DefaultReasoningLevel")]
	public string DefaultReasoningLevel { get; set; } = string.Empty;

	[JsonPropertyName("SupportedReasoningLevels")]
	public List<string> SupportedReasoningLevels { get; set; } = new List<string>();

	[JsonPropertyName("IsDefault")]
	public bool IsDefault { get; set; }
}

public sealed class ProviderReasoningLevelOptionContract
{
	[JsonPropertyName("Value")]
	public string Value { get; set; } = string.Empty;

	[JsonPropertyName("Label")]
	public string Label { get; set; } = string.Empty;
}

public sealed class ProviderSelectionContract
{
	[JsonPropertyName("Provider")]
	public string Provider { get; set; } = string.Empty;

	[JsonPropertyName("Transport")]
	public string Transport { get; set; } = string.Empty;

	[JsonPropertyName("ModelName")]
	public string ModelName { get; set; } = string.Empty;

	[JsonPropertyName("ReasoningLevel")]
	public string? ReasoningLevel { get; set; }
}

public sealed class SetProviderSelectionRequestContract
{
	[JsonPropertyName("SessionKey")]
	public string SessionKey { get; set; } = string.Empty;

	[JsonPropertyName("Selection")]
	public ProviderSelectionContract Selection { get; set; } = new ProviderSelectionContract();
}

public sealed class ProviderSelectionResultContract
{
	[JsonPropertyName("Selection")]
	public ProviderSelectionContract Selection { get; set; } = new ProviderSelectionContract();

	[JsonPropertyName("CatalogVersion")]
	public string CatalogVersion { get; set; } = string.Empty;
}

public interface IBuffalyEmbeddingCatalogSource
{
	string Provider { get; }
	Task<EmbeddingCatalogSourceResult> BuildEmbeddingCatalogAsync(BuffalyProviderCatalogContext context, CancellationToken cancellationToken);
}

public interface IBuffalyEmbeddingExecutor
{
	string Provider { get; }
	string Transport { get; }
	Task<BuffalyEmbeddingResult> EmbedAsync(BuffalyEmbeddingRequest request, CancellationToken cancellationToken);
}

public sealed class EmbeddingCatalogSourceResult
{
	public static EmbeddingCatalogSourceResult Empty() => new EmbeddingCatalogSourceResult();
	public EmbeddingProviderCatalogItem? ProviderItem { get; set; }
	public string Error { get; set; } = string.Empty;
}

public sealed class EmbeddingProviderCatalogItem
{
	public string Provider { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public bool IsConfigured { get; set; }
	public bool IsEnabled { get; set; }
	public string DefaultTransport { get; set; } = string.Empty;
	public string DefaultModelName { get; set; } = string.Empty;
	public string DefaultBaseUrl { get; set; } = string.Empty;
	public List<EmbeddingTransportContract> Transports { get; set; } = new List<EmbeddingTransportContract>();
	public List<EmbeddingModelContract> Models { get; set; } = new List<EmbeddingModelContract>();
}

public sealed class EmbeddingTransportContract
{
	public string Provider { get; set; } = string.Empty;
	public string Transport { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public bool IsDefault { get; set; }
	public bool IsEnabled { get; set; }
}

public sealed class EmbeddingModelContract
{
	public string Provider { get; set; } = string.Empty;
	public string Transport { get; set; } = string.Empty;
	public string ModelName { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public int LogicalDimensions { get; set; }
	public int StoredDimensions { get; set; }
	public string DistanceMetric { get; set; } = "cosine";
	public string PaddingStrategy { get; set; } = string.Empty;
	public bool IsDefault { get; set; }
	public bool SupportedInApi { get; set; }
}

public sealed class BuffalyEmbeddingRequest
{
	public string Provider { get; init; } = string.Empty;
	public string Transport { get; init; } = string.Empty;
	public string ModelName { get; init; } = string.Empty;
	public string Input { get; init; } = string.Empty;
	public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>();
}

public sealed class BuffalyEmbeddingResult
{
	public bool Success { get; init; }
	public float[] Vector { get; init; } = Array.Empty<float>();
	public int LogicalDimensions { get; init; }
	public int TotalTokens { get; init; }
	public string ProviderRequestId { get; init; } = string.Empty;
	public string Raw { get; init; } = string.Empty;
	public string ErrorCode { get; init; } = string.Empty;
	public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class BuffalyCompletionRequest
{
	public string Provider { get; init; } = string.Empty;
	public string ModelName { get; init; } = string.Empty;
	public string ReasoningLevel { get; init; } = string.Empty;
	public IReadOnlyList<BuffalyChatMessage> Messages { get; init; } = Array.Empty<BuffalyChatMessage>();
	public IReadOnlyList<BuffalyToolDefinition> Tools { get; init; } = Array.Empty<BuffalyToolDefinition>();
	public string ResponseFormat { get; init; } = string.Empty;
	public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>();
}

public sealed class BuffalyChatMessage
{
	public string Role { get; init; } = string.Empty;
	public string Content { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public string ToolCallId { get; init; } = string.Empty;
	public string ToolName { get; init; } = string.Empty;
	public string ToolArguments { get; init; } = string.Empty;
	public IReadOnlyList<BuffalyMessageContentPart> ContentParts { get; init; } = Array.Empty<BuffalyMessageContentPart>();
}

public sealed class BuffalyMessageContentPart
{
	public string Type { get; init; } = string.Empty;
	public string Text { get; init; } = string.Empty;
	public string ImageUrl { get; init; } = string.Empty;
	public string VideoUrl { get; init; } = string.Empty;
	public string MimeType { get; init; } = string.Empty;
}

public sealed class BuffalyToolDefinition
{
	public string Name { get; init; } = string.Empty;
	public string MethodName { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public string ReturnType { get; init; } = string.Empty;
	public IReadOnlyList<BuffalyToolParameterDefinition> Parameters { get; init; } = Array.Empty<BuffalyToolParameterDefinition>();
	public string JsonSchema { get; init; } = string.Empty;
}

public sealed class BuffalyToolParameterDefinition
{
	public string Name { get; init; } = string.Empty;
	public string Type { get; init; } = string.Empty;
	public bool Required { get; init; }
	public string Description { get; init; } = string.Empty;
}

public sealed class BuffalyCompletionResult
{
	public bool Success { get; init; }
	public string Text { get; init; } = string.Empty;
	public string Raw { get; init; } = string.Empty;
	public string ErrorCode { get; init; } = string.Empty;
	public string ErrorMessage { get; init; } = string.Empty;
	public string ProviderRequestId { get; init; } = string.Empty;
	public string ProviderResponseId { get; init; } = string.Empty;
	public string RawUsageJson { get; init; } = string.Empty;
	public IReadOnlyList<BuffalyUsageMetric> UsageMetrics { get; init; } = Array.Empty<BuffalyUsageMetric>();
	public IReadOnlyList<BuffalyToolCall> ToolCalls { get; init; } = Array.Empty<BuffalyToolCall>();
}

public sealed class BuffalyToolCall
{
	public string Id { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public string ArgumentsJson { get; init; } = string.Empty;
}

public sealed class BuffalyUsageMetric
{
	public string Dimension { get; init; } = string.Empty;
	public double Value { get; init; }
}


public interface IBuffalyTextToSpeechCatalogSource
{
	string Provider { get; }
	Task<TextToSpeechCatalogSourceResult> BuildTextToSpeechCatalogAsync(BuffalyProviderCatalogContext context, CancellationToken cancellationToken);
}

public interface IBuffalyTextToSpeechExecutor
{
	string Provider { get; }
	Task<BuffalyTextToSpeechResult> GenerateSpeechAsync(BuffalyTextToSpeechRequest request, CancellationToken cancellationToken);
}

public sealed class TextToSpeechCatalogSourceResult
{
	public static TextToSpeechCatalogSourceResult Empty() => new TextToSpeechCatalogSourceResult();
	public TextToSpeechProviderCatalogItem? ProviderItem { get; set; }
	public string Error { get; set; } = string.Empty;
}

public sealed class TextToSpeechProviderCatalogItem
{
	public string Provider { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public bool IsConfigured { get; set; }
	public bool IsEnabled { get; set; }
	public string DefaultModelName { get; set; } = string.Empty;
	public string DefaultVoiceName { get; set; } = string.Empty;
	public List<TextToSpeechModelContract> Models { get; set; } = new List<TextToSpeechModelContract>();
}

public sealed class TextToSpeechModelContract
{
	public string Provider { get; set; } = string.Empty;
	public string ModelName { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public bool IsDefault { get; set; }
	public bool SupportedInApi { get; set; }
	public bool SupportsInstructions { get; set; }
	public List<string> SupportedOutputFormats { get; set; } = new List<string>();
	public List<TextToSpeechVoiceContract> Voices { get; set; } = new List<TextToSpeechVoiceContract>();
}

public sealed class TextToSpeechVoiceContract
{
	public string VoiceName { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public bool IsDefault { get; set; }
}

public sealed class BuffalyTextToSpeechRequest
{
	public string Provider { get; init; } = string.Empty;
	public string ModelName { get; init; } = string.Empty;
	public string VoiceName { get; init; } = string.Empty;
	public string Text { get; init; } = string.Empty;
	public string Instructions { get; init; } = string.Empty;
	public string OutputFormat { get; init; } = "mp3";
	public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>();
}

public sealed class BuffalyTextToSpeechResult
{
	public bool Success { get; init; }
	public byte[] AudioBytes { get; init; } = Array.Empty<byte>();
	public string MimeType { get; init; } = string.Empty;
	public string OutputFormat { get; init; } = string.Empty;
	public string Raw { get; init; } = string.Empty;
	public string ErrorCode { get; init; } = string.Empty;
	public string ErrorMessage { get; init; } = string.Empty;
	public IReadOnlyList<BuffalyUsageMetric> UsageMetrics { get; init; } = Array.Empty<BuffalyUsageMetric>();
}
