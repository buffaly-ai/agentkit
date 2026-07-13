
namespace Ontology
{
	public class TemporaryPrototypes
	{
		// ── async-local holders ────────────────────────────────────────────────────
		private static readonly AsyncLocal<ObjectCache> _cacheLocal = new();
		private static readonly AsyncLocal<ObjectCache> _listCacheLocal = new();
		private static readonly AsyncLocal<List<Prototype>> _listLocal = new();   // NEW

		// ── caches ────────────────────────────────────────────────────────────────
		public static ObjectCache Cache =>
			_cacheLocal.Value
				??= ObjectCacheManager.Instance.GetOrCreateCache(nameof(TemporaryPrototypes));

		public static ObjectCache ListCache =>
			_listCacheLocal.Value
				??= ObjectCacheManager.Instance.GetOrCreateCache(nameof(TemporaryPrototypes) + ".IDList");

		// ── list keyed by prototype-ID ─────────────────────────────────────────────
		public static List<Prototype> ListById
		{
			get
			{
				// fast path — already cached for this async context
				if (_listLocal.Value is { } list) return list;

				const string ENTRY = "List";
				list = ListCache.Get<List<Prototype>>(ENTRY);
				if (list == null)
				{
					list = new List<Prototype>();
					ListCache.Insert(list, ENTRY);
				}

				_listLocal.Value = list;       // cache for subsequent accesses
				return list;
			}
		}

		// ── utility ───────────────────────────────────────────────────────────────
		// Internal on purpose: cache resets must flow through Initializer.ResetCache()
		// so prototype singleton slots and lexeme caches are reset together.
		internal static void ResetCache()
		{
			// clear only if the slot already has a cache
			ObjectCacheManager.Instance.GetOrCreateCache(nameof(TemporaryPrototypes)).Clear();
			ObjectCacheManager.Instance.GetOrCreateCache(nameof(TemporaryPrototypes) + ".IDList").Clear();

			_cacheLocal.Value = null;
			_listCacheLocal.Value = null;
			_listLocal.Value = null;
		}


		// Internal on purpose: session reloads should flow through Initializer.ReloadCache()
		// so prototype singleton slots are invalidated before cache handles are rebound.
		internal static void ReloadCache()
		{
			//This does not clear the cache, but loads the local copy from the 
			//cache managers again. This is useful after we load a session
			_cacheLocal.Value = null;
			_listCacheLocal.Value = null;
			_listLocal.Value = null;
		}


		public static int GetCount()
		{
			return Cache.Count;
		}

		public static List<Prototype> GetAllTemporaryPrototypes()
		{
			return Cache.GetAll<Prototype>().ToList();
		}


		public static Prototype GetOrCreateTemporaryPrototype(string strPrototypeName)
		{
			Prototype? prototype = Cache.Get<Prototype>(strPrototypeName);

			if (null == prototype)
			{
				prototype = new Prototype();
				prototype.PrototypeName = strPrototypeName;
				InsertPrototype(prototype);
			}

			return prototype;
		}

		private static void IndexById(Prototype proto)
		{
			// temp IDs are assigned as -1, -2, -3, …
			int idx = Math.Abs(proto.PrototypeID);
			var list = ListById;
			if (idx >= list.Count)
			{
				// grow with null placeholders
				for (int i = list.Count; i <= idx; i++)
					list.Add(null);
			}
			list[idx] = proto;
		}


		public static int InsertPrototype(Prototype prototype)
		{
			int next = ListById.Count + 1;
			prototype.PrototypeID = -next;
			prototype.Value = 1;

			Cache.Insert(prototype, prototype.PrototypeName);
			IndexById(prototype);
			WireNestedPrototype(prototype);

			return prototype.PrototypeID;
		}

		public static Prototype GetOrInsertPrototype(Prototype prototype)
		{
			Prototype? protoExisting = Cache.Get<Prototype>(prototype.PrototypeName);

			if (null == protoExisting)
			{
				InsertPrototype(prototype);
				protoExisting = prototype as Prototype;
			}

			return protoExisting;
		}
		public static Prototype GetOrCreateTemporaryPrototype(string strPrototypeName, Prototype parent)
		{
			Prototype? prototype = Cache.Get<Prototype>(strPrototypeName);

			if (null == prototype)
			{
				prototype = TemporaryPrototypes.GetOrCreateTemporaryPrototype(strPrototypeName);
				prototype.InsertTypeOf(parent);
			}

			return prototype;
		}

		public static Prototype? GetTemporaryPrototypeOrNull(string strPrototypeName)
		{
			Prototype? prototype = Cache.Get<Prototype>(strPrototypeName);
			return prototype;
		}

		public static Prototype ? GetTemporaryPrototypeOrNull(int iPrototypeID)
		{
			int iLookupID = Math.Abs(iPrototypeID);
			if (iLookupID >= ListById.Count)
				return null; // no prototype with this ID exists

			// we use the absolute value of the ID, because temporary prototypes have negative IDs
			Prototype prototype = ListById[iLookupID];
			return prototype;
		}

		public static Prototype GetTemporaryPrototype(string strPrototypeName)
		{
			Prototype? prototype = GetTemporaryPrototypeOrNull(strPrototypeName);

			if (null == prototype)
				throw new Exception("Prototype does not exist: " + strPrototypeName);

			return prototype;
		}

		public static Prototype GetTemporaryPrototype(int iPrototypeID)
		{
			Prototype ? prototype = GetTemporaryPrototypeOrNull(iPrototypeID);

			if (null == prototype)
				throw new Exception("Prototype does not exist: " + iPrototypeID);

			return prototype;
		}

		private static void WireNestedPrototype(Prototype prototype)
		{
			if (prototype == null)
				throw new ArgumentNullException(nameof(prototype));
			if (prototype.PrototypeID == 0)
				return;
			if (string.IsNullOrWhiteSpace(prototype.PrototypeName))
				return;

			prototype.RemoveNestedParent();

			Prototype? directNestedParent = GetDirectNestedParentOrNull(prototype.PrototypeName);
			if (directNestedParent != null)
				prototype.InsertNestedParent(directNestedParent.PrototypeID);

			foreach (Prototype candidate in GetAllTemporaryPrototypes())
			{
				if (candidate.PrototypeID == prototype.PrototypeID)
					continue;
				if (!HasDirectNestedParentPrototypeName(candidate.PrototypeName, prototype.PrototypeName))
					continue;

				candidate.InsertNestedParent(prototype.PrototypeID);
			}
		}

		private static Prototype? GetDirectNestedParentOrNull(string prototypeName)
		{
			if (string.IsNullOrWhiteSpace(prototypeName))
				return null;
			if (prototypeName.Contains('#'))
				return null;

			int lastDotIndex = prototypeName.LastIndexOf('.');
			if (lastDotIndex <= 0)
				return null;

			string directParentPrototypeName = prototypeName.Substring(0, lastDotIndex);
			return GetTemporaryPrototypeOrNull(directParentPrototypeName);
		}

		private static bool HasDirectNestedParentPrototypeName(string prototypeName, string directParentPrototypeName)
		{
			if (string.IsNullOrWhiteSpace(prototypeName)
				|| string.IsNullOrWhiteSpace(directParentPrototypeName))
			{
				return false;
			}
			if (prototypeName.Contains('#'))
				return false;
			if (!prototypeName.StartsWith(directParentPrototypeName + ".", StringComparison.Ordinal))
				return false;

			int childLeafStartIndex = directParentPrototypeName.Length + 1;
			return prototypeName.IndexOf('.', childLeafStartIndex) < 0;
		}
	}
}

