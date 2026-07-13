namespace ProtoScript
{
	public class EnumDefinition : Statement
	{
		public Visibility Visibility;
		public string EnumName;
		public List<string> EnumTypes = new List<string>();

		public void SetModifiers(Modifiers modifiers)
		{
			this.Visibility = modifiers.Visibility;
		}
	}
}
