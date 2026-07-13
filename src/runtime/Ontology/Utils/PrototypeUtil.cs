using BasicUtilities;

namespace Ontology.Utils
{
	public class PrototypeUtil
	{
		public static void SavePrototypes(string strName, List<Prototype> lstPrototypes)
		{
			JsonObject jsonExamples = new JsonObject();
			JsonArray jsonInputs = new JsonArray();

			foreach (Prototype prototype in lstPrototypes)
			{
				jsonInputs.Add(prototype.ToJsonObject());
			}

			jsonExamples["Inputs"] = jsonInputs;

			FileUtil.WriteFile(FileUtil.BuildPath(@"c:\dev\ai\ontology", strName), jsonExamples.ToJSON());
		}

		public static void SavePrototype(string strName, Prototype prototype)
		{
			JsonObject jsonExamples = new JsonObject();
			JsonArray jsonInputs = new JsonArray();
			jsonInputs.Add(prototype.ToJsonObject());
			jsonExamples["Inputs"] = jsonInputs;
			FileUtil.WriteFile(FileUtil.BuildPath(@"c:\dev\ai\ontology", strName), jsonExamples.ToJSON());
		}


		public static List<Prototype> LoadPrototypes(string strName)
		{
			List<Prototype> lstInputs = new List<Ontology.Prototype>();

			string strContents = FileUtil.ReadFile(FileUtil.BuildPath(@"c:\dev\ai\ontology", strName));

			JsonObject jsonExamples = new JsonObject(strContents);
			JsonArray jsonInputs = jsonExamples["Inputs"].ToJsonArray();

			foreach (JsonValue jsonValue in jsonInputs)
			{
				Prototype prototype = NativeValuePrototype.FromJsonValue(jsonValue);
				lstInputs.Add(prototype);
			}
			return lstInputs;
		}


		public static Prototype LoadPrototype(string strName)
		{
			return LoadPrototypes(strName).First();
		}

		public static void SaveSourceToTargets(string strName, List<Prototype> lstInputs, List<Prototype> lstOutputs)
		{
			JsonObject jsonExamples = new JsonObject();
			JsonArray jsonInputs = new JsonArray();

			foreach (Prototype prototype in lstInputs)
			{
				jsonInputs.Add(prototype.ToJsonObject());
			}

			JsonArray jsonOutputs = new JsonArray();
			foreach (Prototype prototype in lstOutputs)
			{
				jsonOutputs.Add(prototype.ToJsonObject());
			}

			jsonExamples["Inputs"] = jsonInputs;
			jsonExamples["Outputs"] = jsonOutputs;

			FileUtil.WriteFile(FileUtil.BuildPath(@"c:\dev\ai\ontology", strName), jsonExamples.ToJSON());
		}

		public static Tuple<List<Prototype>, List<Prototype>> LoadSourceToTargets(string strName)
		{
			List<Prototype> lstInputs = new List<Ontology.Prototype>();
			List<Prototype> lstOutputs = new List<Ontology.Prototype>();

			string strContents = FileUtil.ReadFile(FileUtil.BuildPath(@"c:\dev\ai\ontology", strName));

			JsonObject jsonExamples = new JsonObject(strContents);
			JsonArray jsonInputs = jsonExamples["Inputs"].ToJsonArray();

			foreach (JsonValue jsonValue in jsonInputs)
			{
				Prototype prototype = Prototype.FromJsonValue(jsonValue);
				lstInputs.Add(prototype);
			}

			JsonArray jsonOutputs = jsonExamples["Outputs"].ToJsonArray();
			foreach (JsonValue jsonValue in jsonOutputs)
			{
				Prototype prototype = Prototype.FromJsonValue(jsonValue);
				lstOutputs.Add(prototype);
			}

			return new Tuple<List<Prototype>, List<Prototype>>(lstInputs, lstOutputs);
		}

	}
}
