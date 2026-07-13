//added
using Ontology;
using ProtoScript.Interpretter.Symbols;

namespace ProtoScript.Interpretter.RuntimeInfo
{
	public class PrototypeTypeInfo : TypeInfo
	{
		public Prototype Prototype;
		public Scope Scope;
		public bool IsGeneric = false;
		public PrototypeTypeInfo Generic = null;

		//N20220602-02
		public Prototype PrimaryParent = null;

		public PrototypeTypeInfo() : base(typeof(Prototype))
		{
		}

		public override string ToString()
		{
			return $"PrototypeTypeInfo[{Prototype?.PrototypeName}]";

		}

		public override string ToShortString()
		{
			return this.Prototype?.PrototypeName;
		}

		public override TypeInfo Clone()
		{
			PrototypeTypeInfo prototypeTypeInfo = new PrototypeTypeInfo();
			prototypeTypeInfo.Index = this.Index;
			prototypeTypeInfo.Prototype = this.Prototype;
			prototypeTypeInfo.Scope = Scope;

			return prototypeTypeInfo as TypeInfo;
		}
	}
}
