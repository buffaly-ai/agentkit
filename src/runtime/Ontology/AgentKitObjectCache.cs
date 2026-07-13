namespace Ontology;

public sealed class ObjectCache
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    public int Count => _values.Count;

    public T? Get<T>(string key)
    {
        if (_values.TryGetValue(key, out object? value) && value is T typed)
            return typed;
        return default;
    }

    public void Insert(object? value, string key)
    {
        _values[key] = value;
    }

    public IEnumerable<T> GetAll<T>()
    {
        foreach (object? value in _values.Values)
        {
            if (value is T typed)
                yield return typed;
        }
    }

    public void Clear() => _values.Clear();
}

public sealed class ObjectCacheManager
{
    private static readonly ObjectCacheManager _instance = new();
    private readonly AsyncLocal<Dictionary<string, ObjectCache>?> _asyncLocalCaches = new();
    private readonly Dictionary<string, ObjectCache> _globalCaches = new(StringComparer.Ordinal);

    public static ObjectCacheManager Instance => _instance;

    public static bool UseAsyncLocal { get; set; } = true;

    public ObjectCache GetOrCreateCache(string name)
    {
        Dictionary<string, ObjectCache> caches;
        if (UseAsyncLocal)
        {
            caches = _asyncLocalCaches.Value ??= new Dictionary<string, ObjectCache>(StringComparer.Ordinal);
        }
        else
        {
            caches = _globalCaches;
        }

        if (!caches.TryGetValue(name, out ObjectCache? cache))
        {
            cache = new ObjectCache();
            caches[name] = cache;
        }

        return cache;
    }
}

public static class CacheManager
{
    public static bool UseAsyncLocal { get; set; } = true;
}
