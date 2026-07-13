using BasicUtilities;
using BasicUtilities.Collections;
using Ontology.BaseTypes;

namespace Ontology.Simulation
{
	public class BagOfFeatures
	{
		static public double Threshold = 0.01;
		static public bool UpdatePredictiveValue = false;


		static public Prototype Categorize(Prototype prototype)
		{
			List<Prototype> lstFeatures = GetFeatures(prototype);
			Set<int> setPrototypes = TemporarySequences.GetPossiblePrototypes(lstFeatures);
			List<Prototype> lstExpectations = GetSequences2(setPrototypes, Dimensions.NL.PrototypeID);


			foreach (Prototype expectation in lstExpectations.Where(x => x.Children.Count <= lstFeatures.Count))
			{
				if (!IsCategorizedSingle(prototype, expectation, setPrototypes))
					continue;

				//More specific subtypes should take precedence
				prototype.InsertTypeOf(expectation.PrototypeID);
			}

			return prototype;
		}

		static public bool IsCategorizedSingle(Prototype prototype, Prototype expectation)
		{
			List<Prototype> lstFeatures = GetFeatures(prototype);
			Set<int> setPrototypes = TemporarySequences.GetPossiblePrototypes(lstFeatures);

			return IsCategorizedSingle(prototype, expectation, setPrototypes);
		}

		static public bool IsCategorizedSingle(Prototype prototype, Prototype expectation, Set<int> setPrototypes)
		{
			return !expectation.Children.Any(x => !setPrototypes.Contains(x.PrototypeID));
		}

		static public Prototype CategorizeAndUnderstand(Prototype prototype)
		{
			//N20250521-01 - This performs similar work to the FollowExpectationsNode node for BagOfFeatures
			List<Prototype> lstFeatures = GetFeatures(prototype);
			Set<int> setPrototypes = TemporarySequences.GetPossiblePrototypes(lstFeatures);
			List<Prototype> lstExpectations = GetSequences2(setPrototypes, Dimensions.NL.PrototypeID);


			foreach (Prototype expectation in lstExpectations.Where(x => x.Children.Count <= lstFeatures.Count))
			{
				if (!IsCategorizedSingle(prototype, expectation, setPrototypes))
					continue;

				if (prototype.ShallowEqual(Ontology.Collection.Prototype))
				{
					Prototype protoTemp = expectation.ShallowClone();
					protoTemp.Children.AddRange(prototype.Children);
					prototype = protoTemp;
				}

				//In this function we only want the highest value / longest term 
				break;
			}

			return prototype;
		}

		static public Prototype CategorizeAndUnderstandMultiple(Prototype prototype)
		{
			//N20250521-01 - This performs similar work to the FollowExpectationsNode node for BagOfFeatures
			List<Prototype> lstFeatures = GetFeatures(prototype);
			Set<int> setPrototypes = TemporarySequences.GetPossiblePrototypes(lstFeatures);
			List<Prototype> lstExpectations = GetSequences2(setPrototypes, Dimensions.NL.PrototypeID);

			foreach (Prototype expectation in lstExpectations.Where(x => x.Children.Count <= lstFeatures.Count))
			{
				if (!IsCategorizedSingle(prototype, expectation, setPrototypes))
					continue;

				if (prototype.ShallowEqual(Ontology.Collection.Prototype))
				{
					Prototype protoTemp = expectation.ShallowClone();
					protoTemp.Children.AddRange(prototype.Children);
					prototype = protoTemp;
				}

				else
				{
					prototype.InsertTypeOf(expectation.PrototypeID);
				}
			}

			return prototype;
		}


		public static List<Prototype> GetFeatures(Prototype protoTagged)
		{
			List<Prototype> lstLeaves = new List<Prototype>();

			PrototypeGraphs.DepthFirstOnNormal(protoTagged, x =>
			{
				if (Prototypes.TypeOf(x, Lexeme.Prototype))
				{
					return x;
				}
				if (Prototypes.TypeOf(x, System_String.Prototype))
				{
					return x;
				}
				if (Prototypes.TypeOf(x, Collection.Prototype))
				{
					return x;
				}

				if (x.PrototypeName.Contains("#"))
					lstLeaves.Add(StringUtil.LeftOfLast(x.PrototypeName, "#"));
				else
					lstLeaves.Add(x);

				return x;
			});

			return lstLeaves;
		}

		public static List<Prototype> GetPrimaryFeatures(Prototype protoTagged)
		{
			List<Prototype> lstFeatures = GetFeatures(protoTagged);
			List<Prototype> lstPrimaryFeatures = GetPrimaryFeatures(lstFeatures);

			return lstPrimaryFeatures;
		}

