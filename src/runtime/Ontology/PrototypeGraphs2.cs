using BasicUtilities;
using BasicUtilities.Collections;
using Ontology.Utils;
using System.Linq;

namespace Ontology
{
	public partial class PrototypeGraphs
	{
		static public bool AreEqual(Prototype proto1, Prototype proto2, bool bLog = false)
		{
			if (proto1 == null && proto2 == null)
				return true;

			if (proto1 == null || proto2 == null)
				return false;

			if (!Prototypes.AreShallowEqual(proto1, proto2))
			{
				if (bLog)
					Logs.DebugLog.WriteEvent("Not Equal", proto1.PrototypeName + " vs " + proto2.PrototypeName);

				return false;
			}

			//N20190501-02
			if (!Prototypes.TypeOf(proto1, Ontology.Collection.Prototype) || !Prototypes.TypeOf(proto2, Ontology.Collection.Prototype))
			{
				//N20190919-03 - Don't shortcircuit looking for prototype count since one can be partially loaded
				Set<int> setProperties = new Set<int>();
				foreach (var pair in proto1.NormalProperties)
				{
					setProperties.Add(pair.Key);
				}

				foreach (var pair in proto2.NormalProperties)
				{
					setProperties.Add(pair.Key);
				}

				foreach (int iKey in setProperties)
				{
					Prototype prop1 = proto1.Properties[iKey];
					Prototype prop2 = proto2.Properties[iKey];

					if (null == prop1 && null == prop2)
					{
						continue;
					}

					if (!AreEqual(prop1, prop2, bLog))
					{
						if (bLog)
							Logs.DebugLog.WriteEvent("Property Not Equal", Prototypes.GetPrototypeName(iKey));

						return false;
					}
				}
			}

			//N20190820-03 - Only compare Collections (no lexemes)
			else
			{
				if (proto1.Children.Count != proto2.Children.Count)
				{
					if (bLog)
						Logs.DebugLog.WriteEvent("Not Equal", proto1.PrototypeName + " Children vs " + proto2.PrototypeName + " Children");

					return false;
				}

				//N20200108-01
				if (proto1.Properties[Compare.Comparison.PrototypeID]?.PrototypeID != proto2.Properties[Compare.Comparison.PrototypeID]?.PrototypeID)
				{
					if (bLog)
						Logs.DebugLog.WriteEvent("Not Equal", proto1.PrototypeName + " Children.Comparison vs " + proto2.PrototypeName + " Children.Comparison");

					return false;
				}

				for (int i = 0; i < proto1.Children.Count; i++)
				{
					if (!AreEqual(proto1.Children[i], proto2.Children[i], bLog))
					{
						if (bLog)
							Logs.DebugLog.WriteEvent("Child Not Equal", i.ToString());

						return false;
					}
				}
			}

			return true;
		}

		static public bool AreEquivalentCircular(Prototype proto1, Prototype proto2)
		{
			//N20220121-03 - Differs from AreEqual in that it does not use the instance
			return AreEquivalentCircular(proto1, proto2, false, new Set<int>());
		}

		static public bool AreEquivalentCircular(Prototype proto1, Prototype proto2, bool bLog, Set<int> setHashes)
		{
			if (proto1 == null && proto2 == null)
				return true;

			if (proto1 == null || proto2 == null)
				return false;

			if (setHashes.Contains(proto1.GetHashCode()))
				return true;

			setHashes.Add(proto1.GetHashCode());

			if (!proto1.ShallowEquivalent(proto2))
			{
				if (bLog)
					Logs.DebugLog.WriteEvent("Not Equal", proto1.PrototypeName + " vs " + proto2.PrototypeName);

				return false;
			}

			//N20190501-02
			if (!Prototypes.TypeOf(proto1, Ontology.Collection.Prototype) || !Prototypes.TypeOf(proto2, Ontology.Collection.Prototype))
			{
				//N20190919-03 - Don't shortcircuit looking for prototype count since one can be partially loaded
				Set<int> setProperties = new Set<int>();
				foreach (var pair in proto1.NormalProperties)
				{
					setProperties.Add(pair.Key);
				}

				foreach (var pair in proto2.NormalProperties)
				{
					setProperties.Add(pair.Key);
				}

				foreach (int iKey in setProperties)
				{
					Prototype prop1 = proto1.Properties[iKey];
					Prototype prop2 = proto2.Properties[iKey];

					if (null == prop1 && null == prop2)
					{
						continue;
					}

					if (!AreEquivalentCircular(prop1, prop2, bLog, setHashes))
					{
						if (bLog)
							Logs.DebugLog.WriteEvent("Property Not Equal", Prototypes.GetPrototypeName(iKey));

						return false;
					}
				}
			}

			//N20190820-03 - Only compare Collections (no lexemes)
			else
			{
				if (proto1.Children.Count != proto2.Children.Count)
				{
					if (bLog)
						Logs.DebugLog.WriteEvent("Not Equal", proto1.PrototypeName + " Children vs " + proto2.PrototypeName + " Children");

					return false;
				}

				//N20200108-01
				if (proto1.Properties[Compare.Comparison.PrototypeID]?.PrototypeID != proto2.Properties[Compare.Comparison.PrototypeID]?.PrototypeID)
				{
					if (bLog)
						Logs.DebugLog.WriteEvent("Not Equal", proto1.PrototypeName + " Children.Comparison vs " + proto2.PrototypeName + " Children.Comparison");

					return false;
				}

				for (int i = 0; i < proto1.Children.Count; i++)
				{
					if (!AreEquivalentCircular(proto1.Children[i], proto2.Children[i], bLog, setHashes))
					{
						if (bLog)
							Logs.DebugLog.WriteEvent("Child Not Equal", i.ToString());

						return false;
					}
				}
			}

			return true;
		}

		static public bool AreEqualCircular(Prototype proto1, Prototype proto2)
		{
			return AreEqualCircular(proto1, proto2, false, new Set<int>());
		}

