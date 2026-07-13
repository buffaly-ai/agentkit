namespace ProtoScript.Interpretter.RuntimeInfo
{
	public class ParameterRuntimeInfo : ValueRuntimeInfo
	{
		public string ParameterName;			//For suggestions
		public override ValueRuntimeInfo Clone()
		{
			ParameterRuntimeInfo info = new ParameterRuntimeInfo();
			info.Index = Index;
			info.Type = Type;
			info.OriginalType = OriginalType;
			info.Value = Value;
			info.ParameterName = ParameterName;
			return info;
		}

		public override string ToString()
		{
			return $"ParameterRuntimeInfo:[{Index}] {Type} ({Value})";
		}
	}
}
