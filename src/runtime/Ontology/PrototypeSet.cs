using System.Collections.Concurrent;

namespace Ontology
{
	public class PrototypeComparer : IEqualityComparer<Prototype>
	{
		public bool Equals(Prototype x, Prototype y)
		{
			return PrototypeGraphs.AreEqual(x, y);
		}

		public int GetHashCode(Prototype obj)
		{
			return PrototypeGraphs.GetHash(obj).GetHashCode();
		}
	}

	public class PrototypeSet : HashSet<Prototype>
	{
		public PrototypeSet() : base(new PrototypeComparer())
		{
		}

		public PrototypeSet(IEnumerable<Prototype> lstPrototypes)
		{
			this.AddRange(lstPrototypes);
		}

		public new bool Add(Prototype item)
		{
			//Note that the normal add isn't working because GetHashCode can return a different result for equal graphs
			if (!Contains(item))
				return base.Add(item);

			return false;
		}

		public void AddRange(IEnumerable<Prototype> setToAdd)
		{
			foreach (Prototype prototype in setToAdd)
				this.Add(prototype);
		}

		public new bool Contains(Prototype item)
		{
			int iHashCode = item.GetHashCode();

			return (this.Any(x => x.GetHashCode() == iHashCode) || this.Any(x => PrototypeGraphs.AreEqual(x, item)));
		}

		public PrototypeSet Clone()
		{
			PrototypeSet setPrototypes = new Ontology.PrototypeSet(); 
			foreach (Prototype prototype in this)
			{
				setPrototypes.Add(prototype.Clone());
			}
			return setPrototypes;
		}

		static public PrototypeSet Minus(PrototypeSet setPrototypes, PrototypeSet setToRemove)
		{
			PrototypeSet setResult = new PrototypeSet(); 

			foreach (Prototype prototype in setPrototypes)
			{
				if (!setToRemove.Contains(prototype))
					setResult.Add(prototype);
			}

			return setResult;
		}
	}


	public class PrototypeCompareToComparer : IComparer<Prototype>
	{
		int IComparer<Prototype>.Compare(Prototype x, Prototype y)
		{
			string strX = PrototypeGraphs.GetHash(x);
			string strY = PrototypeGraphs.GetHash(y);

			return strX.CompareTo(strY);
		}
	}
	public class FastPrototypeCompareToComparer : IEqualityComparer<Prototype>
	{	
		bool IEqualityComparer<Prototype>.Equals(Prototype x, Prototype y)
		{
			return x?.PrototypeID == y?.PrototypeID;
		}

		int IEqualityComparer<Prototype>.GetHashCode(Prototype obj)
		{
			return obj.PrototypeID;
		}
	}


	public class FastPrototypeMap<T> : ConcurrentDictionary<Prototype, T>
	{
		public FastPrototypeMap() : base(new FastPrototypeCompareToComparer())
		{

		}

		public FastPrototypeMap(IEnumerable<KeyValuePair<Prototype, T>> lstPrototypes) : base(new FastPrototypeCompareToComparer())
		{
			foreach (var pair in lstPrototypes)
			{
				this.TryAdd(pair.Key, pair.Value);
			}
		}
	}
	public class FastPrototypeComparer : IEqualityComparer<Prototype>
	{
		public bool Equals(Prototype x, Prototype y)
		{
			if (ReferenceEquals(x, y))
				return true;
			if (x is null || y is null)
				return false;

			return x.ShallowEqual(y);
		}

		public int GetHashCode(Prototype obj)
		{
			if (obj is null)
				return 0;

			if (obj is NativeValuePrototype nativeValuePrototype)
				return nativeValuePrototype.NativeValue.GetHashCode();
			else
				return obj.PrototypeID.GetHashCode();
		}
	}

	public class FastPrototypeSet : HashSet<Prototype>
	{
		public FastPrototypeSet() : base(new FastPrototypeComparer())
		{
		}

		public FastPrototypeSet(IEnumerable<Prototype> lstPrototypes) : base(lstPrototypes, new FastPrototypeComparer())
		{
		}

		public void AddRange(IEnumerable<Prototype> setToAdd)
		{
			foreach (Prototype prototype in setToAdd)
				this.Add(prototype);
		}

		public FastPrototypeSet Clone()
		{
			FastPrototypeSet setPrototypes = new FastPrototypeSet(this);
			foreach (Prototype prototype in this)
			{
				setPrototypes.Add(prototype.Clone());
			}
			return setPrototypes;
		}

		public static FastPrototypeSet Minus(FastPrototypeSet setPrototypes, FastPrototypeSet setToRemove)
		{
			FastPrototypeSet setResult = new FastPrototypeSet(setPrototypes);

			foreach (Prototype prototype in setToRemove)
			{
				setResult.Remove(prototype);
			}

			return setResult;
		}

		// Implementing the indexer to retrieve the original prototype if it exists
		public Prototype this[Prototype potentialDuplicate]
		{
			get
			{
				if (this.TryGetValue(potentialDuplicate, out var original))
					return original;

				this.Add(potentialDuplicate);
				return potentialDuplicate;
			}
		}
	}





	public class PrototypeMap<T> : SortedDictionary<Prototype, T>
	{
		public PrototypeMap() : base(new PrototypeCompareToComparer())
		{

		}
	}

	public class PrototypeOccurrences
	{
		private FastPrototypeMap<int> m_mapCounts = new FastPrototypeMap<int>();

		public FastPrototypeMap<int> Counts
		{
			get
			{
				return m_mapCounts;
			}
		}

		public int Count
		{
			get
			{
				return m_mapCounts.Count;

			}
		}

		public void Add(Prototype prototype)
		{
			if (!m_mapCounts.ContainsKey(prototype))
				m_mapCounts[prototype] = 1;
			else
				m_mapCounts[prototype]++;
		}

		public IEnumerable<Prototype> GetMostOccurring()
		{
			return this.m_mapCounts.OrderByDescending(x => x.Value).Select(x => x.Key);
		}

		public Prototype GetTop1MostOccurrent()
		{
			return this.m_mapCounts.OrderByDescending(x => x.Value).FirstOrDefault().Key;
		}

		public int GetCount(Prototype prototype)
		{
			if (m_mapCounts.ContainsKey(prototype))
				return m_mapCounts[prototype];
			return 0;
		}

		public IEnumerable<Prototype> GetMostOccurringTier(int iMinimum)
		{
			int iTopValue = 0;
			int iReturned = 0;
			foreach (var pair in m_mapCounts.OrderByDescending(x => x.Value))
			{
				if (iTopValue == 0)
					iTopValue = pair.Value;

				if (pair.Value < iTopValue && iReturned >= iMinimum)
					break;

				iReturned++;
				yield return pair.Key;
			}

			yield break;
		}
	}
}