		public static List<Prototype> GetPrimaryFeatures(List<Prototype> lstFeatures)
		{
			List<Prototype> lstPrimaryFeatures = new List<Prototype>();
			foreach (Prototype child in lstFeatures)
			{
				//Get the original prototype, as it may have changed during rolling
				Prototype protoFeature = TemporaryPrototypes.GetTemporaryPrototype(child.PrototypeID);

				//At this point don't include BagOfFeatures sememes. But, in the future you may want to include 
				//the BagOfFeatures and remove it's components instead (for better compression and abstraction)
				if (protoFeature.TypeOf("BagOfFeatures"))
					continue;

				//Use all features not just one
				lstPrimaryFeatures.AddRange(protoFeature.GetAncestorsBelow("Sememe").Select(x => x.ShallowClone()));
			}

			return lstPrimaryFeatures;
		}

		static public List<Prototype> GetSequences2(Set<int> setPossiblePrototypes, int iDimensionPrototypeID)
		{
			List<Prototype> lstResults = new List<Prototype>();

			foreach (int iPrototypeID in setPossiblePrototypes)
			{
				Prototype prototype = Prototypes.GetPrototype(iPrototypeID);
				List<Prototype> lstPrototypes = GetSequencesSingular(prototype, iDimensionPrototypeID);
				foreach (Prototype child in lstPrototypes)
				{
					if (!lstResults.Any(x => Prototypes.AreShallowEqual(x, child)))
					{
						lstResults.Add(child);
					}
				}
			}
		
			//N20190823-02 - Longest first if the same value
			return lstResults.OrderByDescending(x => x.Value).ThenByDescending(x => x.Children.Count).ToList();
		}

		static public List<Prototype> GetSequences(Prototype prototype, int iDimensionPrototypeID)
		{
			//From Tagger.Prototypes.GetPrototypes
			List<Prototype> lstPrototypes = new List<Prototype>();

			if (prototype is QuantumPrototype qp && !qp.Collapsed)
			{
				foreach (Prototype proto in qp.PossiblePrototypes)
				{
					List<Prototype> lstPrototypesSub = GetSequencesSingular(proto, iDimensionPrototypeID);
					lstPrototypes.AddRange(lstPrototypesSub);
				}
			}

			else
			{
				lstPrototypes = GetSequencesSingular(prototype, iDimensionPrototypeID);
			}

			//N20190808-02 - Minor speedup. Note: this may be a problem if we ever add a sequence with "Lexeme" in it
			if (!Prototypes.TypeOf(prototype, Lexeme.Prototype))
			{
				//If we allow matches on the parents. 
				foreach (int parent in prototype.GetAllParents())
				{
					Prototype rowParent = Prototypes.GetPrototype(parent);
				//	if (rowParent.PredictiveValue >= Threshold)             //shortcut so it doesn't go down low value paths (1)
					{
						List<Prototype> lstParentPrototypes = GetSequencesSingular(rowParent, iDimensionPrototypeID);

						//This assumes that we multiply the value of the prototype parent by the value of the sequence
						//I don't know if that 
						for (int i = 0; i < lstParentPrototypes.Count; i++)
						{
							Prototype parentPrototype = lstParentPrototypes[i];

							//Do not degrade the value, see N20190111-01
							//parentPrototype.Value = rowPrototype.PredictiveValue * parentPrototype.Value;

							//We cannot have a threshold and update or the values will drop to 0 eventually
							if (UpdatePredictiveValue || parentPrototype.Value >= Threshold)
								lstPrototypes.Add(parentPrototype);
						}
					}
				}
			}

			//N20190823-02 - Longest first if the same value
			return lstPrototypes.OrderByDescending(x => x.Value).ThenByDescending(x => x.Children.Count).ToList();
		}

		static public List<Prototype> GetSequencesSingular(Prototype prototype, int iDimensionPrototypeID)
		{
			List<Prototype> lstPrototypes = new List<Prototype>();

			List<Prototype> lstSequencePatterns = new List<Prototype>();

			foreach (var rowValue in prototype.PartOfValues)
			{
				Prototype protoSequencePattern = rowValue.Key;

				if (protoSequencePattern.Children.Any(x => x.Value == 0))
					continue;

				if (!Prototypes.TypeOf(protoSequencePattern, "BagOfFeatures"))
					continue;

				protoSequencePattern = protoSequencePattern.Clone();
				protoSequencePattern.Value = rowValue.Value;

				lstSequencePatterns.Add(protoSequencePattern);
				lstPrototypes.Add(protoSequencePattern);
			}

			return lstPrototypes;
		}
	}
}
