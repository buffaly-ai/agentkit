using Utilities;

namespace Ontology
{
	public class Collection : Prototype, IEnumerable<Prototype>
	{



		public const string PrototypeName = nameof(Ontology) + "." + nameof(Collection);

		public static int PrototypeID
		{
			get
			{
				return Prototype.PrototypeID;
			}
		}

		private static ResettablePrototypeAsyncLocal m_Prototype = new ResettablePrototypeAsyncLocal();
		public static Prototype Prototype
		{
			get
			{
				if (null == m_Prototype.Value)
					m_Prototype.Value = Prototypes.GetOrInsertPrototype(PrototypeName);

				return m_Prototype.Value;
			}
		}

		public Collection() : base(Collection.PrototypeID)
		{

		}

		public Collection(Prototype prototype) : base(Collection.PrototypeID)
		{
			this.Children.AddRange(prototype.Children);
		}

		public Collection(IEnumerable<Prototype> lstPrototypes) : base(Collection.PrototypeID)
		{
			this.Children.AddRange(lstPrototypes);
		}

                public Collection(IEnumerable<int> lstPrototypes) : base(Collection.PrototypeID)
                {
                        this.Children.AddRange(lstPrototypes.Select(x => Prototypes.GetPrototype(x)));
                }
		public Collection(Collection col) : base(Collection.PrototypeID)
		{
			this.Children.AddRange(col);
		}

		public static implicit operator List<Prototype>(Collection collection)
		{
			return collection.Children;
		}

		public IEnumerator<Prototype> GetEnumerator()
		{
			return this.Children.GetEnumerator();
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.Children.GetEnumerator();
		}


		public override Prototype ShallowClone()
		{
			return new Collection();
		}

		public override Prototype Clone()
		{
			Collection copy = new Collection();
			copy.Value = this.Value;
			copy.Properties = this.Properties.Clone(copy);

			foreach (Prototype child in this.Children)
			{
				copy.Children.Add(child.Clone());
			}

			return copy;
		}
		public Collection GetRange(int iStartIndex, int iLength)
		{
			Collection protoRange = new Collection();
			protoRange.Children.AddRange(this.Children.GetRange(iStartIndex, iLength));
			return protoRange;
		}

		static public Collection GetRange(Prototype protoCollection, int iStartIndex, int iLength)
		{
			Collection protoRange = new Collection();
			protoRange.Children.AddRange(protoCollection.Children.GetRange(iStartIndex, iLength));
			return protoRange;
		}

		public void Add(Prototype protoElement)
		{
			this.Children.Add(protoElement);
		}

		public void AddElementOrRange(Prototype protoElement)
		{
			if (Prototypes.TypeOf(protoElement, Collection.Prototype))
				this.Children.AddRange(protoElement.Children);
			else 
				this.Children.Add(protoElement);
		}

		public Collection Flatten()
		{
			Collection protoChildren = new Collection(); 
			foreach (Prototype protoChild in this.Children)
			{
				protoChildren.AddElementOrRange(protoChild);
			}
			return protoChildren;
		}

		static public Collection Flatten(IEnumerable<Prototype> lstPrototypes)
		{
			Collection protoChildren = new Collection();
			foreach (Prototype protoChild in lstPrototypes)
			{
				protoChildren.AddElementOrRange(protoChild);
			}
			return protoChildren;
		}

		public void AddRange(Collection collection)
		{
			this.Children.AddRange(collection.Children);
		}

		public void InsertRange(int iIndex, Collection collection)
		{
			this.Children.InsertRange(iIndex, collection.Children);
		}

		public Prototype FirstOrDefault(Predicate<Prototype> f)
		{
			return this.Children.FirstOrDefault(x => f(x));
		}

		public Prototype First()
		{
			return this.Children.First();
		}

		public Prototype Last()
		{
			return this.Children.Last();
		}

        public Prototype Random()
        {
            return this.Children.Random();
        }

		public bool Any(Predicate<Prototype> f)
		{
			return this.Children.Any(x => f(x));
		}

		public Collection Where(Predicate<Prototype> f)
		{
			return new Collection(this.Children.Where(x => f(x)));
		}

		public bool IsEmpty()
		{
			return this.Children.Count == 0;
		}

		public void RemoveAt(int iIndex)
		{
			this.Children.RemoveAt(iIndex);
		}

		public void Clear()
		{
			this.Children.Clear();
		}

		public int Count
		{
			get
			{
				return this.Children.Count;
			}
		}

		public override string ToString()
		{
			return "Ontology.Collection[" + string.Join(", ", this.Children.Select(x => x.ToString())) + "]";
		}
	}
}