		static public bool AreEqualCircular(Prototype proto1, Prototype proto2, bool bLog, Set<int> setHashes)
		{
			if (proto1 == null && proto2 == null)
				return true;

			if (proto1 == null || proto2 == null)
				return false;

			if (setHashes.Contains(proto1.GetHashCode()))
				return true;

			setHashes.Add(proto1.GetHashCode());

			if (!Prototypes.AreShallowEqual(proto1, proto2))
			{
				if (bLog)
					Logs.DebugLog.WriteEvent("Not Equal", proto1.PrototypeName + " vs " + proto2.PrototypeName);

				return false;
			}

			//N20190501-02
			if (!Prototypes.TypeOf(proto1, Ontology.Collection.Prototype) || !Prototypes.TypeOf(proto2, Ontology.Collection.Prototype))
			{
				//N20190919-03 - Don't shortcircuit looking for prototype count since one can be partially loaded
				Set<int> setProperties = new Set<int>();
				foreach (var pair in proto1.NormalProperties)
				{
					setProperties.Add(pair.Key);
				}

				foreach (var pair in proto2.NormalProperties)
				{
					setProperties.Add(pair.Key);
				}

				foreach (int iKey in setProperties)
				{
					Prototype prop1 = proto1.Properties[iKey];
					Prototype prop2 = proto2.Properties[iKey];

					if (null == prop1 && null == prop2)
					{
						continue;
					}

					if (!AreEqualCircular(prop1, prop2, bLog, setHashes))
					{
						if (bLog)
							Logs.DebugLog.WriteEvent("Property Not Equal", Prototypes.GetPrototypeName(iKey));

						return false;
					}
				}
			}

			//N20190820-03 - Only compare Collections (no lexemes)
			else
			{
				if (proto1.Children.Count != proto2.Children.Count)
				{
					if (bLog)
						Logs.DebugLog.WriteEvent("Not Equal", proto1.PrototypeName + " Children vs " + proto2.PrototypeName + " Children");

					return false;
				}

				//N20200108-01
				if (proto1.Properties[Compare.Comparison.PrototypeID]?.PrototypeID != proto2.Properties[Compare.Comparison.PrototypeID]?.PrototypeID)
				{
					if (bLog)
						Logs.DebugLog.WriteEvent("Not Equal", proto1.PrototypeName + " Children.Comparison vs " + proto2.PrototypeName + " Children.Comparison");

					return false;
				}

				for (int i = 0; i < proto1.Children.Count; i++)
				{
					if (!AreEqualCircular(proto1.Children[i], proto2.Children[i], bLog, setHashes))
					{
						if (bLog)
							Logs.DebugLog.WriteEvent("Child Not Equal", i.ToString());

						return false;
					}
				}
			}

			return true;
		}


		public static void BreadthFirst(Prototype prototype, Func<Prototype, Prototype> func)
		{
			if (null != prototype)
			{
				func(prototype);

				foreach (var pair in prototype.Properties)
				{
					BreadthFirst(pair.Value, func);
				}

				for (int i = 0; i < prototype.Children.Count; i++)
				{
					BreadthFirst(prototype.Children[i], func);
				}
			}
		}


		public static void BreadthFirstWithControl(Prototype prototype, Func<Prototype, Prototype> func)
		{
			//This version allows the function to return the next object to operate on. Return a null to stop the search
			if (null != prototype)
			{
				prototype = func(prototype);

				if (null != prototype)
				{
					foreach (var pair in prototype.Properties)
					{
						BreadthFirstWithControl(pair.Value, func);
					}

					for (int i = 0; i < prototype.Children.Count; i++)
					{
						BreadthFirstWithControl(prototype.Children[i], func);
					}
				}
			}
		}
		public static void BreadthFirstWithControlOnNormal(Prototype prototype, Func<Prototype, Prototype> func)
		{
			//This version allows the function to return the next object to operate on. Return a null to stop the search
			if (null != prototype)
			{
				prototype = func(prototype);

				if (null != prototype)
				{
					foreach (var pair in prototype.NormalProperties)
					{
						BreadthFirstWithControlOnNormal(pair.Value, func);
					}

					for (int i = 0; i < prototype.Children.Count; i++)
					{
						BreadthFirstWithControlOnNormal(prototype.Children[i], func);
					}
				}
			}
		}

		public static Prototype? ComparePrototypes(Prototype prototype1, Prototype prototype2, bool bShallow = false)
		{
			Prototype? result = null;

			if (prototype1 == null || prototype2 == null)
				return null;

			if (Prototypes.AreShallowEqual(prototype1, prototype2))
			{
				result = prototype1.ShallowClone();
			}

			else
			{
				Prototype common = GetCommonRoot(prototype1, prototype2);
				if (null != common)
				{
					result = common.ShallowClone();
				}

				if (bShallow)
					return result;
			}

			if (null != result)
			{
				Set<int> setProperties = new Set<int>();
				setProperties.AddRange(prototype1.Properties.Select(x => x.Key));
				setProperties.AddRange(prototype2.Properties.Select(x => x.Key));

				foreach (var key in setProperties)
				{
					Prototype? protoValue1 = prototype1.Properties[key];
					Prototype? protoValue2 = prototype2.Properties[key];

					if (null != protoValue1 && null != protoValue2)
					{


						Prototype child = ComparePrototypes(protoValue1, protoValue2);

						//N20190426-08
						if (null == child)
						{
							result.Properties.Remove(key);
						}
						else
						{
							result.Properties[key] = child;
						}


					}
				}

				//N20191011-02 - Don't compare for non-collections, don't add Compare property
				if (Prototypes.TypeOf(prototype1, Ontology.Collection.Prototype))
				{
					//N20190418-01 - Preserve StartsWith when comparing against a shadow
					if (prototype1.Properties[Compare.Comparison.PrototypeID]?.PrototypeID == Compare.StartsWith.PrototypeID ||
						prototype2.Properties[Compare.Comparison.PrototypeID]?.PrototypeID == Compare.StartsWith.PrototypeID)
					{
						//naive comparison 
						for (int i = 0; i < prototype1.Children.Count && i < prototype2.Children.Count; i++)
						{
							Prototype child = ComparePrototypes(prototype1.Children[i], prototype2.Children[i]);

							//May want to stop when they don't match at all 
							if (child == null)
								break;

							result.Children.Add(child);
						}

						result.Properties[Compare.Comparison.PrototypeID] = Compare.StartsWith.Prototype.Clone();
					}

					//Preserve Interesection when comparing a shadow
					else if (prototype1.Properties[Compare.Comparison.PrototypeID]?.PrototypeID == Compare.Intersection.PrototypeID ||
						prototype2.Properties[Compare.Comparison.PrototypeID]?.PrototypeID == Compare.Intersection.PrototypeID)
					{
						//For intersections there should only be a single element
						for (int i = 0; i < prototype1.Children.Count && i < prototype2.Children.Count; i++)
						{
							Prototype child = ComparePrototypes(prototype1.Children[i], prototype2.Children[i]);

							//May want to stop when they don't match at all 
							if (child == null)
								break;

							result.Children.Add(child);
						}

						result.Properties[Compare.Comparison.PrototypeID] = Compare.Intersection.Prototype.Clone();
					}

					else if (prototype1.Children.Count > 0 || prototype2.Children.Count > 0)
					{
						//naive comparison 
						for (int i = 0; i < prototype1.Children.Count && i < prototype2.Children.Count; i++)
						{
							Prototype child = ComparePrototypes(prototype1.Children[i], prototype2.Children[i]);

							//May want to stop when they don't match at all 
							if (child == null)
								break;

							result.Children.Add(child);
						}

						if ((result.Children.Count != prototype1.Children.Count || result.Children.Count != prototype2.Children.Count))
						{
							//N20181218-01
							result.Properties[Compare.Comparison.PrototypeID] = Compare.StartsWith.Prototype.Clone();
						}

						else
						{
							result.Properties[Compare.Comparison.PrototypeID] = Compare.Exact.Prototype.Clone();
						}
					}

					//N20190330-02, N20200214-02
					else if (prototype1.Children.Count == 0 && prototype2.Children.Count == 0)
					{
						result.Properties[Compare.Comparison.PrototypeID] = Compare.Exact.Prototype.Clone();
					}
				}

			}

			return result;
		}



