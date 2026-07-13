using BasicUtilities;
using BasicUtilities.Collections;
using Ontology.BaseTypes;
using Ontology.Utils;
using System.Text;

namespace Ontology
{
	public class PrototypeLogging
	{
		static public void Log(Prototype prototype)
		{
			LogPrototypeShort(prototype);
		}

		static public void Log(IEnumerable<Prototype> prototypes)
		{
			LogPrototypesShort(prototypes);
		}

		static public void Log(Collection prototypes)
		{
			LogPrototypesShort((IEnumerable <Prototype>)prototypes);
		}


		static public void Log(ConcurrentList lst)
		{
			foreach (var pair in lst)
			{
				PrototypeLogging.LogPrototypeShort(pair.Item1);
				PrototypeLogging.LogPrototypeShort(pair.Item2);
			}
		}

		static public void LogPrototype(Prototype prototype)
		{
			Logs.DebugLog.WriteEvent("Prototype", ToFormattedString(prototype.ToFriendlyJsonObject()).ToString());
		}

		static public void LogPrototypeShort(Prototype prototype)
		{
			Logs.DebugLog.WriteEvent("Prototype", "\r\n" + ToFriendlyString(prototype).ToString());
		}

		static public void LogPrototypesShort(IEnumerable<Prototype> prototypes)
		{
			int i = 0;

			foreach (Prototype prototype in prototypes)
			{
				Logs.DebugLog.WriteEvent("Prototype " + i++ + "\r\n", ToFriendlyString(prototype).ToString());
			}
		}

		static public string ToChildString(Prototype prototype)
		{
			if (null == prototype)
				return "(null)";

			if (prototype.Children.Count == 0)
			{
				if (Prototypes.TypeOf(prototype, Lexeme.Prototype))
					return "L." + StringUtil.RightOfFirst(prototype.PrototypeName, "Lexeme.");
				else if (Prototypes.TypeOf(prototype, System_String.Prototype))
					return "S." + StringUtil.Between(prototype.PrototypeName, "[", "]");
				else
					return prototype.ToString();
			}

			StringBuilder sb = new StringBuilder();
			bool bFirst = true;
			foreach (Prototype child in prototype.Children)
			{
				if (bFirst)
					bFirst = false;
				else
					sb.Append("|");

				if (Prototypes.TypeOf(child, Lexeme.Prototype))
					sb.Append("L.").Append(StringUtil.RightOfFirst(child.PrototypeName, "Lexeme."));
				else if (Prototypes.TypeOf(child, System_String.Prototype))
					sb.Append("S.").Append(StringUtil.Between(child.PrototypeName, "[", "]"));
				else
					sb.Append(child.PrototypeName);
			}

			return sb.ToString();
		}

		static public string ToChildString(Prototype prototype, int iHighlight)
		{
			if (null == prototype)
				return "(null)";

			StringBuilder sb = new StringBuilder();
			bool bFirst = true;
			for (int i = 0; i < prototype.Children.Count; i++)
			{
				Prototype child = prototype.Children[i];

				if (i > 0)
					sb.Append("|");

				if (i == iHighlight)
					sb.Append("<").Append(child.PrototypeName).Append(">");
				else 
					sb.Append(child.PrototypeName);
			}

			return sb.ToString();
		}

	
		static public string ToFormattedString2(string strJSON)
		{
			Prototype prototype = Prototype.FromJSON(strJSON);
			return ToFriendlyString(prototype).ToString();
		}

