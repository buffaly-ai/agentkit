using System.Text.Json;

namespace Buffaly.AgentKit.SampleSupport;

public sealed class JsonFixtureStore(string rootDirectory)
{
    private readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public string RootDirectory { get; } = Path.GetFullPath(rootDirectory);

    public async Task<T> LoadAsync<T>(string relativePath, CancellationToken cancellationToken = default)
    {
        string path = GetBoundedPath(relativePath);
        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException($"Fixture '{relativePath}' was empty or invalid.");
    }

    public async Task SaveAsync<T>(string relativePath, T value, CancellationToken cancellationToken = default)
    {
        string path = GetBoundedPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken).ConfigureAwait(false);
    }

    public string GetBoundedPath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) throw new InvalidOperationException("Fixture paths must be relative.");
        string full = Path.GetFullPath(Path.Combine(RootDirectory, relativePath));
        if (!full.StartsWith(RootDirectory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path '{relativePath}' escapes fixture root '{RootDirectory}'.");
        return full;
    }
}