		public static void DepthFirst(Prototype prototype, Func<Prototype, Prototype> func)
		{
			//Use this version when the function can modify the graph, so it doesn't stack overflow

			if (null != prototype)
			{
				foreach (var pair in prototype.Properties)
				{
					DepthFirst(pair.Value, func);
				}

				for (int i = 0; i < prototype.Children.Count; i++)
				{
					DepthFirst(prototype.Children[i], func);
				}

				func(prototype);

			}
		}

		public static void DepthFirstOnNormal(Prototype prototype, Func<Prototype, Prototype> func)
		{
			//Use this version when the function can modify the graph, so it doesn't stack overflow
			DepthFirstOnNormalCircular(prototype, func, new Set<int>());
		}

		public static void DepthFirstOnNormalCircular(Prototype prototype, Func<Prototype, Prototype> func, Set<int> setHashes)
		{
			//Use this version when the function can modify the graph, so it doesn't stack overflow

			if (null != prototype)
			{
				if (setHashes.Contains(prototype.GetHashCode()))
					return;

				setHashes.Add(prototype.GetHashCode());

				foreach (var pair in prototype.NormalProperties)
				{
					DepthFirstOnNormalCircular(pair.Value, func, setHashes);
				}

				for (int i = 0; i < prototype.Children.Count; i++)
				{
					DepthFirstOnNormalCircular(prototype.Children[i], func, setHashes);
				}

				func(prototype);

			}
		}

		public static Prototype? GetCommonRoot(Prototype prototype1, Prototype prototype2)
		{
			if (prototype1 == null || prototype2 == null)
				return null;

			if (Prototypes.TypeOf(prototype2, prototype1))
				return prototype1.ShallowClone();

			if (Prototypes.TypeOf(prototype1, prototype2))
				return prototype2.ShallowClone();

			List<Prototype> lstCommonRoots = PrototypeGraphs.GetCommonRoots(prototype1, prototype2);
			Prototype? protoCommonRoot = null;
			foreach (Prototype protoRoot in lstCommonRoots)
			{
				if (null == protoCommonRoot)
				{
					protoCommonRoot = protoRoot.ShallowClone();
				}

				else if (!Prototypes.TypeOf(protoCommonRoot, protoRoot))
				{
					protoCommonRoot.InsertTypeOf(protoRoot.ShallowClone());
				}
			}

			return protoCommonRoot;
		}


		public static List<Prototype> GetCommonRoots(Prototype prototype1, Prototype prototype2)
		{
			List<Prototype> lstResults = new List<Prototype>();

			if (prototype1 == null || prototype2 == null)
				return lstResults;

			if (Prototypes.TypeOf(prototype2, prototype1))
				lstResults.Add(prototype1.ShallowClone());

			List<int> lstParents1 = new List<int>(prototype1.GetAllParents());
			foreach (int protoParent in prototype2.GetAllParents())
			{
				if (lstParents1.Contains(protoParent))
				{
					lstResults.Add(Prototypes.GetPrototype(protoParent).ShallowClone());
				}
			}

			return lstResults;
		}

		static public Prototype GetIntersection(Prototype protoCollection1, Prototype protoCollection2)
		{
			return CollectionUtil.GetIntersection(protoCollection1, protoCollection2);
		}

		static public List<Prototype> GetIntersection(List<Prototype> lstPrototypes1, List<Prototype> lstPrototypes2)
		{
			List<Prototype> lstResults = new List<Ontology.Prototype>();

			foreach (Prototype protoChild in lstPrototypes1)
			{
				if (lstPrototypes2.Any(x => AreEqual(protoChild, x)))
					lstResults.Add(protoChild);
			}

			return lstResults;
		}

