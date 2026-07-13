namespace Ontology
{
	public class Compare
	{
		public class Comparison
		{
			public const string PrototypeName = nameof(Compare) + "." + nameof(Comparison);

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

		public class Ignore
		{
			public const string PrototypeName = nameof(Compare) + "." + nameof(Ignore);

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

		public class Exact
		{
			public const string PrototypeName = nameof(Compare) + "." + nameof(Exact);

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

		public class Remove
		{
			public const string PrototypeName = nameof(Compare) + "." + nameof(Remove);

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

		public class Intersection
		{
			public const string PrototypeName = nameof(Compare) + "." + nameof(Intersection);

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

		public class StartsWith
		{
			public const string PrototypeName = nameof(Compare) + "." + nameof(StartsWith);

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

		public class Entities
		{
			public const string PrototypeName = nameof(Compare) + "." + nameof(Entities);

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

		public class Entity
		{
			public const string PrototypeName = nameof(Compare) + "." + nameof(Entity);

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

			public static Prototype Create(Prototype protoExpected)
			{
				Prototype prototype = Entity.Prototype.Clone();
				prototype.Properties[Compare.Entity.PrototypeID] = protoExpected;
				return prototype;
			}

			public static Prototype GetExpectation(Prototype protoEntity)
			{
				return protoEntity.Properties[Compare.Entity.PrototypeID];
			}
		}
	}
}
