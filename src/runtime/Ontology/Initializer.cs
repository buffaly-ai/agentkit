
namespace Ontology
{
	public class Initializer
	{
		static public void Initialize()
		{
			CacheManager.UseAsyncLocal = true;
			ObjectCacheManager.UseAsyncLocal = true;

			Logs.DebugLog.MaxLinesPerFile = 0;
		}

		static public void ResetCache()
		{
			// Canonical global reset entrypoint.
			ResettablePrototypeAsyncLocal.ResetAll();
			TemporaryLexemes.Cache.Clear();
			TemporaryLexemes.m_mapRelatedParentPrototypes.Clear();
			TemporaryPrototypes.ResetCache();
		}

		static public void ReloadCache()
		{
			// Canonical session-switch entrypoint.
			// Reload prototype caches and invalidate cached singleton prototype handles.
			ResettablePrototypeAsyncLocal.ResetAll();
			TemporaryPrototypes.ReloadCache();
		}
	}
}

