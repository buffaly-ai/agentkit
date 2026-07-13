namespace ProtoScript.Interpretter.Compiled
{
	public class DotNetNewInstance : Expression
	{
		public class CollectionInitializer
		{
			public Compiled.Expression Value;
			public StatementParsingInfo Info;
		}

		public class MemberInitializer
		{
			public string Name = string.Empty;
			public Compiled.Expression Value;
			public StatementParsingInfo Info;
		}


		public System.Reflection.ConstructorInfo Constructor;
		public List<Compiled.Expression> Parameters = new List<Expression>();
		public List<CollectionInitializer> CollectionInitializers = new List<CollectionInitializer>();
		public List<MemberInitializer> MemberInitializers = new List<MemberInitializer>();
	}
}

