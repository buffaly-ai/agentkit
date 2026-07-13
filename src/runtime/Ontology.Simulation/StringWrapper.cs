using Ontology.BaseTypes;

namespace Ontology.Simulation
{
	public class StringWrapper : Prototype
	{
		public NativeValuePrototype Prototype;

		public StringWrapper()
		{
			this.Prototype = NativeValuePrototype.GetOrCreateNativeValuePrototype("");

			PopulateClone(this, this.Prototype);
		}
		public StringWrapper(string strValue)
		{
			if (strValue == null)
				throw new ArgumentNullException(nameof(strValue), "String value cannot be null.");

			this.Prototype = NativeValuePrototype.GetOrCreateNativeValuePrototype(strValue);

			PopulateClone(this, this.Prototype);
		}

		public StringWrapper(Prototype prototype) 
		{
			if (prototype is StringWrapper sw)
			{
				this.Prototype = sw.Prototype;
				PopulateClone(this, this.Prototype);
				return;
			}

			if (!Prototypes.TypeOf(prototype, System_String.Prototype))
				throw new Exception("Cannot create a string from prototype: " + prototype.PrototypeName);

			if (prototype is not NativeValuePrototype nvp)
				throw new Exception("Prototype must be of type NativeValuePrototype.");

			this.Prototype = nvp;

			PopulateClone(this, this.Prototype);
		}

		//public static implicit operator StringWrapper(string str)
		//{
		//	return (StringWrapper)StringWrapper.GetOrCreateNativeValuePrototype(str);
		//}

		public string GetStringValue()
		{
			return ((string)this.Prototype.NativeValue);
		}

		static public NativeValuePrototype Join(Prototype collection, Prototype separator)
		{
			List<string> lstStrings = collection.Children.Select(x => (x as NativeValuePrototype).NativeValue as string).ToList();
			string strSeparator = (separator as NativeValuePrototype).NativeValue as string;

			return StringWrapper.ToPrototype(string.Join(strSeparator, lstStrings));
		}

		static public NativeValuePrototype Format(Prototype format, Collection values)
		{
			List<string> lstStrings = values.Children.Select(x => (x as NativeValuePrototype).NativeValue as string).ToList();
			string strFormat = (format as NativeValuePrototype).NativeValue as string;

			return StringWrapper.ToPrototype(string.Format(strFormat, lstStrings.ToArray()));
		}

		static public NativeValuePrototype Format(string strFormat, Collection values)
		{
			List<string> lstStrings = values.Children.Select(x => (x as NativeValuePrototype).NativeValue as string).ToList();

			return StringWrapper.ToPrototype(string.Format(strFormat, lstStrings.ToArray()));
		}

		public static string ToString(Prototype protoPredicate)
		{
			if (protoPredicate is StringWrapper sw)
				return sw.GetStringValue();

			return new StringWrapper(protoPredicate).GetStringValue();		
		}

		static public NativeValuePrototype ToPrototype(string strValue)
		{
			return NativeValuePrototype.GetOrCreateNativeValuePrototype(strValue);
		}
	}
}
