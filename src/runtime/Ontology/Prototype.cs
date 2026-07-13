using BasicUtilities;
using BasicUtilities.Collections;
using System.Runtime.Serialization;

namespace Ontology
{
	public class Prototype
	{
		//Enabled to allow database free execution
		static readonly bool AllowTemporaryPrototypeToJSON = true;

		public int PrototypeID;
		public string PrototypeName;
		public double Value;

		private Prototype? m_protoParent = null;

		[IgnoreDataMemberAttribute]
		public Prototype Parent
		{
			get
			{
				return m_protoParent;
			}
		}

		private int m_iNestedParentPrototypeID = 0;

		[IgnoreDataMemberAttribute]
		public Prototype? NestedParent
		{
			get
			{
				Prototype prototype = (m_bIsMutable ? this : Prototypes.GetPrototype(this.PrototypeID));
				if (prototype.m_iNestedParentPrototypeID == 0)
					return null;

				return Prototypes.GetPrototype(prototype.m_iNestedParentPrototypeID);
			}
		}

		private PrototypePropertiesCollection? m_properties = null;
		public PrototypePropertiesCollection Properties
		{
			get
			{
				if (m_properties == null)
					m_properties = new PrototypePropertiesCollection(this);

				return m_properties;
			}

			set
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value), "Properties cannot be set to null.");