		public static Prototype? GetValue(Prototype prototype, Prototype path)
		{
			if (path.PrototypeID == Compare.Entity.PrototypeID)
				return prototype;

			if (null == prototype)
				return null;

			if (Prototypes.TypeOf(prototype, path))      //should always match 
			{
				foreach (var pair in path.NormalProperties)           //should only be one
				{
					if (pair.Value.PrototypeID == Compare.Intersection.PrototypeID)
					{
						throw new NotImplementedException();
					}

					if (pair.Value.PrototypeID == Compare.Exact.PrototypeID)
						continue;

					Prototype? protoValue = prototype.Properties[pair.Key];
					if (null == protoValue)
					{
						return null;
					}

					return GetValue(protoValue, path.Properties[pair.Key]);
				}

				if (path.Children.Count > 0)
				{
					//N20200630-03 - If the prototype doesn't have the child then returna null
					if (path.Children.Count > prototype.Children.Count)
						return null;

					//This doesn't really need to iterate, just graph the last element of each 
					{
						Prototype childPath = path.Children.Last();
						Prototype child = prototype.Children[path.Children.Count - 1];

						return GetValue(child, childPath);
					}
				}

				//I added this because it was descending into a typeof collection as a dead end
				if (path.PrototypeID != Compare.Entity.PrototypeID)
					throw new Exception("Path ended without an Entity, possible error");

				return prototype;
			}

			return null;
		}


		public static Collection Find(Prototype prototype, Predicate<Prototype> func)
		{
			List<Prototype> lstResults = new List<Prototype>();

			PrototypeGraphs.DepthFirstOnNormal(prototype, x =>
			{
				if (func(x))
					lstResults.Add(x);

				return x;
			});

			return new Collection(lstResults);
		}

		public static Prototype FindParentOrNull(Prototype prototype, Prototype protoLeaf)
		{
			return Find(prototype, x => x.Properties.Any(y => y.Value == protoLeaf) || x.Children.Any(y => y == protoLeaf)).Children.FirstOrDefault();
		}

		static public List<Prototype> FindParents(Prototype prototype, Func<Prototype, bool> func)
		{
			List<Prototype> lstResults = new List<Prototype>();

			foreach (var pair in prototype.Properties)
			{
				if (func(pair.Value))
				{
					lstResults.Add(prototype);
					//return lstResults;
				}
				else
				{
					lstResults.AddRange(FindParents(pair.Value, func));
				}
			}

			foreach (Prototype protoChild in prototype.Children)
			{
				if (func(protoChild))
				{
					lstResults.Add(prototype);
					//return lstResults;
				}
				else
				{
					lstResults.AddRange(FindParents(protoChild, func));
				}
			}

			return lstResults;
		}

		static public List<Prototype> FindUniqueParents(Prototype prototype, Func<Prototype, bool> func)
		{
			List<Prototype> lstResults = new List<Prototype>();

			void AddUnique(Prototype p)
			{
				if (!lstResults.Any(x => x == p))
					lstResults.Add(p);
			}

			void Recurse(Prototype p)
			{
				foreach (var pair in p.Properties)
				{
					Prototype? v = pair.Value;
					if (v == null)
						continue;

					if (func(v))
					{
						AddUnique(p);
						return;
					}
					Recurse(v);
				}

				foreach (Prototype child in p.Children)
				{
					if (func(child))
					{
						AddUnique(p);
						return;
					}

					Recurse(child);
				}
			}

			Recurse(prototype);

			return lstResults;
		}


		static public List<Prototype> FindOrphanedLeaves(Prototype prototype, Prototype shadow)
		{
			List<Prototype> lstParameters = new List<Prototype>();

			if (prototype == null)
				throw new Exception("Shadow contains orphaned leaves");

			if (shadow == null)
			{
				lstParameters.Add(Compare.Entity.Prototype);
				return lstParameters;
			}

			//N20191112-01, N20191206-02
			if (Prototypes.TypeOf(shadow, "NL.AnonymousObject") ^ Prototypes.TypeOf(prototype, "NL.AnonymousObject"))
				return lstParameters;

			//N20190426-01 - Make sure there is a common base type here
			if (!Prototypes.TypeOf(prototype, shadow) && !Prototypes.TypeOf(shadow, prototype))
				throw new Exception("Prototype and shadow must match except leaves");

			foreach (var pair in prototype.Properties)
			{
				if (pair.Key == Compare.Comparison.PrototypeID)
					continue;

				List<Prototype> lstChildParameters = FindOrphanedLeaves(prototype.Properties[pair.Key], shadow.Properties[pair.Key]);

				////Build the paths as we pop back up the tree
				for (int i = 0; i < lstChildParameters.Count; i++)
				{
					Prototype child = lstChildParameters[i];

					Prototype prototypePath = shadow.ShallowClone();
					prototypePath.Properties[pair.Key] = child;

					lstParameters.Add(prototypePath);
				}

			}


			Prototype protoChildren = shadow.ShallowClone();

			for (int i = 0; i < shadow.Children.Count && i < prototype.Children.Count; i++)
			{
				Prototype child = prototype.Children[i];
				Prototype childShadow = shadow.Children[i];

				List<Prototype> lstChildParameters = FindOrphanedLeaves(child, childShadow);

				//Build the children collection as we pop back up the tree
				foreach (Prototype childParameter in lstChildParameters)
				{
					Prototype prototypePath = shadow.ShallowClone();
					for (int j = 0; j < i; j++)
						prototypePath.Children.Add(Compare.Ignore.Prototype.Clone());

					prototypePath.Children.Add(childParameter);

					//Keep the child parameters separate until after this node has been added so that it doesn't add
					//this node to parameters on a different path -- I don't know if it would anyways	
					lstParameters.Add(prototypePath);
				}

				protoChildren.Children.Add(Compare.Ignore.Prototype.Clone());
			}

			//N20190809-04 - Remove excess children
			for (int i = shadow.Children.Count; i < prototype.Children.Count; i++)
			{
				protoChildren.Children.Add(Compare.Entity.Prototype.Clone());
			}

			if (shadow.Children.Count < prototype.Children.Count)
				lstParameters.Add(protoChildren);

			return lstParameters;
		}


		public static List<Prototype> GetLeaves(Prototype prototype)
		{
			List<Prototype> lstPrototypes = new List<Prototype>();

			PrototypeGraphs.BreadthFirst(prototype, x =>
			{
				if (IsLeaf(x))
					lstPrototypes.Add(x);

				return x;
			});

			return lstPrototypes;
		}

		public static List<Prototype> GetLeavesOnNormal(Prototype prototype)
		{
			List<Prototype> lstPrototypes = new List<Prototype>();

			PrototypeGraphs.DepthFirstOnNormal(prototype, x =>
			{
				if (IsNormalLeaf(x))
					lstPrototypes.Add(x);

				return x;
			});

			return lstPrototypes;
		}

