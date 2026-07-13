namespace ProtoScript.Interpretter.RuntimeInfo
{
	public class DotNetTypeInfo : TypeInfo
	{
		public DotNetTypeInfo(System.Type type) : base(type)
		{

		}

		public override TypeInfo Clone()
		{
			DotNetTypeInfo dotNetTypeInfo = new DotNetTypeInfo(this.Type);
			dotNetTypeInfo.Index = this.Index;
			return dotNetTypeInfo as TypeInfo;
		}


	}
}
