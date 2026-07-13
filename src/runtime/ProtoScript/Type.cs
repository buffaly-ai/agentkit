using System.Text;

namespace ProtoScript
{
	public class Type : Expression
	{
		public string TypeName;
		public List<ProtoScript.Type> ElementTypes = new List<ProtoScript.Type>();
		public bool IsNullable = false;
		public bool IsArray = false;
		public Expression ArraySize = null;
		public bool IsGeneric
		{
			get
			{
				return this.ElementTypes.Count > 0;
			}
		}

		
		public Type()
		{
			this.Terms = null;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(TypeName);
			if (IsNullable)
				sb.Append("?");
			if (ElementTypes.Count > 0)
			{
				sb.Append("<");
				for (int i = 0; i < ElementTypes.Count; i++)
				{
					if (i > 0)
						sb.Append(", ");

					sb.Append(ElementTypes[i].ToString());
				}

				sb.Append(">");
			}

			if (IsArray)
			{
				foreach (Expression expression in ArraySize.Terms)
				{
					sb.Append("[");
					if (null != expression)
						sb.Append(ArraySize);
					sb.Append("]");
				}
			}

			return sb.ToString();
		}

		public string GetNonGenericName()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(TypeName);
			if (ElementTypes.Count > 0)
			{
				sb.Append("<");
				for (int i = 0; i < ElementTypes.Count; i++)
				{
					if (i > 0)
						sb.Append(", ");

					sb.Append(ElementTypes[i].GetNonGenericName());
				}

				sb.Append(">");
			}
			return sb.ToString();
		}
	

		public bool IsEqual(Type type)
		{
			if (null != this && null == type)
				return false;

			if (type.TypeName != TypeName)
				return false;

			if (null != ElementTypes && type.ElementTypes.Count != ElementTypes.Count)
				return false;

			for (int i = 0; i < type.ElementTypes.Count; i++)
			{
				if (!type.ElementTypes[i].IsEqual(ElementTypes[i]))
					return false;
			}
			
			if (type.IsNullable != IsNullable)
				return false;

			return true;
		}
	}
}
