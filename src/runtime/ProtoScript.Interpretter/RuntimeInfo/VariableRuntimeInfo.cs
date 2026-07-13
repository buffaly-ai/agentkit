namespace ProtoScript.Interpretter.RuntimeInfo
{
	public class VariableRuntimeInfo : ValueRuntimeInfo
	{
		public override ValueRuntimeInfo Clone()
		{
			VariableRuntimeInfo info = new VariableRuntimeInfo();
			info.Index = Index;
			info.Type = Type;
			info.OriginalType = OriginalType;
			info.Value = Value;
			return info;
		}

		public override string ToString()
		{
			return $"VariableRuntimeInfo:[{Index}] {Type} ({Value})";
		}
	}
}