		public static List<Prototype> GetLeafPaths(Prototype prototype)
		{
			List<Prototype> lstPaths = new List<Prototype>();

			if (PrototypeGraphs.IsLeaf(prototype))
			{
				lstPaths.Add(Compare.Entity.Prototype);
				return lstPaths;
			}
			else
			{
				foreach (var pair in prototype.Properties)
				{
					List<Prototype> lstChildPaths = GetLeafPaths(pair.Value);
					foreach (Prototype protoChildPath in lstChildPaths)
					{
						Prototype protoPath = prototype.ShallowClone();
						protoPath.Properties[pair.Key] = protoChildPath;
						lstPaths.Add(protoPath);
					}
				}

				for (int i = 0; i < prototype.Children.Count; i++)
				{
					List<Prototype> lstChildPaths = GetLeafPaths(prototype.Children[i]);
					foreach (Prototype protoChildPath in lstChildPaths)
					{
						Prototype protoPath = prototype.ShallowClone();
						for (int j = 0; j < i; j++)
							protoPath.Children.Add(Compare.Ignore.Prototype);

						protoPath.Children.Add(protoChildPath);

						lstPaths.Add(protoPath);
					}

				}
			}

			return lstPaths;
		}

		public static bool IsLeaf(Prototype prototype)
		{
			return prototype.Children.Count == 0 && prototype.Properties.Count == 0;
		}
		public static bool IsNormalLeaf(Prototype prototype)
		{
			return prototype.Children.Count == 0 && prototype.NormalProperties.Count() == 0;
		}

		static public List<Prototype?> GetEntitiesOrNull(Prototype prototype, List<Prototype> lstPaths)
		{
			List<Prototype?> lstEntities = new List<Prototype?>();

			foreach (Prototype path in lstPaths)
			{
				Prototype? protoEntity = PrototypeGraphs.GetValue(prototype, path);
				lstEntities.Add(protoEntity);
			}

			return lstEntities;
		}

		static public string GetHash(Prototype prototype)
		{
			string strJSON = prototype.ToJSON(true);
			string strName = StringUtil.GetMD5(strJSON);
			return strName;
		}

		public static Prototype GetParentPath(Prototype protoPath)
		{
			Prototype protoParentPath = protoPath.Clone();

			foreach (var pair in protoPath.Properties)
			{
				Prototype protoChildPath = GetParentPath(pair.Value);
				if (null == protoChildPath)
				{
					protoParentPath = Compare.Entity.Prototype;
				}
				else
				{
					protoParentPath.Properties[pair.Key] = protoChildPath;
				}

				return protoParentPath;
			}

			for (int i = 0; i < protoPath.Children.Count; i++)
			{
				Prototype protoChild = protoPath.Children[i];

				if (protoChild.PrototypeID != Compare.Ignore.PrototypeID)
				{
					Prototype protoChildPath = GetParentPath(protoChild);
					if (null == protoChild)
					{
						protoParentPath = Compare.Entity.Prototype;
					}
					else
					{
						protoParentPath.Children[i] = protoChildPath;
					}

					return protoParentPath;
				}
			}

			return null;
		}



		static public List<Prototype> Minus(Prototype prototype, Prototype shadow, bool bShallow = false)
		{
			return MinusCircular(prototype, shadow, new Set<int>(), bShallow);
		}
		static public List<Prototype> MinusCircular(Prototype prototype, Prototype shadow, Set<int> setHashes, bool bShallow = false)
		{

			List<Prototype> lstParameters = new List<Prototype>();
			if (setHashes.Contains(prototype.GetHashCode()))
				return lstParameters;

			setHashes.Add(prototype.GetHashCode());

			//N20190109-01 - Continue comparing past a match
			if (Prototypes.TypeOf(prototype, shadow))
			{
				if (!Prototypes.AreShallowEqual(prototype, shadow))
				{
					lstParameters.Add(Compare.Entity.Prototype.Clone());
					if (bShallow)
						return lstParameters;
				}


				foreach (var pair in prototype.Properties)
				{
					if (shadow.Properties[pair.Key] == null && pair.Value != null)
					{
						Prototype prototypePath = shadow.ShallowClone();
						prototypePath.Properties[pair.Key] = Compare.Entity.Prototype.Clone();
						lstParameters.Add(prototypePath);
					}
				}

				foreach (var pair in shadow.Properties)
				{
					if (prototype.Properties[pair.Key] != null)
					{
						if (pair.Value != null)
						{
							List<Prototype> lstChildParameters = MinusCircular(prototype.Properties[pair.Key], pair.Value, setHashes, bShallow);

							////Build the paths as we pop back up the tree
							for (int i = 0; i < lstChildParameters.Count; i++)
							{
								Prototype child = lstChildParameters[i];

								Prototype prototypePath = shadow.ShallowClone();
								prototypePath.Properties[pair.Key] = child;

								lstParameters.Add(prototypePath);
							}
						}
					}
				}


				//N-20181231-01, N20181218-01
				if (Prototypes.TypeOf(shadow, Ontology.Collection.Prototype) &&
					shadow.Properties[Compare.Comparison.PrototypeID]?.PrototypeID == Compare.StartsWith.PrototypeID)
				{
					//Entity can be added by the block above
					if (!lstParameters.Any(x => x.PrototypeID == Compare.Entity.PrototypeID))
					{
						lstParameters.Add(Compare.Entity.Prototype.Clone());
					}
				}
				//N20190420-01
				else if (shadow.Children.Count == 0 && shadow.Properties[Compare.Comparison.PrototypeID]?.PrototypeID == Compare.Exact.PrototypeID)
				{
					//Entity can be added by the block above
					if (!lstParameters.Any(x => x.PrototypeID == Compare.Entity.PrototypeID))
					{
						lstParameters.Add(Compare.Entity.Prototype.Clone());
					}
				}

				//N20190815-03
				else if (Prototypes.TypeOf(shadow, Ontology.Collection.Prototype))
				{
					for (int i = 0; i < shadow.Children.Count && i < prototype.Children.Count; i++)
					{
						Prototype child = prototype.Children[i];
						Prototype childShadow = shadow.Children[i];

						List<Prototype> lstChildParameters = MinusCircular(child, childShadow, setHashes, bShallow);

						//Build the children collection as we pop back up the tree
						foreach (Prototype childParameter in lstChildParameters)
						{
							Prototype prototypePath = shadow.ShallowClone();
							for (int j = 0; j < i; j++)
								prototypePath.Children.Add(Compare.Ignore.Prototype.Clone());

							prototypePath.Children.Add(childParameter);

							//Keep the child parameters separate until after this node has been added so that it doesn't add
							//this node to parameters on a different path -- I don't know if it would anyways	
							lstParameters.Add(prototypePath);
						}
					}

					for (int i = shadow.Children.Count; i < prototype.Children.Count; i++)
					{
						Prototype prototypePath = shadow.ShallowClone();
						for (int j = 0; j < i; j++)
							prototypePath.Children.Add(Compare.Ignore.Prototype.Clone());

						prototypePath.Children.Add(Compare.Entity.Prototype.Clone());

						lstParameters.Add(prototypePath);
					}
				}
			}

			else if (!Prototypes.AreShallowEqual(prototype, shadow))
			{
				lstParameters.Add(Compare.Entity.Prototype.Clone());
			}

			return lstParameters;
		}

