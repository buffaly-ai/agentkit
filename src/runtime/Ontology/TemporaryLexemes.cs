using BasicUtilities.Collections;

namespace Ontology
{
	public class TemporaryLexeme : Prototype
	{
		private TemporaryLexeme()
		{
		}
		public TemporaryLexeme(string strLexeme)
		{
			this.PrototypeName = "Lexeme." + Lexemes.ToPrototypeName(strLexeme);
			this.PrototypeID = TemporaryPrototypes.GetOrInsertPrototype(this).PrototypeID;
			this.InsertTypeOf(Ontology.Lexeme.Prototype);
			this.Lexeme = strLexeme;
		}
		public string Lexeme { get; set; }

		protected FastPrototypeMap<double>? m_mapLexemePrototypes = null;
		public FastPrototypeMap<double> LexemePrototypes
		{
			get
			{
				if (null == m_mapLexemePrototypes)
					m_mapLexemePrototypes = new FastPrototypeMap<double>();

				return m_mapLexemePrototypes;
			}
		}

		public override Prototype ShallowClone()
		{
			TemporaryLexeme prototype = new TemporaryLexeme();

			PopulateClone(prototype);
			prototype.m_mapLexemePrototypes = this.m_mapLexemePrototypes;
			prototype.Lexeme = this.Lexeme;

			return prototype;
		}
		public override Prototype Clone()
		{
			//Lexeme's don't need to be deep cloned, so we can just return a shallow clone
			return ShallowClone();
		}
	}

	public class TemporaryLexemes
	{

		static public ObjectCache Cache
		{
			get
			{
				return ObjectCacheManager.Instance.GetOrCreateCache(nameof(TemporaryLexemes));
			}
		}
		static public ObjectCache m_mapRelatedParentPrototypes
		{
			get
			{
				return ObjectCacheManager.Instance.GetOrCreateCache(nameof(TemporaryLexemes) + ".RelatedParentPrototypes");
			}
		}

		public static TemporaryLexeme? GetLexemeByLexeme(string Lexeme)
		{
			return Cache.Get<TemporaryLexeme>(Lexeme);

		}

		static public void CacheRelatedPrototype(Prototype protoRelated, TemporaryLexeme protoLexeme)
		{
			int iRelatedPrototypeID = protoRelated.GetBaseType().PrototypeID;
			Set<int>? dtLexemePrototypes = m_mapRelatedParentPrototypes.Get<Set<int>>(iRelatedPrototypeID.ToString());
			if (dtLexemePrototypes is null)
			{
				dtLexemePrototypes = new Set<int>();
				m_mapRelatedParentPrototypes.Insert(dtLexemePrototypes, iRelatedPrototypeID.ToString());
			}
			dtLexemePrototypes.Add(protoLexeme.PrototypeID);
		}

		static public Prototype GetOrInsertLexeme(string strLexeme, Prototype protoRelated)
		{
			TemporaryLexeme? rowExisting = TemporaryLexemes.GetLexemeByLexeme(strLexeme);
			if (null == rowExisting)
			{
				rowExisting = new TemporaryLexeme(strLexeme);
				Cache.Insert(rowExisting, strLexeme);
			}

			rowExisting.LexemePrototypes.TryAdd(protoRelated, 1.0);
			CacheRelatedPrototype(protoRelated, rowExisting);

			return rowExisting;
		}

		static public Prototype GetOrInsertLexeme(string strLexeme)
		{
			//N20220610-03 - Need to check lexemes because PrototypeName is case sensitive 
			//but lexemes are not
			TemporaryLexeme? rowExisting = TemporaryLexemes.GetLexemeByLexeme(strLexeme);
			if (null == rowExisting)
			{
				rowExisting = new TemporaryLexeme(strLexeme);
				Cache.Insert(rowExisting, strLexeme);
			}

			return rowExisting;
		}


