namespace ProtoScript.Interpretter.RuntimeInfo
{
	public class TypeInfo : ICloneable
	{
		public System.Type Type;
		public int Index;

		public TypeInfo(System.Type type)
		{
			Type = type;
		}

		public virtual TypeInfo Clone()
		{
			TypeInfo typeInfo = new TypeInfo(this.Type);
			return typeInfo;
		}

		object ICloneable.Clone()
		{
			return this.Clone();
		}

		public override string ToString()
		{
			return $"TypeInfo[" + this.Type?.Name + "]";
		}

		public virtual string ToShortString()
		{
			return this.Type?.Name;
		}
	}
}
