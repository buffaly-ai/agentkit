namespace Ontology
{
	public class PrototypeGraphLists
	{
		static public List<Prototype> Clone(List<Prototype> prototypes)
		{
			List<Prototype> lstCloned = new List<Prototype>();
			foreach (Prototype prototype in prototypes)
			{
				lstCloned.Add(prototype.Clone());
			}
			return lstCloned;
		}

		static public Prototype ? ComparePrototypes(List<Prototype> protoInputs, bool bShallow = false)
		{
			Prototype ? protoShadowTree = protoInputs[0];

			for (int i = 1; i < protoInputs.Count; i++)
			{
				protoShadowTree = PrototypeGraphs.ComparePrototypes(protoShadowTree, protoInputs[i]);
			}

			return protoShadowTree;
		}



		public static List<List<Prototype>> MinusValues(List<Prototype> lstSources, List<Prototype> lstShadows)
		{
			List<List<Prototype>> lstResults = new List<List<Ontology.Prototype>>();

			for (int i = 0; i < lstSources.Count; i++)
			{
				lstResults.Add(PrototypeGraphs.MinusValues(lstSources[i], lstShadows[i]));
			}

			return lstResults;
		}

		public static List<List<Prototype>> Minus(List<Prototype> lstSources, List<Prototype> lstShadows)
		{
			List<List<Prototype>> lstResults = new List<List<Ontology.Prototype>>();

			for (int i = 0; i < lstSources.Count; i++)
			{
				lstResults.Add(PrototypeGraphs.Minus(lstSources[i], lstShadows[i], true));
			}

			return lstResults;
		}

		public static bool TypeOf(List<Prototype> lstSources, List<Prototype> lstTargets)
		{
			for (int i = 0; i < lstSources.Count; i++)
			{
				if (!Prototypes.TypeOf(lstSources[i], lstTargets[i]))
					return false; 
			}

			return true; 
		}

		public static bool TypeOf(List<Prototype> lstSources, Prototype protoTarget)
		{
			for (int i = 0; i < lstSources.Count; i++)
			{
				if (!Prototypes.TypeOf(lstSources[i], protoTarget))
					return false;
			}

			return true;
		}

		static public List<Prototype> SelectIndex(List<List<Prototype>> lstCollection, int iIndex)
		{
			List<Prototype> lstPrototypes = new List<Prototype>();
			foreach (List<Prototype> prototypes in lstCollection)
			{
				lstPrototypes.Add(prototypes[iIndex]);
			}
			return lstPrototypes;
		}

		static public bool AreEqual(List<Prototype> protoInputs)
		{
			for (int i = 0; i < protoInputs.Count - 1; i++)
			{
				Prototype protoInput = protoInputs[i];
				Prototype protoOutput = protoInputs[i +1 ];

				if (null == protoInput || null == protoOutput || !PrototypeGraphs.AreEqual(protoInput, protoOutput))
					return false;
			}

			return true;
		}
		static public bool AreEqual(List<Prototype> protoInputs, List<Prototype> protoOutputs)
		{
			if (protoInputs.Count != protoOutputs.Count)
				throw new Exception("Input and Output lists must be the same size");

			for (int i = 0; i < protoInputs.Count; i++)
			{
				Prototype protoInput = protoInputs[i];
				Prototype protoOutput = protoOutputs[i];

				if (null == protoInput || null == protoOutput || !PrototypeGraphs.AreEqual(protoInput, protoOutput, false))
					return false;
			}

			return true;
		}

	}
}
