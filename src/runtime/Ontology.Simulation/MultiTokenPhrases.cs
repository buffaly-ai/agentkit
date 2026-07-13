namespace Ontology.Simulation
{
	public class MultiTokenPhrases
	{
		public const string PrototypeName = "MultiTokenPhrase";

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
		static public Prototype InsertMultiTokenPhrase(Collection collection, Prototype target)
		{
			string strUniqueKey = string.Join("_", collection.Children.Select(x => x.PrototypeID));
			strUniqueKey += "__" + target.PrototypeID;

			Prototype? prototype = TemporaryPrototypes.GetTemporaryPrototypeOrNull("MultiTokenPhrase#" + strUniqueKey);
			if (null == prototype)
			{
				prototype = Prototype.CreateInstance(strUniqueKey);
				prototype.Children.AddRange(collection.Children);
				prototype.Properties["MultiTokenPhrase.Field.Target"] = target;
				TemporarySequences.InsertWithChildren(prototype);
			}

			return prototype;

		}
		static public List<Prototype> GetPartOfMultiTokenPhrases(Prototype prototype)
		{
			//Get all Multi-Token Phrases for which this prototype is a part of
			List<Prototype> lstPhrases = new List<Prototype>();

			Prototype rowPrototype = Prototypes.GetPrototype(prototype.PrototypeID);
			foreach (var rowValue in rowPrototype.PartOfValues)
			{
				Prototype protoPhrase = rowValue.Key;
				if (Prototypes.TypeOf(protoPhrase, Prototype))
				{
					lstPhrases.Add(protoPhrase);
				}
			}

			return lstPhrases;
		}

		static public Prototype GetTarget(Prototype protoPhrase)
		{
			if (!Prototypes.TypeOf(protoPhrase, Prototype))
			{
				throw new Exception("The prototype is not a MultiTokenPhrase");
			}

			return protoPhrase.Properties["MultiTokenPhrase.Field.Target"];
		}

		static public List<Prototype> GetTargetsOfContainingPhrases(Prototype prototype)
		{
			//Get the Target of any Multi-Token Phrase that contains this prototype

			List<Prototype> lstTargets = new List<Prototype>();

			List<Prototype> lstPhrases = GetPartOfMultiTokenPhrases(prototype);
			foreach (Prototype protoPhrase in lstPhrases)
			{
				lstTargets.Add(GetTarget(protoPhrase));
			}

			return lstTargets;
		}

		static public List<Prototype> GetMultiTokenPhrasesTargeting(Prototype protoTarget, bool bIncludeParent)
		{
			List<Prototype> lstTargets = Prototype.GetAllDescendantsWhere(x => x.Properties["MultiTokenPhrase.Field.Target"]?.PrototypeName == protoTarget.PrototypeName).ToList();
			if (lstTargets.Count == 0 && bIncludeParent)
			{
				protoTarget = protoTarget.GetBaseType();
				lstTargets = Prototype.GetAllDescendantsWhere(x => x.Properties["MultiTokenPhrase.Field.Target"]?.PrototypeName == protoTarget.PrototypeName).ToList();
			}
			return lstTargets;
		}


	}
}