		static public StringBuilder ToFormattedString(JsonValue jsonValue, int iLevels = 0)
		{
			StringBuilder sb = new StringBuilder();

			if (jsonValue.ToJsonObject() != null)
			{
				JsonObject jsonObject = jsonValue.ToJsonObject();

				sb.Append("{");

				iLevels++;

				bool bFirst = true; 
				foreach (var pair in jsonObject)
				{
					if (bFirst)
						bFirst = false;
					else
						sb.Append(",");
							
					sb.AppendLine().Append(new string('\t', iLevels));

					sb.Append("'").Append(pair.Key).Append("':").Append(ToFormattedString(pair.Value, iLevels));
				}

				iLevels--;

				sb.AppendLine();

				if (iLevels > 0)
					sb.Append(new string('\t', iLevels));

				sb.Append("}");
			}

			else if (jsonValue.ToJsonArray() != null)
			{
				JsonArray jsonArray = jsonValue.ToJsonArray();


				sb.Append("[");

				iLevels++;

				bool bFirst = true; 
				foreach (var val in jsonArray)
				{
					if (bFirst)
						bFirst = false;
					else
						sb.Append(",").AppendLine().Append(new string('\t', iLevels));

					sb.Append(ToFormattedString(val, iLevels));
				}

				iLevels--;


				sb.Append("]");
			}
			else
			{
				sb.Append("'").Append(JsonUtil.ToSafeString(jsonValue.ToString())).Append("'");
			}

			return sb;
		}

		static public StringBuilder ToFriendlyPathString(Prototype path)
		{
			StringBuilder sb = new StringBuilder();
			 if (path.PrototypeID == Ontology.Collection.PrototypeID)
			{

			}
			else
				sb.Append("(").Append(path.PrototypeName).Append(")");

			foreach (var pair in path.Properties)
			{
				Prototype protoName = Prototypes.GetPrototype(pair.Key);
				string strName = (protoName.PrototypeName.Contains(".") ? StringUtil.RightOfLast(protoName.PrototypeName, ".") : protoName.PrototypeName);
				if (strName.EndsWith("]"))
					strName = StringUtil.LeftOfLast(strName, "]");

				sb.Append(".").Append(strName).Append(".").Append(ToFriendlyPathString(pair.Value));
			}

			for (int i = 0; i < path.Children.Count; i++)
			{
				Prototype child = path.Children[i];
				if (child.PrototypeID != Compare.Ignore.PrototypeID)
					sb.Append("[").Append(i).Append("]").Append(".").Append(ToFriendlyPathString(child));
			}

			return sb;
		}

		
		static public string ToFriendlyString2(Prototype prototype)
		{
			return ToFriendlyString(prototype).ToString(); 
		}

		static public bool IncludeLexemes = false;
		static public bool IncludeTypeOfs = true;
		static public bool IncludePrototypeIDs = true;
		static public bool IncludeValues = false;

		static private Set<int> m_setCircular = new Set<int>();
		static public StringBuilder ToFriendlyString(Prototype prototype)
		{
			m_setCircular.Clear();

			StringBuilder sb = ToFriendlyShadowString(prototype, "");

			m_setCircular.Clear();
			return sb;
		}

		static public StringBuilder ToFriendlyShadowString(Prototype prototype, int iLevels)
		{
			StringBuilder sb = new StringBuilder();
			if (null == prototype)
			{
				sb.Append("null");
				return sb;
			}

			if (m_setCircular.Contains(prototype.GetHashCode()))
			{
				sb.Append(prototype.PrototypeName);
				return sb;
			}

			m_setCircular.Add(prototype.GetHashCode());
			

			//This should not be on a new line
			//sb.Append(new string('\t', iLevels));
			sb.Append(prototype.PrototypeName);
			if (IncludePrototypeIDs)
				sb.Append(" (").Append(prototype.PrototypeID).Append(")");

			if (IncludeValues)
				sb.Append(" ").Append(prototype.Value);

			iLevels++;
			foreach (var pair in prototype.Properties)
			{
				Prototype protoName = Prototypes.GetPrototype(pair.Key);

				sb.AppendLine();

				string strName = null;

				if (protoName.PrototypeName.StartsWith(prototype.PrototypeName + "."))
				{
					if (StringUtil.InString(protoName.PrototypeName, ".Field."))
					{
						strName = "." + StringUtil.RightOfFirst(protoName.PrototypeName, prototype.PrototypeName + ".Field.");
					}
					else if (StringUtil.InString(protoName.PrototypeName, ".Property."))
					{
						strName = "." + StringUtil.RightOfFirst(protoName.PrototypeName, prototype.PrototypeName + ".Property.");
					}

					else
					{
						strName = protoName.PrototypeName;
					}
				}
				else
				{
					strName = protoName.PrototypeName;
				}

				sb.Append(new string('\t', iLevels));
				sb.Append(strName).Append(" = ");
				sb.Append(ToFriendlyShadowString(pair.Value, iLevels));
			}

			if (IncludeLexemes || Prototypes.TypeOf(prototype, Ontology.Collection.Prototype))
			{
				for (int i = 0; i < prototype.Children.Count; i++)
				{
					sb.AppendLine();
					sb.Append(new string('\t', iLevels));
					sb.Append("[").Append(i).Append("] = ");
					sb.Append(ToFriendlyShadowString(prototype.Children[i], iLevels));
				}
			}


			iLevels--;

			return sb;
		}

