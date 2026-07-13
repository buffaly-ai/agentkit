//added
using BasicUtilities.Collections;

namespace Ontology.Simulation
{
	public class TemporaryPrototypeCategorization
	{
		static public bool IsCategorized(Prototype prototype, Prototype protoCompare, bool bLog = false)
		{
			//From PrototypeCategorizationTrees.IsCategorized

			//N20190501-04 - Do not add anonymous types to this method

			//N20190422-03
			if (prototype?.PrototypeID == Compare.Ignore.PrototypeID || protoCompare?.PrototypeID == Compare.Ignore.PrototypeID)
				return true;

			//N20190425-12
			if (null == prototype && null == protoCompare)
				return true;

			//N20190426-08
			if (null == prototype || null == protoCompare)
			{
				if (bLog)
					Logs.DebugLog.WriteEvent("IsCategorized", null == prototype ? "prototype" : "shadow" + " is null");

				return false;
			}

			//N20190815-02
			if (protoCompare.PrototypeID == Compare.Entity.PrototypeID)
				return true;

			if (!prototype.ShallowEquivalent(protoCompare) && !Prototypes.TypeOf(prototype, protoCompare))
			{
				if (bLog)
				{
					Logs.DebugLog.WriteEvent("IsCategorized", $"{prototype.PrototypeName} != {protoCompare.PrototypeName}");
				}

				return false;


			}

			foreach (var pair in protoCompare.Properties)
			{
				if (pair.Value == null)
					continue;

				//Ignore special Comparison key
				if (pair.Key == Compare.Comparison.PrototypeID)
					continue;

				//Allow Nulls to be passed so Compare.Ignore can be evaluated
				//else
				if (!IsCategorized(prototype.Properties[pair.Key], protoCompare.Properties[pair.Key], bLog))
				{
					if (bLog)
						Logs.DebugLog.WriteEvent("IsCategorized", $"{prototype.PrototypeName}.Properties[{Prototypes.GetPrototypeName(pair.Key)}] != {protoCompare.PrototypeName}.Properties[{Prototypes.GetPrototypeName(pair.Key)}]");

					return false;
				}
			}

			//N20190815-01 - Don't compare children unless this is a collection (to ignore Lexemes)
			if (Prototypes.TypeOf(prototype, Ontology.Collection.Prototype) && Prototypes.TypeOf(protoCompare, Ontology.Collection.Prototype))
			{
				//N20191224-02
				if (prototype.Properties[Compare.Comparison.PrototypeID]?.PrototypeID == Compare.StartsWith.PrototypeID)
				{
					if (protoCompare.Properties[Compare.Comparison.PrototypeID]?.PrototypeID == Compare.StartsWith.PrototypeID &&
						protoCompare.Children.Count <= prototype.Children.Count)
					{
						for (int i = 0; i < protoCompare.Children.Count; i++)
						{
							Prototype protoChildCompare = protoCompare.Children[i];
							if (protoChildCompare.PrototypeID != Compare.Ignore.PrototypeID)        //allow for missing elements 
							{
								if (!IsCategorized(prototype.Children[i], protoChildCompare, bLog))
								{
									if (bLog)
										Logs.DebugLog.WriteEvent("IsCategorized", $"{prototype.PrototypeName}.Children[{i}] != {protoCompare.PrototypeName}.Children[{i}]");

									return false;
								}
							}
						}

						return true;
					}

					return false;
				}

				//N20181217-01
				Prototype protoCompareType = protoCompare.Properties[Compare.Comparison.PrototypeID] ?? Compare.Exact.Prototype;

				if (protoCompareType.PrototypeID == Compare.Exact.PrototypeID)
				{
					//N20200819-02
					if (protoCompare.Children.Count == 0 && prototype.Children.Count == 0)
						return true;

					//N20181218-01
					if (protoCompare.Children.Count != prototype.Children.Count)
					{
						if (bLog)
							Logs.DebugLog.WriteEvent("IsCategorized", $"{prototype.PrototypeName}.Children.Count != {protoCompare.PrototypeName}.Children.Count");

						return false;
					}

					for (int i = 0; i < protoCompare.Children.Count; i++)
					{
						Prototype protoChildCompare = protoCompare.Children[i];
						if (protoChildCompare.PrototypeID != Compare.Ignore.PrototypeID)        //allow for missing elements 
						{
							if (!IsCategorized(prototype.Children[i], protoChildCompare, bLog))
							{
								if (bLog)
									Logs.DebugLog.WriteEvent("IsCategorized", $"{prototype.PrototypeName}.Children[{i}] != {protoCompare.PrototypeName}.Children[{i}]");

								return false;
							}
						}
					}
				}
				else if (protoCompareType.PrototypeID == Compare.StartsWith.PrototypeID)                //N20181218-01
				{
					if (protoCompare.Children.Count > prototype.Children.Count)
					{
						if (bLog)
							Logs.DebugLog.WriteEvent("IsCategorized", $"{prototype.PrototypeName}.Children.Count != {protoCompare.PrototypeName}.Children.Count");

						return false;
					}

					for (int i = 0; i < protoCompare.Children.Count; i++)
					{
						Prototype protoChildCompare = protoCompare.Children[i];
						if (protoChildCompare.PrototypeID != Compare.Ignore.PrototypeID)        //allow for missing elements 
						{
							if (!IsCategorized(prototype.Children[i], protoChildCompare, bLog))
							{
								if (bLog)
									Logs.DebugLog.WriteEvent("IsCategorized", $"{prototype.PrototypeName}.Children[{i}] != {protoCompare.PrototypeName}.Children[{i}]");

								return false;
							}
						}
					}
				}
				else if (protoCompareType.PrototypeID == Compare.Intersection.PrototypeID)
				{
					//Note: This could potentially be expensive for instances where there a lot of children in the protoCompare
					foreach (Prototype protoChild in protoCompare.Children)
					{
						if (!prototype.Children.Any(x => IsCategorized(x, protoChild, bLog)))
						{
							if (bLog)
								Logs.DebugLog.WriteEvent("IsCategorized", $"{prototype.PrototypeName}.Children[*] != {protoCompare.PrototypeName}.Children[*]");

							return false;
						}
					}
				}
			}

			return true;
		}

