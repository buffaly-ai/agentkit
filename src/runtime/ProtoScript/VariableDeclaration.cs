using System.Text;

namespace ProtoScript
{
	public class VariableDeclaration : Statement
	{
		public ProtoScript.Type Type;
		public string VariableName;
		public bool IsConst;
		public bool IsExternal;
		public Expression Initializer;

		public List<VariableDeclaration> ChainedDeclarations;

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			if (IsExternal)
				sb.Append("extern ");

			if (IsConst)
				sb.Append("const ");

			sb.Append(Type.ToString());
			sb.Append(" " + VariableName);

			if (Initializer != null)
			{
				sb.Append(" = ");
				sb.Append(Initializer.ToString());
			}

			//if (oDeclaration.ChainedDeclarations != null)
			//{
			//	foreach (VariableDeclaration chained in oDeclaration.ChainedDeclarations)
			//	{
			//		Write(", ");
			//		Write(ToString(chained.VariableName));
			//		if (chained.Initializer != null)
			//		{
			//			Write(" = ");
			//			ToString(chained.Initializer);
			//		}
			//	}
			//}
			return sb.ToString();
		}
	}
}