		static public void InsertRelatedPrototype(Prototype protoLexeme, Prototype protoRelated, bool bFailOnDuplicate)
		{
			TemporaryLexeme? rowExisting = TemporaryLexemes.GetLexemeByLexeme(protoLexeme.PrototypeName);
			if (null == rowExisting)
			{
				throw new Exception($"Lexeme {protoLexeme.PrototypeName} does not exist in cache");
			}

			if (null == protoRelated)
			{
				throw new Exception("Cannot insert related prototype for null prototype");
			}

			bool bAdded = rowExisting.LexemePrototypes.TryAdd(protoRelated, 1.0);

			if (bAdded)
			{
				CacheRelatedPrototype(protoRelated, rowExisting);
			}

			else if (bFailOnDuplicate)
			{
				throw new Exception($"Lexeme prototype already exists {protoLexeme.PrototypeName} -> {protoRelated.PrototypeName}");
			}
		}

		//static public Prototype InsertMultiTokenLexeme(string strMultiToken, Prototype protoRelated)
		//{
		//	//N20210512-01
		//	Prototype protoLexeme = Tokenize(strMultiToken);

		//	for (int i = 0; i < protoLexeme.Children.Count; i++)
		//	{
		//		Prototype child = protoLexeme.Children[i];
		//		string strValue = NativeValuePrototypes.FromPrototype(child) as string;
		//		protoLexeme.Children[i] = TemporaryLexemes.GetOrInsertLexeme(strValue);
		//	}

		//	string strHash = PrototypeGraphs.GetHash(protoLexeme);

		//	Prototype protoHash = TemporaryPrototypes.GetOrCreateTemporaryPrototype("Lexeme." + strHash);
		//	protoHash.Children.AddRange(protoLexeme.Children);

	
		//	Prototype protoLexemePrototype = TemporaryLexemes.GetOrInsertLexeme(strMultiToken);

		//	protoHash.Properties[Lexeme.PrototypeID] = protoLexemePrototype;

		//	LexemePrototypesRow rowLexemePrototype = new LexemePrototypesRow();
		//	rowLexemePrototype.PrototypeID = protoLexemePrototype.PrototypeID;
		//	rowLexemePrototype.RelatedPrototypeID = protoRelated.PrototypeID;
		//	rowLexemePrototype.Value = 1; //TODO
		//	LexemesRow rowLexeme = Lexemes.GetLexemeByPrototypeID(protoLexemePrototype.PrototypeID);
		//	rowLexeme.LexemePrototypes.Add(rowLexemePrototype);

		//	CacheRelatedPrototype(protoRelated, rowLexemePrototype);

		//	return protoHash;
		//}

		static public List<TemporaryLexeme> GetLexemesByRelatedPrototype(Prototype protoRelated, bool bAllowedDerivedTypes = true)
		{
			//Note: Both this method and GetLexemesByRelatedPrototypeParent should respect the instance
			//but that method was written before we created the m_mapRelatedParentPrototypes cache
			Set<int>? dtLexemePrototypes = m_mapRelatedParentPrototypes.Get<Set<int>>(protoRelated.PrototypeID.ToString());
			List<TemporaryLexeme> collection = new List<TemporaryLexeme>();

			if (null == dtLexemePrototypes)
			{
				return collection;
			}

			foreach (int iPrototypeID in dtLexemePrototypes)
			{
				//if (!bAllowedDerivedTypes && !Prototypes.TypeOf(protoRelated, rowLexemePrototype.RelatedPrototype))
				//	continue;

				//This does not filter by singular vs plural
				collection.Add((TemporaryLexeme)TemporaryPrototypes.GetTemporaryPrototypeOrNull(iPrototypeID));
			}

			return collection;
		}

		static public Prototype Tokenize(string strInput)
		{
			Prototype prototype = new Ontology.Collection();
			foreach (string strToken in Lexemes.Split(strInput))
			{
				prototype.Children.Add(NativeValuePrototype.GetOrCreateNativeValuePrototype(strToken).ShallowClone());
			}

			return prototype;
		}


	}
}

