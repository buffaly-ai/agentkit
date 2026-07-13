using BasicUtilities.Collections;
using System.Text;
using System.Collections;
using BasicUtilities;

namespace Ontology
{
	public class PrototypePropertiesCollection : IEnumerable<KeyValuePair<int, Prototype?>>
	{
		private readonly Prototype m_protoParent;
		private Map<int, Prototype?> m_mapProperties;

		private Map<int, Prototype?> MapProperties =>
			m_mapProperties ?? (m_mapProperties = new Map<int, Prototype?>());

		public Prototype? this[int iPrototypeID]
		{
			get
			{
				if (m_mapProperties == null) return null;
				return m_mapProperties.TryGetValue(iPrototypeID, out var value) ? value : null;
			}
			set
			{
				MapProperties[iPrototypeID] = value;
			}
		}

		public Prototype? this[string strPrototypeName]
		{
			get
			{
				var prop = Prototypes.GetPrototypeByPrototypeName(strPrototypeName);
				return this[prop.PrototypeID];
			}
			set
			{
				var prop = Prototypes.GetPrototypeByPrototypeName(strPrototypeName);
				this[prop.PrototypeID] = value;
			}
		}

		public PrototypePropertiesCollection(Prototype parent)
		{
			m_protoParent = parent;
		}

		public PrototypePropertiesCollection(Prototype parent, Prototype clone)
		{
			m_protoParent = parent;
			var source = clone.Properties;
			if (source.m_mapProperties != null)
			{
				foreach (var pair in source.m_mapProperties)
				{
					MapProperties[pair.Key] = pair.Value?.Clone();
				}
			}
		}

		public IEnumerator<KeyValuePair<int, Prototype?>> GetEnumerator()
		{
			return (m_mapProperties ?? Enumerable.Empty<KeyValuePair<int, Prototype?>>())
				   .GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public PrototypePropertiesCollection Clone(Prototype parent)
		{
			var clone = new PrototypePropertiesCollection(parent);
			if (m_mapProperties != null)
			{
				foreach (var pair in m_mapProperties)
				{
					clone.MapProperties[pair.Key] = pair.Value?.Clone();
				}
			}
			return clone;
		}

		public bool ContainsKey(int iPrototypeID) =>
			m_mapProperties?.ContainsKey(iPrototypeID) ?? false;

		public void Remove(int iPrototypeID) =>
			m_mapProperties?.Remove(iPrototypeID);

		public int Count => m_mapProperties?.Count ?? 0;

		public void Clear() =>
			m_mapProperties?.Clear();

		public override string ToString()
		{
			var sb = new StringBuilder();
			var entries = m_mapProperties ?? Enumerable.Empty<KeyValuePair<int, Prototype?>>();
			foreach (var pair in entries)
			{
				if (sb.Length > 0) sb.Append(", ");
				var protoProp = Prototypes.GetPrototype(pair.Key);
				if (protoProp.PrototypeName.StartsWith(m_protoParent.PrototypeName))
					sb.Append(StringUtil.RightOfFirst(
								  protoProp.PrototypeName,
								  m_protoParent.PrototypeName + ".Field."))
					  .Append(" = ").Append(pair.Value);
				else
					sb.Append(protoProp.PrototypeName)
					  .Append(" = ").Append(pair.Value);
			}
			return sb.ToString();
		}

		public void Insert(Prototype protoKey, Prototype protoValue) =>
			this[protoKey.PrototypeID] = protoValue;

		public Prototype GetParent() => m_protoParent;

		public Prototype ? GetOrNull(Prototype protoKey) =>
			ContainsKey(protoKey.PrototypeID) ? this[protoKey.PrototypeID] : null;

		public void AddRange(IEnumerable<KeyValuePair<int, Prototype>> properties)
		{
			foreach (var pair in properties)
			{
				MapProperties[pair.Key] = pair.Value;
			}
		}
	}
}