		static public StringBuilder ToFriendlyShadowString(Prototype prototype, string prefix = "")
		{
			StringBuilder sb = new StringBuilder();
			if (null == prototype)
			{
				sb.Append("null");
				return sb;
			}

			if (Prototypes.TypeOf(prototype, System_String.Prototype))
				sb.Append("\"").Append(StringUtil.Between(prototype.PrototypeName, "[", "]")).Append("\"");
			else if (Prototypes.TypeOf(prototype, System_Int32.Prototype) || Prototypes.TypeOf(prototype, System_Boolean.Prototype) || Prototypes.TypeOf(prototype, System_Double.Prototype))
				sb.Append(StringUtil.Between(prototype.PrototypeName, "[", "]"));
			else
				sb.Append(prototype.PrototypeName);

			if (m_setCircular.Contains(prototype.GetHashCode()))
			{
				return sb;
			}

			m_setCircular.Add(prototype.GetHashCode());

		
			if (IncludePrototypeIDs)
				sb.Append(" (").Append(prototype.PrototypeID).Append(")");

			if (IncludeValues)
				sb.Append(" ").Append(prototype.Value);

			if (IncludeTypeOfs)
			{
				var typeOfs = prototype.Ancestors;
				if (typeOfs.Any())
				{
					sb.Append(":");
					sb.Append(" [").Append(String.Join(", ", typeOfs.Select(x => x.PrototypeName))).Append("]");
				}
			}

			// Process properties
			var propertyList = prototype.Properties.ToList();
			for (int i = 0; i < propertyList.Count; i++)
			{
				var pair = propertyList[i];
				Prototype protoName = Prototypes.GetPrototype(pair.Key);

				bool isLast = (i == propertyList.Count - 1);
				string connector = isLast ? "└──" : "├──";

				sb.AppendLine();
				sb.Append(prefix).Append(connector).Append(" ");

				string strName = null;

				//Always use friendly name here: 
				strName = StringUtil.RightOfLast(protoName.PrototypeName, ".");

				sb.Append(strName).Append(" = ");

				{
					// Recurse with the correct prefix for the value
					string newPrefix = prefix + (isLast ? "    " : "│   ");
					sb.Append(ToFriendlyShadowString(pair.Value, newPrefix).ToString().TrimStart());
				}
			}

			// Process children if conditions are met
			if (IncludeLexemes 
				|| Prototypes.TypeOf(prototype, Ontology.Collection.Prototype)
				|| Prototypes.TypeOf(prototype, Ontology.Sequence.Prototype))
			{
				for (int i = 0; i < prototype.Children.Count; i++)
				{
					bool isLast = (i == prototype.Children.Count - 1);
					string connector = isLast ? "└──" : "├──";

					sb.AppendLine();
					sb.Append(prefix).Append(connector).Append(" [").Append(i).Append("] = ");

					string newPrefix = prefix + (isLast ? "    " : "│   ");
					sb.Append(ToFriendlyShadowString(prototype.Children[i], newPrefix).ToString().TrimStart());
				}
			}

			return sb;
		}


