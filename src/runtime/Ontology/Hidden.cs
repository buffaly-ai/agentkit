namespace Ontology
{
	public class Hidden
	{
	
		public class Base
		{
			public const string PrototypeName = nameof(Ontology) + "." + nameof(Hidden) + "." + nameof(Base);

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
		}

		static public Prototype GetProperty(Prototype protoHidden, string strField)
		{
			return protoHidden.Properties[protoHidden.PrototypeName + ".Field." + strField];
		}

		static public Prototype CreateHiddenInstance(Prototype protoHidden, List<Prototype> lstEntities)
		{
			Prototype protoHiddenInstance = protoHidden.CreateInstance();
			protoHiddenInstance.InsertTypeOf(Base.Prototype);

			for (int i = 0; i < lstEntities.Count; i++)
			{
				Prototype protoProperty = Prototypes.GetOrInsertPrototype(protoHidden.PrototypeName + ".Field." + i);
				protoHiddenInstance.Properties[protoProperty.PrototypeID] = lstEntities[i];
			}

			return protoHiddenInstance;
		}

	}
}
