using Ontology.BaseTypes;

namespace Ontology.Simulation
{
	public class DoubleWrapper : Prototype
	{
		public NativeValuePrototype Prototype;

		public DoubleWrapper(double d)
		{
			this.Prototype = NativeValuePrototype.GetOrCreateNativeValuePrototype(d);

			PopulateClone(this, this.Prototype);
		}

		public DoubleWrapper(Prototype prototype)
		{
			if (prototype is DoubleWrapper dw)
			{
				this.Prototype = dw.Prototype;
				PopulateClone(this, this.Prototype);
				return;
			}

			if (!Prototypes.TypeOf(prototype, System_Double.Prototype))
				throw new Exception("Cannot create a double from prototype: " + prototype.PrototypeName);

			if (prototype is not NativeValuePrototype nvp)
				throw new Exception("Prototype must be of type NativeValuePrototype.");

			this.Prototype = nvp;

			PopulateClone(this, this.Prototype);
		}
		public double GetDoubleValue()
		{
			return (double)this.Prototype.NativeValue;
		}

		static public NativeValuePrototype ToPrototype(double dValue)
		{
			return NativeValuePrototype.GetOrCreateNativeValuePrototype(dValue);
		}

		public static double ToDouble(Prototype protoPredicate)
		{
			return new DoubleWrapper(protoPredicate).GetDoubleValue();
		}
	}
}
