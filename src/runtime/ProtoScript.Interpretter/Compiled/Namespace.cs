//added
using ProtoScript.Interpretter.Symbols;
using ProtoScript.Interpretter.RuntimeInfo;

namespace ProtoScript.Interpretter.Compiled
{
	public class Namespace : TypeInfo
	{
		public Scope Scope = new Scope(Scope.ScopeTypes.Namespace);
		public List<Compiled.Statement> Statements;

		public string NamespaceName;
		public Namespace ParentNamespace;

		public Namespace() : base(typeof(Namespace))
		{

		}

		public string FullNamespaceName
		{
			get
			{
				if (null == this.ParentNamespace)
					return this.NamespaceName;
				else
					return this.ParentNamespace.FullNamespaceName + "." + this.NamespaceName;
			}
		}

	}
}
