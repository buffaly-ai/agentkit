using Ontology.BaseTypes;

namespace Ontology.Simulation
{
	public class BoolWrapper : Prototype
	{
		public NativeValuePrototype Prototype;

		public BoolWrapper(bool b)
		{
			this.Prototype = NativeValuePrototype.GetOrCreateNativeValuePrototype(b);

			PopulateClone(this, this.Prototype);
		}

		public BoolWrapper(Prototype prototype)
		{
			if (prototype is BoolWrapper bw)
			{
				this.Prototype = bw.Prototype;
				PopulateClone(this, this.Prototype);
				return;
			}

			if (!Prototypes.TypeOf(prototype, System_Boolean.Prototype))
				throw new Exception("Cannot create a bool from prototype: " + prototype.PrototypeName);

			if (prototype is not NativeValuePrototype nvp)
				throw new Exception("Prototype must be of type NativeValuePrototype.");

			this.Prototype = nvp;

			PopulateClone(this, this.Prototype);
		}

		public bool GetBoolValue()
		{
			return (bool)this.Prototype.NativeValue;
		}

		public static implicit operator bool(BoolWrapper d) => d.GetBoolValue();

		static public NativeValuePrototype ToPrototype(bool bValue)
		{
			return NativeValuePrototype.GetOrCreateNativeValuePrototype(bValue);
		}

		public static bool ToBoolean(Prototype protoPredicate)
		{
			return new BoolWrapper(protoPredicate).GetBoolValue();
		}

	}
}
