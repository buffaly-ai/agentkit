namespace Ontology
{
	public class PrototypeTransform
	{
		public Prototype InputShadowTree;
		public Prototype OriginalInputShadowTree;

		public Prototype ShadowTree;
		public List<Prototype> EntityExtractionPaths = new List<Prototype>();
		public List<Prototype> EntityPaths = new List<Prototype>();
		public List<Prototype> ShadowBindingPaths = new List<Prototype>(); 


		public Prototype GetEntityExtractionPath(int iShadowBindingPath)
		{
			int iIndex = this.EntityPaths[iShadowBindingPath].Children.FindIndex(x => x.PrototypeID != Compare.Ignore.PrototypeID);
			return this.EntityExtractionPaths[iIndex];
		}
	}
}
