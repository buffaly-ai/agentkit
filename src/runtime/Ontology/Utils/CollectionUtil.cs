namespace Ontology.Utils
{
	public class CollectionUtil
	{
		static public bool StartsWith(Prototype prototype, Prototype protoTarget)
		{
			if (protoTarget.Children.Count > prototype.Children.Count)
				return false;

			for (int i = 0; i < protoTarget.Children.Count; i++)
			{
				if (!PrototypeGraphs.AreEqual(prototype.Children[i], protoTarget.Children[i]))
					return false;
			}

			return true;
		}

		static public bool EndsWith(Prototype prototype, Prototype protoTarget)
		{
			if (protoTarget.Children.Count > prototype.Children.Count)
				return false;

			for (int i = 0; i < protoTarget.Children.Count; i++)
			{
				if (!PrototypeGraphs.AreEqual(prototype.Children[prototype.Children.Count - protoTarget.Children.Count + i], protoTarget.Children[i]))
					return false;
			}

			return true;
		}
		static public Prototype LeftOfFirst(Prototype prototype, Prototype protoTarget)
		{
			int iIndex = IndexOf(prototype, protoTarget);
			if (iIndex >= 0)
			{
				Prototype protoResult = new Ontology.Collection();
				protoResult.Children.AddRange(prototype.Children.GetRange(0, iIndex));
				return protoResult;
			}

			return null;
		}

		static public Prototype RightOfLast(Prototype prototype, Prototype protoTarget)
		{
			int iIndex = LastIndexOf(prototype, protoTarget);
			if (iIndex >= 0)
			{
				Prototype protoResult = new Ontology.Collection();
				protoResult.Children.AddRange(prototype.Children.GetRange(iIndex + 1, prototype.Children.Count - iIndex - 1));
				return protoResult;
			}

			return null;
		}

		static public Prototype RightOfFirst(Prototype prototype, Prototype protoTarget)
		{
			int iIndex = IndexOf(prototype, protoTarget);
			if (iIndex >= 0)
			{
				Prototype protoResult = new Ontology.Collection();
				protoResult.Children.AddRange(prototype.Children.GetRange(iIndex + 1, prototype.Children.Count - iIndex - 1));
				return protoResult;
			}

			return null;
		}

		static public int IndexOf(Prototype prototype, Prototype protoTarget)
		{
			for (int i = 0; i < prototype.Children.Count; i++)
			{
				if (PrototypeGraphs.AreEqual(prototype.Children[i], protoTarget))
					return i;
			}

			return -1;
		}

		static public int LastIndexOf(Prototype prototype, Prototype protoTarget)
		{
			for (int i = prototype.Children.Count - 1; i >= 0; i--)
			{
				if (PrototypeGraphs.AreEqual(prototype.Children[i], protoTarget))
					return i;
			}

			return -1;
		}

		static public Prototype GetIntersection(Prototype protoCollection1, Prototype protoCollection2)
		{
			if (!Prototypes.TypeOf(protoCollection1, Ontology.Collection.Prototype) || !Prototypes.TypeOf(protoCollection2, Ontology.Collection.Prototype))
				throw new Exception("GetIntersection only supports Ontology.Collection");

			Prototype protoResult = Ontology.Collection.Prototype.Clone();

			foreach (Prototype protoChild in protoCollection1.Children)
			{
				if (protoCollection2.Children.Any(x => PrototypeGraphs.AreEqual(protoChild, x)))
					protoResult.Children.Add(protoChild);
			}

			return protoResult;
		}

		static public Prototype Minus(Prototype protoCollection1, Prototype protoCollection2)
		{
			//Performs protoCollection1 - protoCollection2, leaving items that appear in protoCollection1 only
			if (!Prototypes.TypeOf(protoCollection1, Ontology.Collection.Prototype) || !Prototypes.TypeOf(protoCollection2, Ontology.Collection.Prototype))
				throw new Exception("GetIntersection only supports Ontology.Collection");

			Prototype protoResult = Ontology.Collection.Prototype.Clone();

			foreach (Prototype protoChild in protoCollection1.Children)
			{
				if (!protoCollection2.Children.Any(x => PrototypeGraphs.AreEqual(protoChild, x)))
					protoResult.Children.Add(protoChild);
			}

			return protoResult;
		}
	}

}
