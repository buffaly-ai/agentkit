namespace ProtoScript.Interpretter.RuntimeInfo
{
	public class ValueRuntimeInfo : ICloneable
	{
		public int Index;
		public TypeInfo Type;
		public TypeInfo OriginalType;
		public object Value;

		virtual public ValueRuntimeInfo Clone()
		{
			ValueRuntimeInfo info = new ValueRuntimeInfo();
			info.Index = Index;
			info.Type = Type;
			info.OriginalType = OriginalType;
			info.Value = Value;
			return info;
		}

		public override string ToString()
		{
			return $"ValueRuntimeInfo:[{Index}] {Type} ({Value})";
		}

		object ICloneable.Clone()
		{
			return this.Clone();
		}
	}
}
