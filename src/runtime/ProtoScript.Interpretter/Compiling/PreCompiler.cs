using BasicUtilities;
using Ontology;
using ProtoScript.Interpretter.RuntimeInfo;
using ProtoScript.Interpretter.Symbols;

namespace ProtoScript.Interpretter.Compiling
{
	public static class PreCompiler
	{
		public static void PrecompileFile(NativeInterpretter interpretter, string strFile)
		{
			string strJson = Precompile(interpretter, strFile);
			FileUtil.WriteFile(strFile + ".json", strJson);
		}
		public static string Precompile(NativeInterpretter interpretter, string strFile)
		{
			ProtoScript.File file = ProtoScript.Parsers.Files.Parse(strFile);

			PrototypeDefinition ? protoDef = file.PrototypeDefinitions.FirstOrDefault(x => x.Methods.Any());
			if (null != protoDef)
				throw new InvalidOperationException($"'{strFile}' is not a candidate for precompiling because prototype '{protoDef.PrototypeName.TypeName}' declares methods.");

			List<Prototype> lstPrototypes = new List<Prototype>();
			foreach (PrototypeDefinition statement in file.PrototypeDefinitions)
			{
				lstPrototypes.Add(TemporaryPrototypes.GetTemporaryPrototype(statement.PrototypeName.TypeName));
			}

			JsonArray jsonArray = new JsonArray();
			foreach (var prototype in lstPrototypes)
			{
				jsonArray.Add(prototype.ToJsonObject());
			}
			string strJson = JsonUtil.ToFriendlyJSON(jsonArray).ToString();

			return strJson;
		}

		static public void LoadPrecompiledFile(string strFile, SymbolTable symbols)
		{
			string strJson = FileUtil.ReadFile(strFile);
			LoadPrecompiled(strJson, symbols);
		}
		static public void LoadPrecompiled(string strJson, SymbolTable symbols)
		{
			JsonArray jsonArray = new JsonValue(strJson).ToJsonArray();
			foreach (var jsonPrototype in jsonArray.Select(x => x.ToJsonObject()))
			{
				Prototype prototype = FromJsonToNewPrototype(jsonPrototype, symbols, true);
			}
		}

		public static Prototype FromJsonToNewPrototype(JsonObject jsonPrototype, SymbolTable symbols, bool bTopLevel = false)
		{
			if (jsonPrototype.ContainsKey(nameof(NativeValuePrototype.NativeValue)))
			{
				return NativeValuePrototype.FromJsonValue(jsonPrototype);
			}

			string strPrototypeName = jsonPrototype.GetStringOrNull("PrototypeName");

			Prototype prototype;
			if (strPrototypeName == "Ontology.Collection")
			{
				prototype = Ontology.Collection.Prototype.ShallowClone();
			}
			else
			{
				prototype = TemporaryPrototypes.GetOrCreateTemporaryPrototype(strPrototypeName);

				if (bTopLevel)
				{
					PrototypeTypeInfo info = PrototypeCompiler.InsertTemporaryPrototypeAsSymbol(prototype, symbols);
				}
			}


			foreach (var pair in jsonPrototype)
			{
				if (pair.Key == "PrototypeID")
					continue;

				if (pair.Key == "PrototypeName")
					continue;

				if (pair.Key == "Ontology.TypeOf")
				{
					JsonObject jsonTypeOfs = pair.Value.ToJsonObject();
					foreach (var jsonTypeOf in jsonTypeOfs.GetJsonArrayOrDefault("Children").Select(x => x.ToJsonObject()))
					{
						Prototype protoChild = TemporaryPrototypes.GetOrCreateTemporaryPrototype(jsonTypeOf.GetStringOrNull("PrototypeName"));
						prototype.InsertTypeOf(protoChild);
					}
				}
				else if (pair.Key == "Children")
				{
					foreach (var jsonChild in pair.Value.ToJsonArray().Select(x => x.ToJsonObject()))
					{
						prototype.Children.Add(FromJsonToNewPrototype(jsonChild, symbols));
					}
				}
				else
				{
					Prototype protoProp = TemporaryPrototypes.GetOrCreateTemporaryPrototype(pair.Key);
					prototype.Properties[protoProp.PrototypeID] = FromJsonToNewPrototype(pair.Value, symbols);
				}
			}

			return prototype;
		}
	}
}
