namespace Ontology
{
	public class TypeOfs
	{
		static public Prototype Merge(Prototype prototype1, Prototype prototype2)
		{
			foreach (int protoTypeOf in prototype2.GetTypeOfs())
			{
				prototype1.InsertTypeOf(protoTypeOf);
			}

			return prototype1;
		}

		static public Prototype Insert(Prototype prototype, int iParentID)
		{
			return prototype.InsertTypeOf(iParentID);
		}

		static public Prototype Insert(Prototype prototype, Prototype parent)
		{
			return prototype.InsertTypeOf(parent.PrototypeID);
		}

		static public Prototype InsertRange(Prototype prototype1, Collection collection)
		{
			foreach (Prototype protoTypeOf in collection.Children)
			{
				Insert(prototype1, protoTypeOf.PrototypeID);
			}

			return prototype1;
		}

		static public Prototype Remove(Prototype prototype, Prototype protoRemove)
		{
			return prototype.RemoveTypeOf(protoRemove.PrototypeID);
		}
	}
}