		//N20190817-01 - Find values in the prototype not in the shadow
		static public List<Prototype> MinusValues(Prototype prototype, Prototype shadow)
		{
			List<Prototype> lstParameters = new List<Prototype>();

			//N20190109-01 - Continue comparing past a match
			if (Prototypes.TypeOf(prototype, shadow))
			{
				if (!Prototypes.AreShallowEqual(prototype, shadow))
				{
					//20190921-01 - Get a shallow difference
					lstParameters.Add(prototype.ShallowClone());
					return lstParameters;
				}


				foreach (var pair in prototype.Properties)
				{
					//N20191105-01 - don't return the Comparison as the difference
					if (shadow.Properties[pair.Key] == null && pair.Key != Compare.Comparison.PrototypeID)
					{
						Prototype prototypePath = shadow.ShallowClone();
						prototypePath.Properties[pair.Key] = pair.Value.ShallowClone(); //Use shallow here
						lstParameters.Add(prototypePath);
					}
				}

				foreach (var pair in shadow.Properties)
				{
					if (prototype.Properties[pair.Key] != null)
					{
						if (pair.Value != null)
						{
							List<Prototype> lstChildParameters = MinusValues(prototype.Properties[pair.Key], pair.Value);

							////Build the paths as we pop back up the tree
							for (int i = 0; i < lstChildParameters.Count; i++)
							{
								Prototype child = lstChildParameters[i];

								Prototype prototypePath = shadow.ShallowClone();
								prototypePath.Properties[pair.Key] = child;

								lstParameters.Add(prototypePath);
							}
						}
					}
				}

				if (Prototypes.TypeOf(shadow, Ontology.Collection.Prototype))
				{
					for (int i = 0; i < shadow.Children.Count && i < prototype.Children.Count; i++)
					{
						Prototype child = prototype.Children[i];
						Prototype childShadow = shadow.Children[i];

						List<Prototype> lstChildParameters = MinusValues(child, childShadow);

						//Build the children collection as we pop back up the tree
						foreach (Prototype childParameter in lstChildParameters)
						{
							Prototype prototypePath = shadow.ShallowClone();
							for (int j = 0; j < i; j++)
								prototypePath.Children.Add(Compare.Ignore.Prototype.Clone());

							prototypePath.Children.Add(childParameter);

							//Keep the child parameters separate until after this node has been added so that it doesn't add
							//this node to parameters on a different path -- I don't know if it would anyways	
							lstParameters.Add(prototypePath);
						}
					}

					for (int i = shadow.Children.Count; i < prototype.Children.Count; i++)
					{
						Prototype prototypePath = shadow.ShallowClone();
						prototypePath.Properties[Compare.Comparison.PrototypeID] = Compare.StartsWith.Prototype.Clone();
						for (int j = 0; j < i; j++)
							prototypePath.Children.Add(Compare.Ignore.Prototype.Clone());

						prototypePath.Children.Add(prototype.Children[i].ShallowClone());

						lstParameters.Add(prototypePath);
					}
				}
			}

			//N20191203-05
			else if (Prototypes.TypeOf(shadow, prototype))
			{
				foreach (var pair in prototype.Properties)
				{
					//N20191105-01 - don't return the Comparison as the difference
					if (shadow.Properties[pair.Key] == null && pair.Key != Compare.Comparison.PrototypeID)
					{
						//This is different than above, it should use the "prototype" since the shadow typeof prototype
						Prototype prototypePath = prototype.ShallowClone();
						prototypePath.Properties[pair.Key] = pair.Value.ShallowClone(); //Use shallow here
						lstParameters.Add(prototypePath);
					}
				}
			}

			else if (!Prototypes.AreShallowEqual(prototype, shadow))
			{
				lstParameters.Add(prototype.ShallowClone());
			}

			return lstParameters;
		}


		static public List<Prototype> Parameterize(Prototype prototype, Prototype shadow, bool bShallow = false)
		{
			return Parameterize(new List<Prototype> { prototype }, shadow, bShallow);
		}

