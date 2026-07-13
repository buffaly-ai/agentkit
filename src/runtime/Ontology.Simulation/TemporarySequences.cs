using BasicUtilities.Collections;

namespace Ontology.Simulation
{
	public class TemporarySequences
	{
		static public double DefaultPartOfValue = 1.0;
		static public Prototype InsertWithChildren(Prototype protoSequence)
		{
			protoSequence.InsertTypeOf(Sequence.Prototype);
		
			BuildPartOfValues(protoSequence);

			return protoSequence;
		}

		static public void BuildPartOfValues(Prototype protoSequence)
		{
			foreach (Prototype prototype in protoSequence.Children)
			{
				Prototype rowPrototype = Prototypes.GetPrototype(prototype.PrototypeID);
				rowPrototype.PartOfValues.TryAdd(protoSequence, DefaultPartOfValue);
			}
		}

		public static Set<int> GetPossiblePrototypes(IEnumerable<Prototype> lstPrototypes)
		{
			Set<int> setPrototypes = new Set<int>();

			foreach (Prototype protoLexeme in lstPrototypes)
			{
				if (Prototypes.TypeOf(protoLexeme, Lexeme.Prototype))
				{
					TemporaryLexeme? lexeme = protoLexeme as TemporaryLexeme;

					if (lexeme == null)
					{
						throw new Exception($"Prototype {protoLexeme.PrototypeName} is not a TemporaryLexeme.");
					}

					foreach (var rowLexemePrototype in lexeme.LexemePrototypes)
					{
						setPrototypes.Add(rowLexemePrototype.Key.PrototypeID);
						setPrototypes.AddRange(rowLexemePrototype.Key.GetAllParents());
					}
				}

				//N20250409-02 - Allow filtering based on QuantumPrototypes
				else if (protoLexeme is QuantumPrototype qp)
				{
					foreach (Prototype qpOption in qp.PossiblePrototypes)
					{
						setPrototypes.Add(qpOption.PrototypeID);
						setPrototypes.AddRange(qpOption.GetAllParents());
					}
				}
				else
				{
					setPrototypes.Add(protoLexeme.PrototypeID);
					setPrototypes.AddRange(protoLexeme.GetAllParents());
				}
			}

			return setPrototypes;
		}
	}
}
