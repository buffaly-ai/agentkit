namespace ProtoScript.Interpretter.RuntimeInfo
{
	public class FieldTypeInfo : PrototypeTypeInfo
	{
		public Compiled.Expression Initializer;
		public TypeInfo FieldInfo;

		public override TypeInfo Clone()
		{
			FieldTypeInfo fieldTypeInfo = new FieldTypeInfo();
			fieldTypeInfo.FieldInfo = this.FieldInfo;
			fieldTypeInfo.Index = this.Index;
			fieldTypeInfo.Initializer = this.Initializer;
			fieldTypeInfo.Prototype = this.Prototype;
			fieldTypeInfo.Scope = this.Scope;
			fieldTypeInfo.Type = this.Type;
			return fieldTypeInfo as TypeInfo;
		}
	}
}
