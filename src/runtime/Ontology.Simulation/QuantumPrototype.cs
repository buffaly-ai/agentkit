namespace Ontology.Simulation
{
	public class QuantumPrototype : Prototype
	{
		private Collection m_colPossiblePrototypes = new Collection();
		public bool Collapsed { get; private set; }

		private QuantumPrototype()
		{
		}

		public QuantumPrototype(IEnumerable<Prototype> possiblePrototypes)
		{
			if (possiblePrototypes == null || !possiblePrototypes.Any())
				throw new ArgumentException("Must provide at least one prototype.");

			m_colPossiblePrototypes = new Collection(possiblePrototypes);
			Collapsed = !(possiblePrototypes.Count() > 1);

			this.PrototypeName = possiblePrototypes.First().PrototypeName;
			this.PrototypeID = possiblePrototypes.First().PrototypeID;
			this.Value = possiblePrototypes.First().Value;
		}

		public IEnumerable<Prototype> PossiblePrototypes
		{
			get
			{
				return m_colPossiblePrototypes.Children;
			}
		}

		public override bool TypeOf(Prototype parent)
		{
			if (Collapsed)
				return base.TypeOf(parent);

			return m_colPossiblePrototypes.Children.Any(x => Prototypes.TypeOf(x, parent));
		}

		public override IEnumerable<int> GetAllParents()
		{
			if (Collapsed)
				return base.GetAllParents();

			List<int> lstAllParents = new List<int>();
			foreach (Prototype prototype in m_colPossiblePrototypes.Children)
			{
				foreach (int protoParent in prototype.GetAllParents())
				{
					if (!lstAllParents.Any(x => x == protoParent))
						lstAllParents.Add(protoParent);
				}
			}

			return lstAllParents;
		}

		public override Prototype Clone()
		{
			QuantumPrototype prototype = Prototypes.CloneCircular(this) as QuantumPrototype;
			prototype.Collapsed = this.Collapsed;
			prototype.m_colPossiblePrototypes = new Collection(this.m_colPossiblePrototypes.Children);
			return prototype;
		}

		public override Prototype ShallowClone()
		{
			QuantumPrototype nv = new QuantumPrototype();

			PopulateClone(nv);
			nv.m_colPossiblePrototypes = new Collection(this.m_colPossiblePrototypes.Children);
			nv.Collapsed = this.Collapsed;

			return nv;
		}

		public void Collapse(Prototype prototype)
		{
			if (!m_colPossiblePrototypes.Children.Any(x => Prototypes.TypeOf(x, prototype)))
			{
				//N20250323-01 - This must be a subtype
				if (this.TypeOfsCollection.Any(x => x == prototype.PrototypeID))
					prototype = Prototypes.GetPrototype(prototype.GetParents().First());
				else
					throw new ArgumentException("The prototype is not a possible prototype or no prototypes derive from it.");
			}

			// Refine the collection to only include prototypes that have 'prototype' as a base
			var refinedPrototypes = m_colPossiblePrototypes.Children
				.Where(x => Prototypes.TypeOf(x, prototype))
				.ToList();

			if (!refinedPrototypes.Any())
				throw new ArgumentException("No prototypes in the collection derive from the specified prototype.");

			m_colPossiblePrototypes = new Collection(refinedPrototypes);

			// If only one prototype remains, collapse fully
			if (m_colPossiblePrototypes.Count == 1)
			{
				var singlePrototype = m_colPossiblePrototypes.Children.First();
				PopulateClone(this, singlePrototype);
				Collapsed = true;
				m_colPossiblePrototypes.Clear();
			}
		}
	}
}
