//added
using Ontology;
using ProtoScript.Interpretter.RuntimeInfo;

namespace ProtoScript.Interpretter.Compiled
{
	public class NewInstance : Expression
	{
		public class ObjectInitializer
		{
			public Prototype Property;
			public Compiled.Expression Value;
		}

		public FunctionRuntimeInfo Constructor = null;
		public List<ObjectInitializer> Initializers = new List<ObjectInitializer>();
		public List<Compiled.Expression> Parameters = new List<Expression>();
	}
}
