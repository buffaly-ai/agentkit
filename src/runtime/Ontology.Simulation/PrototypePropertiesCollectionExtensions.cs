namespace Ontology.Simulation
{
	//>create an extension methods class for PrototypePropertiesCollection that defines GetStringOrDefault and uses the StringWrapper.GetStringValue pattern
	public static class PrototypePropertiesCollectionExtensions
	{
		public static string GetStringOrDefault(this PrototypePropertiesCollection collection, Prototype protoKey, string defaultValue = null)
		{
			var prototype = collection.GetOrNull(protoKey);
			if (prototype != null)
			{
				// Assuming `prototype` can return an instance of `StringWrapper`
				return StringWrapper.ToString(prototype);
			}

			return defaultValue;
		}
		public static void SetString(this PrototypePropertiesCollection collection, Prototype protoKey, string value)
		{
			collection[protoKey.PrototypeID] = StringWrapper.ToPrototype(value);
		}

		public static void SetStringCollection(this PrototypePropertiesCollection collection, Prototype protoKey, IEnumerable<string> values)
		{
			Collection col = new Collection();

			foreach (string synonym in values)
			{
				col.Add(StringWrapper.ToPrototype(synonym));
			}

			collection[protoKey.PrototypeID] = col;
		}

		public static void SetInt(this PrototypePropertiesCollection collection, Prototype protoKey, int value)
		{
			collection[protoKey.PrototypeID] = IntWrapper.ToPrototype(value);
		}

		public static void SetBool(this PrototypePropertiesCollection collection, Prototype protoKey, bool value)
		{
			collection[protoKey.PrototypeID] = BoolWrapper.ToPrototype(value);
		}

		public static bool GetBoolOrDefault(this PrototypePropertiesCollection collection, Prototype protoKey, bool defaultValue = false)
		{
			var prototype = collection.GetOrNull(protoKey);
			if (prototype != null)
			{
				// Assuming `prototype` can return an instance of `BoolWrapper`
				return new BoolWrapper(prototype).GetBoolValue();
			}
			return defaultValue;
		}

		public static int GetIntOrDefault(this PrototypePropertiesCollection collection, Prototype protoKey, int defaultValue = 0)
		{
			var prototype = collection.GetOrNull(protoKey);
			if (prototype != null)
			{
				// Assuming `prototype` can return an instance of `IntWrapper`
				return  new IntWrapper(prototype).GetIntValue();
			}
			return defaultValue;
		}

	}

}