		static public bool IsPartiallyCategorized(Prototype prototype, Prototype shadow)
		{
			return IsPartiallyCategorizedCircular(prototype, shadow, new Set<int>());
		}

		static public bool IsPartiallyCategorizedCircular(Prototype prototype, Prototype shadow, Set<int> setHashes)
		{
			//From HCPTrees.IsPartiallyCategorized - but minimized to remove augmented properties and collection comparison types

			if (shadow == null)
				return true;

			//N20210109-03 - Treat null as missing
			if (prototype == null)
				return true;

			if (setHashes.Contains(prototype.GetHashCode()))
				return true;

			setHashes.Add(prototype.GetHashCode());

			if (!Prototypes.TypeOf(prototype, shadow))
				return false;

			foreach (var pair in prototype.Properties)
			{
				if (!IsPartiallyCategorizedCircular(pair.Value, shadow.Properties[pair.Key], setHashes))
					return false;
			}


			for (int i = 0; i < prototype.Children.Count && i < shadow.Children.Count; i++)
			{
				Prototype protoChild = prototype.Children[i];

				if (!IsPartiallyCategorizedCircular(protoChild, shadow.Children[i], setHashes))
					return false;
			}

			return true;
		}
		static public Prototype GetPartiallyUncategorized(Prototype prototype, Prototype shadow)
		{
			return GetPartiallyUncategorizedCircular(prototype, shadow, new Set<int>());
		}

		static public Prototype  GetPartiallyUncategorizedCircular(Prototype prototype, Prototype shadow, Set<int> setHashes)
		{
			//N20220729-01 - Return the parts of the graph that have missing properties (the prototype is missing
			//a shadow's property

			if (shadow == null)
				return null;
			
			if (prototype == null)
				return shadow.ShallowClone();

			if (setHashes.Contains(prototype.GetHashCode()))
				return null;

			setHashes.Add(prototype.GetHashCode());

			if (!Prototypes.TypeOf(prototype, shadow))
				throw new Exception("The prototype should be partially categorized");

			Prototype protoResult = prototype.ShallowClone();
			bool bUncategorizedFound = false; 
			foreach (var pair in shadow.Properties)
			{
				Prototype protoPropResult = GetPartiallyUncategorizedCircular(prototype.Properties[pair.Key], pair.Value, setHashes);
				if (null != protoPropResult)
				{
					protoResult.Properties[pair.Key] = protoPropResult;
					bUncategorizedFound = true;
				}
			}


			for (int i = 0; i < prototype.Children.Count && i < shadow.Children.Count; i++)
			{
				Prototype protoChild = prototype.Children[i];

				Prototype protoChildResult = GetPartiallyUncategorizedCircular(protoChild, shadow.Children[i], setHashes);
				if (null != protoChildResult)
				{
					protoResult.Children.Add(protoChildResult);
					bUncategorizedFound = true; 
				}
			}

			return bUncategorizedFound ? protoResult : null;
		}

		static public IEnumerable<Prototype> GetLeafDifferences(Prototype prototype, Prototype shadow)
		{
			return GetLeafDifferencesCircular(prototype, shadow, new Set<int>());
		}
		static public IEnumerable<Prototype> GetLeafDifferencesCircular(Prototype prototype, Prototype shadow, Set<int> setHashes)
		{
			//N20220730-01 - Get the properties in the shadow that don't have a corresponding item in the prototype
			if (prototype == null || shadow == null)
				throw new Exception("Prototype cannot be null");

			if (!Prototypes.TypeOf(prototype, shadow))
				throw new Exception("Prototypes do not match");

			if (!setHashes.Contains(prototype.GetHashCode()))
			{
				setHashes.Add(prototype.GetHashCode());

				foreach (var pair in shadow.Properties)
				{
					if (prototype.Properties[pair.Key] == null)
					{
						Prototype protoResult = prototype.ShallowClone();
						protoResult.Properties[pair.Key] = pair.Value;

						yield return protoResult;
					}
					else
					{
						foreach (var res in GetLeafDifferencesCircular(prototype.Properties[pair.Key], pair.Value, setHashes))
						{
							yield return res;
						}
					}
				}
			}
		}
	}
}
