//added
namespace ProtoScript.Interpretter.RuntimeInfo
{
	public class GenericTypeInfo : TypeInfo
	{
		public GenericTypeInfo() : base(typeof(GenericTypeInfo))
		{
		}

		public override string ToString()
		{
			return $"GenericTypeInfo[]";

		}

		public override TypeInfo Clone()
		{
			GenericTypeInfo prototypeTypeInfo = new GenericTypeInfo();
			prototypeTypeInfo.Index = this.Index;
			return prototypeTypeInfo as TypeInfo;
		}
	}
}
