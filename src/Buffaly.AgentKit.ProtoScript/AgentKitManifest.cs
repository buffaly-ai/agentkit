using System.Text.Json;
using System.Text.Json.Serialization;

namespace Buffaly.AgentKit.ProtoScript;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AgentKitManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("projectFile")]
    public string ProjectFile { get; init; } = string.Empty;

    [JsonPropertyName("exports")]
    public List<AgentKitManifestExport> Exports { get; init; } = [];

    public static async Task<AgentKitManifest> LoadAsync(string manifestFile, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = File.OpenRead(manifestFile);
        AgentKitManifest manifest = await JsonSerializer.DeserializeAsync<AgentKitManifest>(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Manifest is empty.");
        manifest.Validate();
        return manifest;
    }

    public void Validate()
    {
        if (SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported manifest schemaVersion '{SchemaVersion}'. Expected 1.");
        if (string.IsNullOrWhiteSpace(ProjectFile))
            throw new InvalidDataException("Manifest projectFile is required.");
        if (Exports.Count == 0)
            throw new InvalidDataException("Manifest must explicitly allow at least one export.");

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (AgentKitManifestExport export in Exports)
        {
            if (string.IsNullOrWhiteSpace(export.Name))
                throw new InvalidDataException("Export name is required.");
            if (!names.Add(export.Name))
                throw new InvalidDataException($"Duplicate projected tool name '{export.Name}'.");
            if (string.IsNullOrWhiteSpace(export.Method))
                throw new InvalidDataException($"Export '{export.Name}' method is required.");
        }
    }

    public string ResolveProjectFile(string manifestFile)
    {
        string baseDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestFile)) ?? Environment.CurrentDirectory;
        if (Path.IsPathRooted(ProjectFile))
            throw new InvalidDataException("Manifest projectFile must be relative to the manifest file.");
        string path = Path.GetFullPath(Path.Combine(baseDirectory, ProjectFile));
        string allowedRoot = baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Manifest projectFile must not escape the manifest directory.");
        if (!File.Exists(path))
            throw new FileNotFoundException("Manifest projectFile was not found.", path);
        return path;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AgentKitManifestExport
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("prototype")]
    public string? Prototype { get; init; }

    [JsonPropertyName("parameterDescriptions")]
    public Dictionary<string, string> ParameterDescriptions { get; init; } = new(StringComparer.Ordinal);
}