		static public List<Prototype> Parameterize(List<Prototype> lstPrototypes, Prototype shadow, bool bShallow = false)
		{
			//This version is capable of using all prototype examples in case one of the prototypes has multiple matches.
			//However, the prototypes should have the same overall structure
			List<Prototype> lstParameters = new List<Prototype>();

			Prototype prototype = lstPrototypes.First();


			//N20231006-01 - Allow use to mark the entities with Compare.Entity in the shadow
			//N20260110-01 - Moved this first, so we can non-destructively mark a graph with Compare.Entity to indicate parameters
			if (shadow.TypeOf(Compare.Entity.Prototype))
			{
				lstParameters.Add(Compare.Entity.Prototype.Clone());
				return lstParameters;
			}

			//N20190109-01 - Continue comparing past a match
			else if (Prototypes.TypeOf(prototype, shadow))
			{
				//N20260116-01
				//We want bShallow=false to return the *deepest* non-matching nodes (not every leaf).
				//So we detect whether THIS node mismatches, but only return Compare.Entity at THIS node if there are
				//no deeper mismatches. If bShallow=true, return immediately on first mismatch boundary.
				//N20260117-01
				//Switched to ShallowEquivalent because it was pulling every composite object as different
				bool bMismatchHere = false;

				if (lstPrototypes.Any(x => !x.ShallowEquivalent(shadow)))
				{
					bMismatchHere = true;
					if (bShallow)
					{
						lstParameters.Add(Compare.Entity.Prototype.Clone());
						return lstParameters;
					}
				}

				//N20190122-01
				foreach (var pair in prototype.Properties)
				{
					if (shadow.Properties[pair.Key] == null && pair.Value != null)
					{
						//Entity can be added by the block above
						bMismatchHere = true;

						if (bShallow)
						{
							lstParameters.Add(Compare.Entity.Prototype.Clone());
							return lstParameters;
						}

						break;
					}
				}

				//N20260116-01
				//Collect deeper parameters separately. If any deeper mismatches exist, return only those.
				List<Prototype> lstDeepParameters = new List<Prototype>();

				foreach (var pair in shadow.Properties)
				{
					if (pair.Key == Compare.Comparison.PrototypeID)
						continue;

					if (prototype.Properties[pair.Key] != null)
					{
						{
							List<Prototype> lstChildPrototypes = lstPrototypes.Select(x => x.Properties[pair.Key]).ToList();
							List<Prototype> lstChildParameters = Parameterize(lstChildPrototypes, pair.Value, bShallow);

							////Build the paths as we pop back up the tree
							for (int i = 0; i < lstChildParameters.Count; i++)
							{
								Prototype child = lstChildParameters[i];

								Prototype prototypePath = shadow.ShallowClone();
								prototypePath.Properties[pair.Key] = child;

								lstDeepParameters.Add(prototypePath);
							}
						}
					}
				}



				//N20190914-01
				if (Prototypes.TypeOf(prototype, Ontology.Collection.Prototype))
				{
					if (Prototypes.TypeOf(prototype, Ontology.Collection.Prototype) &&
						(shadow.Properties[Compare.Comparison.PrototypeID]?.PrototypeID ?? Compare.Exact.PrototypeID) == Compare.Exact.PrototypeID)
					{
						int expected = prototype.Children.Count;
						if (lstPrototypes.Any(x => x.Children.Count != expected))
						{
							bMismatchHere = true;
							if (bShallow)
							{
								lstParameters.Add(Compare.Entity.Prototype.Clone());
								return lstParameters;
							}
						}
					}

					for (int i = 0; i < shadow.Children.Count && i < prototype.Children.Count; i++)
					{
						List<Prototype> lstChildren = lstPrototypes.Select(x => x.Children[i]).ToList();
						Prototype childShadow = shadow.Children[i];

						List<Prototype> lstChildParameters = Parameterize(lstChildren, childShadow, bShallow);

						//Build the children collection as we pop back up the tree
						foreach (Prototype childParameter in lstChildParameters)
						{
							Prototype prototypePath = shadow.ShallowClone();
							for (int j = 0; j < i; j++)
								prototypePath.Children.Add(Compare.Ignore.Prototype.Clone());

							prototypePath.Children.Add(childParameter);

							//Keep the child parameters separate until after this node has been added so that it doesn't add
							//this node to parameters on a different path -- I don't know if it would anyways	
							lstDeepParameters.Add(prototypePath);
						}
					}
				}

				//N20260116-01
				//If we found any deeper mismatches, return ONLY those (deepest-only behavior).
				if (lstDeepParameters.Count > 0)
				{
					lstParameters.AddRange(lstDeepParameters);
					return lstParameters;
				}

				//N20260116-01
				//No deeper mismatches exist; if this node mismatched, return Compare.Entity at this node boundary.
				if (bMismatchHere)
				{
					lstParameters.Add(Compare.Entity.Prototype.Clone());
					return lstParameters;
				}

				//If the node matches and nothing below differs, return no parameters.
			}

			//If prototype doesn't categorize under shadow, this is a mismatch boundary at this node.
			else
			{
				lstParameters.Add(Compare.Entity.Prototype.Clone());
			}

			return lstParameters;
		}




		static public Prototype RemovePath(Prototype prototype, Prototype path)
		{
			if (Prototypes.TypeOf(prototype, path))      //should always match 
			{
				foreach (var pair in path.Properties)           //should only be one
				{
					Prototype protoValue = prototype.Properties[pair.Key];

					if (pair.Value.PrototypeID == Compare.Entity.PrototypeID)
					{
						prototype.Properties.Remove(pair.Key);
					}

					else
					{
						RemovePath(protoValue, pair.Value);
					}
				}

				for (int i = prototype.Children.Count - 1; i >= 0; i--)
				{
					if (i >= path.Children.Count)
						continue;

					Prototype childPath = path.Children[i];
					Prototype child = prototype.Children[i];

					if (childPath.PrototypeID == Compare.Ignore.PrototypeID)
						continue;

					//N20190809-04 - Remove excess children (but not if the Child does not exist in path)
					if (childPath.PrototypeID == Compare.Entity.PrototypeID)
					{
						prototype.Children.RemoveAt(i);
						continue;
					}

					RemovePath(child, childPath);
				}

				return prototype;
			}

			throw new Exception($"Prototype {prototype.PrototypeName} does not match path {path.PrototypeName}");
		}