				m_properties = value;
			}
		}

		public IEnumerable<KeyValuePair<int, Prototype>> NormalProperties
		{
			get
			{
				return Properties;
			}
		}


		private List<Prototype>? m_children;
		public List<Prototype> Children
		{
			get
			{
				if (m_children == null)
					m_children = new List<Prototype>();
				return m_children;
			}
			set
			{
				m_children = value;
			}
		}

		public static implicit operator Prototype(string strPrototypeName)
		{
			if (null == strPrototypeName)
				return null;

			return Prototypes.GetPrototypeByPrototypeName(strPrototypeName);
		}

		public Prototype()
		{
		}

		protected Prototype(string strPrototypeName)
		{
			//This creates a copy of the prototype 

			Prototype ? prototype = TemporaryPrototypes.GetTemporaryPrototypeOrNull(strPrototypeName);
			if (null == prototype)
				throw new Exception("Temporary Prototype does not exist: " + strPrototypeName);

			this.PrototypeID = prototype.PrototypeID;
			this.PrototypeName = prototype.PrototypeName;
			this.Value = prototype.Value;

			//We must clone the properties in case any were initialized already
			this.Properties = new Ontology.PrototypePropertiesCollection(this, prototype);
		}

		protected Prototype(int iPrototypeID)
		{
			Prototype? prototype = TemporaryPrototypes.GetTemporaryPrototypeOrNull(iPrototypeID);
			if (null == prototype)
				throw new Exception("Temporary Prototype does not exist: " + iPrototypeID);

			this.PrototypeID = prototype.PrototypeID;
			this.PrototypeName = prototype.PrototypeName;
			this.Value = prototype.Value;

			//We must clone the properties in case any were initialized already 
			this.Properties = new Ontology.PrototypePropertiesCollection(this, prototype);
			return;
		}

		public virtual Prototype Clone()
		{
			return Prototypes.CloneCircular(this);
		}

		private void InvalidateCacheParents()
		{
			var stack = new Stack<Prototype>();
			var visited = new HashSet<Prototype>();
			stack.Push(this);

			while (stack.Count > 0)
			{
				Prototype current = stack.Pop();
				if (!visited.Add(current))
					continue;

				current.m_lstCachedParents = null;

				foreach (Prototype protoDescendant in current.GetDescendants())
				{
					stack.Push(protoDescendant);
				}
			}
		}

		protected List<int>? m_lstTypeOfs = null;
		protected List<int> TypeOfsCollection
		{
			get
			{
				if (m_lstTypeOfs == null)
				{
					m_lstTypeOfs = new List<int>(capacity:2);
				}
				return m_lstTypeOfs;
			}
		}

		public IEnumerable<Prototype> Ancestors 		
		{
			get
			{
				return TypeOfsCollection.Select(x => Prototypes.GetPrototype(x)).ToList();
			}
		}

		//N20260110-01 - A switch to allow us to change the TypeOfs
		private bool m_bIsMutable = false;
		public bool IsMutable
		{
			get { return m_bIsMutable; }
			set { m_bIsMutable = value; }
		}

		public Prototype InsertTypeOf(Prototype protoParent)
		{
			return InsertTypeOf(protoParent.PrototypeID);
		}

		public virtual Prototype InsertTypeOf(int iParentPrototypeID)
		{
			if (m_bIsCopy && !m_bIsMutable)
				throw new InvalidOperationException("Cannot insert type of on a copy of a prototype.");

			if (this.PrototypeID == iParentPrototypeID)
				throw new InvalidOperationException("A prototype cannot be a type-of itself.");

			if (!this.TypeOfsCollection.Contains(iParentPrototypeID))
			{
				this.TypeOfsCollection.Add(iParentPrototypeID);

				if (this.PrototypeID == 0)
					throw new Exception("Prototypes should be created before the TypeOf is set.");

				Prototypes.GetPrototype(iParentPrototypeID).DescendantsCollection.Add(this.PrototypeID);

				InvalidateCacheParents();
			}

			return this;
		}
		public virtual Prototype InsertPrimaryTypeOf(int iParentPrototypeID)
		{
			if (m_bIsCopy)
				throw new Exception("Cannot insert type of on a copy of a prototype.");

			if (this.PrototypeID == iParentPrototypeID)
				throw new InvalidOperationException("A prototype cannot be a type-of itself.");

			RemoveTypeOf(iParentPrototypeID); // remove any existing type of first

			if (!this.TypeOfsCollection.Contains(iParentPrototypeID))
			{
				this.TypeOfsCollection.Insert(0, iParentPrototypeID);

				if (this.PrototypeID == 0)
					throw new Exception("Prototypes should be created before the TypeOf is set.");

				Prototypes.GetPrototype(iParentPrototypeID).DescendantsCollection.Add(this.PrototypeID);
				InvalidateCacheParents();
			}

			return this;
		}


		public virtual Prototype RemoveTypeOf(int protoPath)
		{
			if (m_lstTypeOfs == null)        // nothing recorded yet
				return this;

			int idx = m_lstTypeOfs.IndexOf(protoPath);
			if (idx >= 0)
			{
				m_lstTypeOfs.RemoveAt(idx);
				Prototypes.GetPrototype(protoPath).DescendantsCollection.Remove(this.PrototypeID);

				InvalidateCacheParents();
			}
			return this;
		}
		public virtual Prototype RemoveTypeOfAndDescendants(Prototype protoPath)
		{
			// nothing recorded yet
			var list = m_lstTypeOfs;
			if (list is null || list.Count == 0)
				return this;

			bool modified = false;

			// walk backwards so RemoveAt is O(1) and order of remaining items is kept
			for (int i = list.Count - 1; i >= 0; i--)
			{
				if (Prototypes.TypeOf(list[i], protoPath))
				{
					int parentId = list[i];
					list.RemoveAt(i);
					Prototypes.GetPrototype(parentId).DescendantsCollection.Remove(this.PrototypeID);

					modified = true;
				}
			}

			if (modified)
				InvalidateCacheParents();      // fire exactly once

			return this;
		}

		public virtual List<int> GetTypeOfs()
		{
			return TypeOfsCollection;
		}

		public virtual Prototype InsertNestedParent(Prototype protoParent)
		{
			return InsertNestedParent(protoParent.PrototypeID);
		}
		public virtual Prototype InsertNestedParent(int iNestedParentPrototypeID)
		{
			if (m_bIsCopy && !m_bIsMutable)
				throw new InvalidOperationException("Cannot insert nested parent on a copy of a prototype.");

			if (this.PrototypeID == iNestedParentPrototypeID)
				throw new InvalidOperationException("A prototype cannot be nested under itself.");

			if (m_iNestedParentPrototypeID == iNestedParentPrototypeID)
				return this;

			if (m_iNestedParentPrototypeID != 0)
				Prototypes.GetPrototype(m_iNestedParentPrototypeID).NestedChildrenCollection.Remove(this.PrototypeID);

			m_iNestedParentPrototypeID = iNestedParentPrototypeID;
			if (m_iNestedParentPrototypeID != 0)
				Prototypes.GetPrototype(m_iNestedParentPrototypeID).NestedChildrenCollection.Add(this.PrototypeID);

			return this;
		}
		public virtual Prototype RemoveNestedParent()
		{
			if (m_iNestedParentPrototypeID == 0)
				return this;

			Prototypes.GetPrototype(m_iNestedParentPrototypeID).NestedChildrenCollection.Remove(this.PrototypeID);
			m_iNestedParentPrototypeID = 0;

			return this;
		}



		public virtual bool ShallowEqual(Prototype rhs)
		{
			return this.PrototypeID == rhs?.PrototypeID;
		}

		public virtual bool ShallowEquivalent(Prototype rhs)
		{
			return this.ShallowEqual(rhs);
		}

		public virtual bool TypeOf(Prototype parent)
		{
			if (null == parent)
				return false;

			if (Prototypes.AreShallowEqual(this, parent))
				return true;

			//TODO: This could be shortcut
			foreach (int protoTypeOf in GetAllParents())
			{
				if (protoTypeOf == parent.PrototypeID)
					return true;
			}

			return false;
		}

		protected bool m_bIsCopy = false;

		public virtual Prototype ShallowClone()
		{
			Prototype prototype = new Prototype();
			PopulateClone(prototype);
			return prototype;
		}

		protected void PopulateClone(Prototype prototype)
		{
			PopulateClone(prototype, this);
		}
		protected void PopulateClone(Prototype prototype, Prototype protoSource)
		{
			prototype.PrototypeID = protoSource.PrototypeID;
			prototype.PrototypeName = protoSource.PrototypeName;
			prototype.Value = protoSource.Value;
			prototype.m_mapAssociations = protoSource.m_mapAssociations;
			prototype.m_mapPartOfValues = protoSource.m_mapPartOfValues;
			prototype.m_mapData = protoSource.m_mapData;
			prototype.m_protoParent = null;
			prototype.m_lstTypeOfs = protoSource.m_lstTypeOfs;
			prototype.m_bIsCopy = true;
			prototype.m_bIsInstance = protoSource.m_bIsInstance;

			if (prototype.m_bIsInstance) // cloning an instance
			{
				//For now we aren't going to consider these copies, since they aren't
				//modifying the singleton prototype. But more thought is needed. 
				prototype.m_bIsCopy = false;
			}

		}

		protected bool m_bIsInstance = false;
		public virtual Prototype CreateInstance(string ? strInstanceName = null)
		{
			Prototype protoInstance = this.ShallowClone();

			protoInstance.PrototypeName = this.PrototypeName + "#" + (strInstanceName ?? TemporaryPrototypes.ListById.Count.ToString());
			protoInstance.m_bIsInstance = true;
			protoInstance.m_bIsCopy = false; // this is not a copy, but an instance of the prototype
			protoInstance.PrototypeID = TemporaryPrototypes.InsertPrototype(protoInstance);
			protoInstance.InsertPrimaryTypeOf(this.PrototypeID);

			return protoInstance;
		}

		public virtual Prototype GetBaseType()
		{
			if (this.IsInstance())
			{
				return Prototypes.GetPrototype(this.TypeOfsCollection.First());
			}

			return this;
		}

		public virtual bool IsInstance()
		{
			return this.m_bIsInstance;
		}


		protected List<int>? m_lstCachedParents = null;

		public virtual IEnumerable<int> GetParents()
		{
			return GetTypeOfs();
		}
		public virtual IEnumerable<int> GetAllParents()
		{
			//always use the singleton here,
			//N20260109-01 - unless we've marked mutable
			Prototype prototype = (m_bIsMutable ? this : Prototypes.GetPrototype(this.PrototypeID));

			if (null == prototype.m_lstCachedParents)
				prototype.m_lstCachedParents = prototype.GetAllParentsCircular(new List<int>());

			return prototype.m_lstCachedParents;
		}

		public virtual IEnumerable<Prototype> GetAncestorsBelow(Prototype parent)
		{
			return this.GetAllParents().Where(x => Prototypes.GetPrototype(x).GetTypeOfs().Any(x => x == parent.PrototypeID)).Select(x => Prototypes.GetPrototype(x));
		}

		private List<int> GetAllParentsCircular(List<int> setPrototypes)
		{
			List<int> lstPrototypes = new List<int>();
			foreach (int iParentID in GetTypeOfs())
			{
				//Can have itself as a parent in shadows
				if (this.PrototypeID == iParentID)
					continue;

				if (setPrototypes.Contains(iParentID))
					continue;

				lstPrototypes.Add(iParentID);
				setPrototypes.Add(iParentID);

				foreach (int iParentID2 in Prototypes.GetPrototype(iParentID).GetAllParentsCircular(setPrototypes))
				{
					if (!lstPrototypes.Contains(iParentID2))
						lstPrototypes.Add(iParentID2);
				}
			}

			return lstPrototypes;
		}


		public void SetParents()
		{
			foreach (var pair in this.Properties)
			{
				if (pair.Value != null)
				{
					pair.Value.m_protoParent = this;
					pair.Value.SetParents();
				}
			}

			foreach (Prototype child in this.Children)
			{
				if (null != child)
				{
					child.m_protoParent = this;
					child.SetParents();
				}
			}
		}

		private HashSet<int>? m_lstDescendants = null;
		protected HashSet<int> DescendantsCollection
		{
			get
			{
				if (m_lstDescendants == null)
				{
					m_lstDescendants = new HashSet<int>();
				}
				return m_lstDescendants;
			}
		}

		private HashSet<int>? m_setNestedChildren = null;
		protected HashSet<int> NestedChildrenCollection
		{
			get
			{
				if (m_setNestedChildren == null)
				{
					m_setNestedChildren = new HashSet<int>();
				}
				return m_setNestedChildren;
			}
		}


		public virtual IEnumerable<Prototype> GetDescendants()
		{
			foreach (int id in DescendantsCollection)
				yield return Prototypes.GetPrototype(id);
		}

		public virtual IEnumerable<Prototype> GetNestedChildren()
		{
			Prototype prototype = (m_bIsMutable ? this : Prototypes.GetPrototype(this.PrototypeID));
			foreach (int id in prototype.NestedChildrenCollection)
				yield return Prototypes.GetPrototype(id);
		}

		public virtual IEnumerable<Prototype> GetAllDescendants()
		{
			var seen = new HashSet<int>(capacity: 32);   // visited IDs
			var stack = new Stack<Prototype>();
			stack.Push(this);

			while (stack.Count != 0)
			{
				Prototype parent = stack.Pop();

				foreach (Prototype child in parent.GetDescendants())
				{
					// Skip already-visited nodes (prevents cycles)
					if (!seen.Add(child.PrototypeID)) continue;

					yield return child;      // stream the result immediately
					stack.Push(child);       // depth-first walk
				}
			}
		}

		
		public virtual IEnumerable<Prototype> GetAllDescendantsWhere(Predicate<Prototype> predicate)
		{
			foreach (Prototype p in GetAllDescendants())
			{
				//on-the-fly filtering; no intermediate list
				if (predicate(p))
					yield return p;

			}
		}


		public void Associate(Prototype prototype)
		{
			if (Associations.ContainsKey(prototype))
				Associations[prototype] += 1;
			else
				Associations[prototype] = 1;
		}

		public void NormalizeAssociations()
		{
			double dTotal = Associations.Values.Sum();
			var keys = Associations.Keys.ToList();
			foreach (var key in keys)
			{
				Associations[key] /= dTotal;
			}
		}

		public void AssociateWithWeight(Prototype prototype, double dWeight)
		{
			Associations[prototype] = dWeight;
		}


		public void BidirectionalAssociate(Prototype prototype)
		{
			Associate(prototype);
			prototype.Associate(this);
		}


		//The FastPrototypeMap is not going to deal with differences in the instance, just PrototypeID
		protected FastPrototypeMap<double>? m_mapAssociations = null;

		public FastPrototypeMap<double> Associations
		{
			get
			{
				if (null == m_mapAssociations)
					m_mapAssociations = new FastPrototypeMap<double>();

				return m_mapAssociations;
			}
		}
		public IOrderedEnumerable<KeyValuePair<Prototype, double>> OrderedAssociations
		{
			get
			{
				return Associations.OrderByDescending(x => x.Value);
			}
		}

		protected FastPrototypeMap<double>? m_mapPartOfValues = null;

		public FastPrototypeMap<double> PartOfValues
		{
			get
			{
				if (null == m_mapPartOfValues)
					m_mapPartOfValues = new FastPrototypeMap<double>();
				return m_mapPartOfValues;
			}
		}


		protected Map<string, object>? m_mapData = null;

		public Map<string, object> Data
		{
			get
			{
				if (m_mapData == null)
					m_mapData = new Map<string, object>();

				return m_mapData;
			}
			set
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value), "Data cannot be set to null.");
				m_mapData = value;
			}
		}



		public virtual JsonObject ToJsonObjectCircular(bool bRemoveNulls, Set<int> setHashes)
		{
			return ToFriendlyJsonObjectCircular(setHashes);
		}

		public virtual JsonObject ToJsonObject(bool bRemoveNulls = false)
		{
			JsonObject jsonPrototype = new JsonObject();

			if (!AllowTemporaryPrototypeToJSON && this.PrototypeID < 0)
				throw new Exception("Trying to save temporary prototype");

			jsonPrototype[nameof(PrototypeID)] = this.PrototypeID;

			foreach (var pair in this.Properties)
			{
				if (!bRemoveNulls || pair.Value != null)
				{
					if (pair.Key < 0)
					{
						jsonPrototype[Prototypes.GetPrototypeName(pair.Key)] = pair.Value?.ToJsonObject();
					}
					else
						jsonPrototype[pair.Key.ToString()] = pair.Value?.ToJsonObject();
				}
			}

			if (this.Children.Count > 0)
			{
				JsonArray jsonChildren = new JsonArray();

				foreach (Prototype child in this.Children)
				{
					jsonChildren.Add(child == null ? null : child.ToJsonObject());
				}

				jsonPrototype[nameof(Children)] = jsonChildren;
			}

			return jsonPrototype;
		}

		public virtual string ToJSON(bool bRemoveNulls = false)
		{
			return ToJsonObject(bRemoveNulls).ToJSON();
		}

		public virtual JsonObject ToFriendlyJsonObject()
		{
			JsonObject jsonPrototype = new JsonObject();

			if (!AllowTemporaryPrototypeToJSON && this.PrototypeID < 0)
				throw new Exception("Trying to save temporary prototype");

			jsonPrototype[nameof(PrototypeID)] = this.PrototypeID;
			jsonPrototype[nameof(PrototypeName)] = this.PrototypeName;

			foreach (var pair in this.Properties)
			{
				jsonPrototype[Prototypes.GetPrototypeName(pair.Key)] = pair.Value?.ToFriendlyJsonObject();
			}

			if (this.Children.Count > 0)
			{
				JsonArray jsonChildren = new JsonArray();

				foreach (Prototype child in this.Children)
				{
					if (child == null)
						jsonChildren.Add(new JsonValue((object)null));
					else
						jsonChildren.Add(child.ToFriendlyJsonObject());
				}

				jsonPrototype[nameof(Children)] = jsonChildren;
			}

			return jsonPrototype;
		}

		public virtual JsonObject ToFriendlyJsonObjectCircular(Set<int> setHashes)
		{
			//circular detection needed because this may be a collection that contains temporary prototypes
			JsonObject jsonPrototype = new JsonObject();
			jsonPrototype[nameof(PrototypeID)] = this.PrototypeID;
			jsonPrototype[nameof(PrototypeName)] = this.PrototypeName;

			if (setHashes.Contains(this.GetHashCode()))
				return jsonPrototype;

			setHashes.Add(this.GetHashCode());


			if (!AllowTemporaryPrototypeToJSON && this.PrototypeID < 0)
				throw new Exception("Trying to save temporary prototype");


			foreach (var pair in this.Properties)
			{
				jsonPrototype[Prototypes.GetPrototypeName(pair.Key)] = pair.Value?.ToFriendlyJsonObjectCircular(setHashes);
			}

			if (this.Children.Count > 0)
			{
				JsonArray jsonChildren = new JsonArray();

				foreach (Prototype child in this.Children)
				{
					if (child == null)
						jsonChildren.Add(new JsonValue((object)null));
					else
						jsonChildren.Add(child.ToFriendlyJsonObjectCircular(setHashes));
				}

				jsonPrototype[nameof(Children)] = jsonChildren;
			}

			return jsonPrototype;
		}

		public virtual string ToFriendlyJSON()
		{
			return ToFriendlyJsonObject().ToJSON();
		}

		public static Prototype FromJsonValue(JsonValue jsonValue)
		{
			return NativeValuePrototype.FromJsonValue(jsonValue);
		}

		public static Prototype FromJSON(string strJSON)
		{
			return NativeValuePrototype.FromJsonValue(new JsonValue(strJSON));
		}

		public override string ToString()
		{
			//This fixes an undetectable bug prototype + "::" + prototype
			return PrototypeName;
		}

		public void Add(Prototype protoElement)
		{
			this.Children.Add(protoElement);
		}

		public void AddRange(IEnumerable<Prototype> protoCollection)
		{
			foreach (Prototype proto in protoCollection)
				Add(proto);
		}
	}
}
