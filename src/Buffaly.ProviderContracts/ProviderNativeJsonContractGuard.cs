using System.Text.Json;

namespace Buffaly.ProviderContracts;

// Classifies provider-native model text so JSON-shaped contract drift is returned to the core retry loop instead of rendered as prose.
public static class ProviderNativeJsonContractGuard
{
	public static bool ShouldReturnRawForCoreContractRetry(string? text)
	{
		string candidate = NormalizeJsonCandidate(text);
		if (string.IsNullOrWhiteSpace(candidate))
			return false;

		if (TryDecodeJsonString(candidate, out string decoded))
			candidate = decoded.Trim();

		if (!IsJsonLike(candidate))
			return false;

		if (!TryParseJson(candidate, out JsonDocument? document) || document == null)
			return true;
		using (document)
		{
			if (document.RootElement.ValueKind != JsonValueKind.Object)
				return true;
			return !document.RootElement.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array;
		}
	}

	public static string NormalizeJsonCandidate(string? text)
	{
		string candidate = (text ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(candidate))
			return string.Empty;

		if (TryStripClosedFence(candidate, out string fenced))
			return fenced.Trim();

		return candidate;
	}

	private static bool TryStripClosedFence(string text, out string body)
	{
		body = string.Empty;
		string trimmed = (text ?? string.Empty).Trim();
		if (!trimmed.StartsWith("```", StringComparison.Ordinal) || !trimmed.EndsWith("```", StringComparison.Ordinal))
			return false;

		int firstLineEnd = trimmed.IndexOf('\n');
		if (firstLineEnd < 0)
			return false;

		body = trimmed.Substring(firstLineEnd + 1, trimmed.Length - firstLineEnd - 4).Trim();
		return !string.IsNullOrWhiteSpace(body);
	}

	private static bool IsJsonLike(string text)
	{
		string trimmed = (text ?? string.Empty).TrimStart();
		return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
	}

	private static bool TryDecodeJsonString(string candidate, out string decoded)
	{
		decoded = string.Empty;
		string trimmed = (candidate ?? string.Empty).Trim();
		if (!trimmed.StartsWith("\"", StringComparison.Ordinal) || !trimmed.EndsWith("\"", StringComparison.Ordinal))
			return false;

		try
		{
			decoded = JsonSerializer.Deserialize<string>(trimmed)?.Trim() ?? string.Empty;
			return !string.IsNullOrWhiteSpace(decoded);
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private static bool TryParseJson(string candidate, out JsonDocument? document)
	{
		document = null;
		try
		{
			document = JsonDocument.Parse(candidate.Trim());
			return true;
		}
		catch (JsonException)
		{
			return false;
		}
	}
}