		static public Prototype Merge(Prototype shadow, Prototype value)
		{
			//See N20190425-02 for expected behavior

			if (shadow == null && value == null)
				return null; //N20190820-04

			if (shadow == null)
				return value.Clone();

			if (value == null)
				return shadow.Clone();

			if (value.PrototypeID == Compare.Ignore.PrototypeID && shadow.PrototypeID == Compare.Ignore.PrototypeID)
				return Compare.Ignore.Prototype;
			//throw new Exception("Both shadow and value are to be ignored");

			if (value.PrototypeID == Compare.Ignore.PrototypeID)
				return shadow.Clone();

			if (shadow.PrototypeID == Compare.Ignore.PrototypeID)
				return value.Clone();

			Prototype protoResult = null;

			//N20190426-02 - Use the more specific version 
			if (Prototypes.TypeOf(value, shadow))
				protoResult = value.ShallowClone();

			else if (Prototypes.TypeOf(shadow, value))
				protoResult = shadow.ShallowClone();

			else
				protoResult = value.ShallowClone();

			Set<int> setProperties = new Set<int>();
			foreach (var pair in shadow.Properties)
			{
				//Don't copy the expected type from the entity if it exists
				if (pair.Key == Compare.Entity.PrototypeID)
					continue;

				setProperties.Add(pair.Key);
			}

			foreach (var pair in value.Properties)
			{
				setProperties.Add(pair.Key);
			}

			foreach (int iKey in setProperties)
			{
				Prototype propShadow = shadow.Properties[iKey];
				Prototype propValue = value.Properties[iKey];

				protoResult.Properties[iKey] = Merge(propShadow, propValue);
			}

			//N20190426-06
			for (int i = 0; i < value.Children.Count; i++)
			{
				if (i >= shadow.Children.Count)
					protoResult.Children.Add(value.Children[i]);
				else
					protoResult.Children.Add(Merge(shadow.Children[i], value.Children[i]));
			}

			//Merge any additional values from the shadow
			for (int i = value.Children.Count; i < shadow.Children.Count; i++)
			{
				protoResult.Children.Add(shadow.Children[i]);
			}

			return protoResult;
		}

		static public Prototype RemoveComparisonOperations(Prototype shadow)
		{
			List<int> lstToRemove = new List<int>();
			foreach (var pair in shadow.Properties)
			{
				if (pair.Key == Ontology.Compare.Comparison.PrototypeID)
					lstToRemove.Add(pair.Key);

				else if (null != pair.Value)
					RemoveComparisonOperations(pair.Value);
			}

			foreach (int iKey in lstToRemove)
				shadow.Properties.Remove(iKey);

			foreach (var child in shadow.Children)
			{
				RemoveComparisonOperations(child);
			}

			return shadow;
		}


		static public Prototype SetValue(Prototype prototype, Prototype path, Prototype value, bool bMergeAugmentedProperties = false, bool bMerge = true)
		{
			Prototype result = null;

			if (path.PrototypeID == Compare.Entity.PrototypeID)
			{
				if (null == prototype)
					result = value;

				else if (null == value)
					result = value;

				//N20190425-03
				else if (value.PrototypeID == Compare.Ignore.Prototype.PrototypeID)
					result = value;

				else
					//N20191018-05
					result = bMerge ? Merge(prototype, value) : value;
			}

			else if (path.Children.Count > 0)
			{
				if (path.Properties[Compare.Comparison.PrototypeID]?.PrototypeID == Compare.Intersection.PrototypeID)
				{
					//N20190423-03
					if (path.Children.Count == 1 && path.Children[0].PrototypeID == Compare.Entity.PrototypeID)
					{
						if (prototype.Children.Count == 1 && prototype.Children[0].PrototypeID == Compare.Entity.PrototypeID)
						{
							prototype.Children[0] = value;
							result = prototype;
						}
						else if (!prototype.Children.Any(x => PrototypeGraphs.AreEqual(x, value)))
						{
							prototype.Children.Add(value);
							result = prototype;
						}
						else
						{
							result = prototype; //Note: This seems like it should be result = value here
						}
					}
					else if (path.Children.Count == 1)
					{
						//N20191105-01
						if (!prototype.Children.Any(x => PrototypeGraphs.AreEqual(x, value)))
						{
							prototype.Properties[Compare.Comparison.PrototypeID] = Compare.Intersection.Prototype.Clone();
							prototype.Children.Add(value);
							result = prototype;
						}
					}
					else
					{
						for (int i = 0; i < prototype.Children.Count; i++)
						{
							prototype.Children[i] = SetValue(prototype.Children[i], path.Children[0], value, bMergeAugmentedProperties, bMerge);

							result = prototype;
						}
					}
				}
				else
				{
					int i = path.Children.Count - 1;       //Last element should be the only important one

					//MinimizeShadow can require adding ignores between paths
					for (int j = prototype.Children.Count; j < path.Children.Count - 1; j++)
					{
						prototype.Children.Add(Compare.Ignore.Prototype.Clone());
					}

					//N20190425-06
					if (prototype.Children.Count == path.Children.Count - 1)
					{
						Prototype protoResult = SetValue(path.Children[i].Clone(), path.Children[i], value, bMergeAugmentedProperties, bMerge);
						prototype.Children.Add(protoResult);
					}
					else
					{
						Prototype protoResult = SetValue(prototype.Children[i], path.Children[i], value, bMergeAugmentedProperties, bMerge);
						prototype.Children[i] = protoResult;
					}

					result = prototype;
				}
			}

			else if (path.Properties.Count > 0)
			{
				if (path.Properties.Count != 1)        //should only be one
					throw new Exception("The path has too may properties");

				var pairPath = path.Properties.First(); //first should work on a Dictionary

				//N20190424-05 - Do not set TypeOf here, it should be serialized to graph as normal
				{
					Prototype protoProp = prototype.Properties[pairPath.Key];

					//N20190510-03 - Add properties if needed
					if (protoProp == null)
					{
						protoProp = pairPath.Value.Clone();
					}

					Prototype protoResult = SetValue(protoProp, pairPath.Value, value, bMergeAugmentedProperties, bMerge);

					//N10 - Removed reversible properties here. They are done in a separate function 
					prototype.Properties[pairPath.Key] = protoResult;

					result = prototype;
				}
			}

			//N20191017-05 - Note this is a bug but it may be relied upon because we should be returning the value not the prototype 
			if (null == prototype)
				result = prototype;
			else if (result == null)
				result = prototype;

			return result;
		}


		static public int Size(Prototype p)
		{
			if (p == null)
				return 0;

			int cost = 1; // count this node

			// Count children
			foreach (Prototype child in p.Children)
				cost += Size(child);

			// Count property values
			foreach (var pair in p.Properties)
			{
				if (pair.Value != null)
					cost += Size(pair.Value);
			}

			return cost;
		}
	}
}

