namespace Ontology
{
	public class Sequences
	{
		public static Prototype ? GetSequence(int iSequencePatternID)
		{
			Prototype protoSequencePattern = Prototypes.GetPrototype(iSequencePatternID);

			//We don't have to use a sequence pattern
			if (Prototypes.TypeOf(protoSequencePattern, Sequence.Prototype))
				return protoSequencePattern;

			return null;
		}
	}
}
