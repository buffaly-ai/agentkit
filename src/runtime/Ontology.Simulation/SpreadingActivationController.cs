using BasicUtilities.Collections;

namespace Ontology.Simulation
{

	public class SpreadingActivationController : BreadthFirstSearchControl<Prototype>
	{
		private FastPrototypeMap<double> m_mapActivations = new FastPrototypeMap<double>();
		public int ActivationCount = 0;

		public FastPrototypeMap<double> ActivationValues
		{
			get
			{
				return m_mapActivations;
			}
		}
		public void InsertPrototypes(IEnumerable<Prototype> lstPrototypes)
		{
			foreach (Prototype prototype in lstPrototypes)
			{
				InsertPrototype(prototype);
			}
		}

		public void InsertPrototype(Prototype prototype)
		{
			int iKey = prototype.PrototypeID;
			if (prototype is NativeValuePrototype)
			{
				iKey = ((NativeValuePrototype)prototype).NativeValue.GetHashCode();
			}

			if (!m_setExpanded.Contains(iKey))
			{
				InsertItem(prototype);
				m_setExpanded.Add(iKey);
			}
		}

		public void IncrementActivation(Prototype prototype, double dValue = 1.0)
		{
			if (!m_mapActivations.ContainsKey(prototype))
			{
				m_mapActivations[prototype] = dValue;
			}
			else
			{
				m_mapActivations[prototype] += dValue;
			}

			ActivationCount++;
		}
	}
}
