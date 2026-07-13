namespace Ontology.BaseTypes
{
	public class System_Int32
	{
		public const string PrototypeName = "System.Int32";

		public static int PrototypeID => Prototype.PrototypeID;

		private static readonly ResettablePrototypeAsyncLocal s_prototype = new();

		public static Prototype Prototype
		{
			get
			{
				if (s_prototype.Value == null)
					s_prototype.Value = Prototypes.GetOrInsertPrototype(PrototypeName);

				return s_prototype.Value;
			}
		}
	}

	public class System_String
	{
		public const string PrototypeName = "System.String";

		public static int PrototypeID => Prototype.PrototypeID;

		private static readonly ResettablePrototypeAsyncLocal s_prototype = new();

		public static Prototype Prototype
		{
			get
			{
				if (s_prototype.Value == null)
					s_prototype.Value = Prototypes.GetOrInsertPrototype(PrototypeName);

				return s_prototype.Value;
			}
		}
	}

	public class System_Double
	{
		public const string PrototypeName = "System.Double";

		public static int PrototypeID => Prototype.PrototypeID;

		private static readonly ResettablePrototypeAsyncLocal s_prototype = new();

		public static Prototype Prototype
		{
			get
			{
				if (s_prototype.Value == null)
					s_prototype.Value = Prototypes.GetOrInsertPrototype(PrototypeName);

				return s_prototype.Value;
			}
		}
	}

	public class System_Boolean
	{
		public const string PrototypeName = "System.Boolean";

		public static int PrototypeID => Prototype.PrototypeID;

		private static readonly ResettablePrototypeAsyncLocal s_prototype = new();

		public static Prototype Prototype
		{
			get
			{
				if (s_prototype.Value == null)
					s_prototype.Value = Prototypes.GetOrInsertPrototype(PrototypeName);

				return s_prototype.Value;
			}
		}
	}
}
