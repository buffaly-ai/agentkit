using BasicUtilities;
using BasicUtilities.Collections;
using System.Text;

namespace Ontology
{
	public class PrototypeJsonSerializer : IJsonSerializer
	{
		public StringBuilder ToJSON(object obj)
		{
			Logs.DebugLog.WriteEvent("PrototypeJsonSerializer", "");

			if (obj is NativeValuePrototype)
				return new StringBuilder(((NativeValuePrototype)obj).ToFriendlyJSON());

			return new StringBuilder(((Prototype)obj).ToFriendlyJSON());
		}

		public object FromJsonObject(JsonObject jsonObject)
		{
			if (jsonObject.ContainsKey("NativeValue"))
				return NativeValuePrototype.FromJsonValue(jsonObject);

			return Prototype.FromJsonValue(jsonObject);
		}

		public StringBuilder ToJSON(object obj, Set<int> setSerialized)
		{
			return ToJSON(obj);
		}

		public object FromJsonObject(System.Type t, JsonObject jsonObject)
		{
			throw new NotImplementedException();
		}
	}

	public class NativeValuePrototypeJsonSerializer : IJsonSerializer
	{
		public StringBuilder ToJSON(object obj)
		{
			Logs.DebugLog.WriteEvent("NativeValuePrototypeJsonSerializer", "");

			return new StringBuilder(((NativeValuePrototype)obj).ToFriendlyJSON());
		}

		public object FromJsonObject(JsonObject jsonObject)
		{
			return NativeValuePrototype.FromJsonValue(jsonObject);
		}

		public StringBuilder ToJSON(object obj, Set<int> setSerialized)
		{
			return new StringBuilder(((NativeValuePrototype)obj).ToFriendlyJSON());
		}

		public object FromJsonObject(System.Type t, JsonObject jsonObject)
		{
			throw new NotImplementedException();
		}
	}

	//public class TemporaryPrototypeJsonSerializer : IJsonSerializer
	//{
	//	public StringBuilder ToJSON(object obj)
	//	{
	//		Logs.DebugLog.WriteEvent("TemporaryPrototypeJsonSerializer", "");

	//		return new StringBuilder(((TemporaryPrototype)obj).ToFriendlyJSON());
	//	}

	//	public object FromJsonObject(JsonObject jsonObject)
	//	{
	//		return TemporaryPrototype.FromJsonValue(jsonObject);
	//	}

	//	public StringBuilder ToJSON(object obj, Set<int> setSerialized)
	//	{
	//		return new StringBuilder(((TemporaryPrototype)obj).ToFriendlyJSON());

	//	}

	//	public object FromJsonObject(System.Type t, JsonObject jsonObject)
	//	{
	//		throw new NotImplementedException();
	//	}
	//}

}
