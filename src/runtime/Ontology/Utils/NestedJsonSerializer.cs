//added
using BasicUtilities;
using BasicUtilities.Collections;
using System.Reflection;
using System.Text;

namespace Ontology.Utils
{
	//Use this class to correctly serialize Prototypes to JSON when the prototype is a property 
	//Normal JSON serialization won't look for serializers below the top level 
	public class NestedJsonSerializer<T> : IJsonSerializer
	{
		public object FromJsonObject(System.Type t, JsonObject jsonObject)
		{
			//Note: This was generated during upgrade but not tested
			return JsonUtil.ConvertType(t, jsonObject);
		}
		public object FromJsonObject(JsonObject jsonObject)
		{
			return JsonUtil.ConvertType(typeof(T), jsonObject);
		}

		public StringBuilder ToJSON(object obj)
		{
			StringBuilder str = new StringBuilder();
			str.Append("{");

			bool bFirst = true;
			foreach (FieldInfo field in typeof(T).GetFields())
			{
				if (bFirst)
					bFirst = false;
				else
					str.Append(",");

				str.Append("\"").Append(JsonUtil.ToSafeString(field.Name)).Append("\":");
				object oVal = field.GetValue(obj);

				if (null == oVal)
					str.Append("null");

				else
				{
					IJsonSerializer serializer = JsonSerializers.GetSerializer(field.FieldType);
					if (null != serializer)
						str.Append(serializer.ToJSON(oVal));
					else
						str.Append(JsonUtil.ToStringExt(oVal));
				}
			}
			str.Append("}");

			return str;
		}

		public StringBuilder ToJSON(object obj, Set<int> setSerialized)
		{
			//Note: This was generated during upgrade but not tested

			StringBuilder str = new StringBuilder();
			str.Append("{");

			bool bFirst = true;
			foreach (FieldInfo field in typeof(T).GetFields())
			{
				if (bFirst)
					bFirst = false;
				else
					str.Append(",");

				str.Append("\"").Append(JsonUtil.ToSafeString(field.Name)).Append("\":");
				object oVal = field.GetValue(obj);

				if (null == oVal)
					str.Append("null");

				else
				{
					IJsonSerializer serializer = JsonSerializers.GetSerializer(field.FieldType);
					if (null != serializer)
						str.Append(serializer.ToJSON(oVal, setSerialized));
					else
						str.Append(JsonUtil.ToStringExt(oVal, setSerialized));
				}
			}
			str.Append("}");

			return str;
		}
	}

	public class ListOfNestedJsonSerializer<T> : IJsonSerializer
	{
		public object FromJsonObject(JsonObject jsonObject)
		{
			return JsonUtil.ConvertType(typeof(T), jsonObject);
		}

		public StringBuilder ToJSON(object obj)
		{
			List<T> lst = obj as List<T>;

			StringBuilder str = new StringBuilder();
			str.Append("[");
			NestedJsonSerializer<T> nested = new NestedJsonSerializer<T>();

			bool bFirst = true;

			foreach (T val in lst)
			{
				if (bFirst)
					bFirst = false;
				else
					str.Append(",");

				str.Append(nested.ToJSON(val));
			}
			str.Append("]");
			return str;
		}

		public StringBuilder ToJSON(object obj, Set<int> setSerialized)
		{
			//Note: This was generated during upgrade but not tested
			List<T> lst = obj as List<T>;

			StringBuilder str = new StringBuilder();
			str.Append("[");
			NestedJsonSerializer<T> nested = new NestedJsonSerializer<T>();

			bool bFirst = true;

			foreach (T val in lst)
			{
				if (bFirst)
					bFirst = false;
				else
					str.Append(",");

				str.Append(nested.ToJSON(val, setSerialized));
			}
			str.Append("]");
			return str;
		}

		public object? FromJsonObject(System.Type t, JsonObject jsonObject)
		{
			return JsonUtil.ConvertType(t, jsonObject);
		}
	}
}
