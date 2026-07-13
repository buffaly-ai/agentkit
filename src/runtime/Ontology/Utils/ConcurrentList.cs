using System.Collections;

namespace Ontology.Utils
{
	public class ConcurrentList : IEnumerable
	{
		public List<Prototype> List1;
		public List<Prototype> List2;

		public ConcurrentList()
		{
			List1 = new List<Prototype>();
			List2 = new List<Prototype>();
		}
		public ConcurrentList(List<Prototype> lst1, List<Prototype> lst2)
		{
			if (lst1.Count != lst2.Count)
				throw new Exception("List 1 and 2 are not the same size");

			List1 = lst1;
			List2 = lst2;
		}

		public IEnumerator<Tuple<Prototype, Prototype>> GetEnumerator()
		{
			for (int i = 0; i < List1.Count(); i++)
			{
				yield return new Tuple<Prototype, Prototype>(List1[i], List2[i]);
			}

			yield break;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			for (int i = 0; i < List1.Count(); i++)
			{
				yield return new Tuple<Prototype, Prototype>(List1[i], List2[i]);
			}

			yield break;
		}

		public void Add(Prototype prototype1, Prototype prototype2)
		{
			List1.Add(prototype1);
			List2.Add(prototype2);
		}

		public void Add(Tuple<Prototype, Prototype> tuple)
		{
			List1.Add(tuple.Item1);
			List2.Add(tuple.Item2);
		}

		public void AddRange(ConcurrentList range)
		{
			foreach (var pair in range)
			{
				this.Add(pair);
			}
		}

		public ConcurrentList Swapped()
		{
			return new ConcurrentList(this.List2, this.List1);
		}
	}

}
