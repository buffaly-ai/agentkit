using Ontology.BaseTypes;

namespace Ontology.Simulation
{
	public class IntWrapper : Prototype
	{
		public NativeValuePrototype Prototype;

		public IntWrapper(int i)
		{
			Prototype = NativeValuePrototype.GetOrCreateNativeValuePrototype(i);

			PopulateClone(this, this.Prototype);
		}

		public IntWrapper(Prototype prototype)
		{
			if (prototype is IntWrapper iw)
			{
				this.Prototype = iw.Prototype;
				PopulateClone(this, this.Prototype);
				return;
			}

			if (!Prototypes.TypeOf(prototype, System_Int32.Prototype))
				throw new Exception("Cannot create an int from prototype: " + prototype.PrototypeName);

			if (prototype is not NativeValuePrototype nvp)
				throw new Exception("Prototype must be of type NativeValuePrototype.");

			this.Prototype = nvp;

			PopulateClone(this, this.Prototype);
		}

		public int GetIntValue()
		{
			return (int)this.Prototype.NativeValue;
		}

		public static implicit operator int(IntWrapper d) => d.GetIntValue();	

		static public Prototype Increment(Prototype nv)
		{
			int iValue = (int)(nv as NativeValuePrototype).NativeValue;
			iValue = iValue + 1;
			return NativeValuePrototype.GetOrCreateNativeValuePrototype(iValue);
		}

		static public NativeValuePrototype ToPrototype(int iValue)
		{
			return NativeValuePrototype.GetOrCreateNativeValuePrototype(iValue);
		}

		public static int ToInteger(Prototype protoPredicate)
		{
			return new IntWrapper(protoPredicate).GetIntValue();
		}
	}
}