		// Configuration class to hold boolean settings
		public class Configuration
		{
			public bool IncludeLexemes = false;
			public bool IncludeTypeOfs = true;
			public bool IncludePrototypeIDs = true;
			public bool IncludeValues = false;
		}


		public static JsonObject ToFriendlyJsonObject(Prototype prototype, Configuration config = null)
		{
			config = config ?? new Configuration();
			HashSet<int> setCircular = new HashSet<int>();
			JsonObject result = ToFriendlyJsonObject(prototype, setCircular, config);
			setCircular.Clear();
			return result;
		}

		private static JsonValue ToFriendlyJsonObject(Prototype prototype, HashSet<int> setCircular, Configuration config)
		{
			if (null == prototype)
			{
				return null;
			}

			if (prototype is NativeValuePrototype nvp)
			{
				return new JsonValue(nvp.NativeValue);
			}

			if (setCircular.Contains(prototype.GetHashCode()))
			{
				return new JsonObject { [nameof(prototype.PrototypeName)] = prototype.PrototypeName };
			}

			setCircular.Add(prototype.GetHashCode());

			// Start the JSON object
			JsonObject jsonPrototype = new JsonObject();
			jsonPrototype[nameof(prototype.PrototypeName)] = prototype.PrototypeName;

			string strPrototypeName = prototype.PrototypeName.Contains("#") ? StringUtil.LeftOfFirst(prototype.PrototypeName, "#") : prototype.PrototypeName;

			// Include PrototypeID if specified in config
			if (config.IncludePrototypeIDs)
			{
				jsonPrototype[nameof(prototype.PrototypeID)] = prototype.PrototypeID;
			}

			// Add Value if specified in config
			if (config.IncludeValues)
			{
				jsonPrototype[nameof(prototype.Value)] = prototype.Value;
			}

			// Process properties directly under the root
			foreach (var pair in prototype.Properties)
			{
				Prototype protoName = Prototypes.GetPrototype(pair.Key);

				//if (protoName.PrototypeID == TypeOf.PrototypeID)
				//{
				//	if (config.IncludeTypeOfs)
				//	{
				//		if (prototype.GetTypeOfs().Count > 0)
				//		{
				//			JsonArray jsonArray = new JsonArray(prototype.GetTypeOfs().Select(x => x.PrototypeName));
				//			jsonPrototype["TypeOfs"] = jsonArray;
				//		}
				//	}

				//	continue;
				//}

				string strName = null;
				if (protoName.PrototypeName.StartsWith(strPrototypeName + "."))
				{
					if (StringUtil.InString(protoName.PrototypeName, ".Field."))
					{
						strName = StringUtil.RightOfFirst(protoName.PrototypeName, strPrototypeName + ".Field.");
					}
					else if (StringUtil.InString(protoName.PrototypeName, ".Property."))
					{
						strName = StringUtil.RightOfFirst(protoName.PrototypeName, strPrototypeName + ".Property.");
					}
					else
					{
						strName = protoName.PrototypeName;
					}
				}
				else
				{
					strName = protoName.PrototypeName;
				}

				jsonPrototype[strName] = pair.Value != null ? ToFriendlyJsonObject(pair.Value, setCircular, config) : null;
			}

			// Process children if conditions are met
			if (config.IncludeLexemes || Prototypes.TypeOf(prototype, Ontology.Collection.Prototype))
			{
				if (prototype.Children.Count > 0)
				{
					JsonArray jsonChildren = new JsonArray();
					foreach (Prototype child in prototype.Children)
					{
						jsonChildren.Add(child == null ? null : ToFriendlyJsonObject(child, setCircular, config));
					}
					jsonPrototype[nameof(prototype.Children)] = jsonChildren;
				}
			}

			return jsonPrototype;
		}


	}
}
