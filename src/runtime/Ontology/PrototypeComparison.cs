namespace Ontology
{
	public class PrototypeComparison
	{
		public enum DifferenceTypes { Same, Differenct, Changed, Added, Deleted, Error };


		public class Result
		{
			public DifferenceTypes DifferenceType = DifferenceTypes.Same;

			public Prototype IsolatedOriginal = null;
			public Prototype IsolatedNew = null;

			public Prototype OriginalParent = null;
			public Prototype NewParent = null; 
		}


		private static Prototype GetParentPathOfProperty(Prototype protoParent, int iPropertyPrototypeID, Prototype protoChild)
		{
			Prototype protoPath = protoParent.ShallowClone();

			//Null child indicates the start of the path
			if (null != protoChild)
				protoPath.Properties[iPropertyPrototypeID] = protoChild;

			return protoPath;
		}

		private static Prototype GetParentPathOfChild(Prototype protoParent, Prototype protoChild)
		{
			Prototype protoPath = protoParent.ShallowClone();

			//Null child indicates the start of the path
			if (null != protoChild)
				protoPath.Children.Add(protoChild);

			return protoPath;
		}

		public static Result Compare(Prototype prototype1, Prototype prototype2)
		{
			if (!Prototypes.AreShallowEqual(prototype1, prototype2))
			{
				//As the Prototypes get written to the database they can get an instance 
				//CSharp.MethodEvaluation[UnderstandUtil.GetAndInitializeProtoScriptTagger(settings)]' vs 'CSharp.MethodEvaluation
				if (!prototype1.PrototypeName.StartsWith(prototype2.PrototypeName))
				{
					Result result = new Result();

					result.DifferenceType = DifferenceTypes.Differenct;
					result.IsolatedOriginal = prototype1;
					result.IsolatedNew = prototype2;

					Logs.DebugLog.WriteEvent("PrototypeComparison.Difference", "Different");
					//Logs.DebugLog.WriteEvent("IsolatedOriginal", JsonUtil.ToFriendlyJSON(prototype1.ToFriendlyJSON()));
					//Logs.DebugLog.WriteEvent("IsolatedNew", JsonUtil.ToFriendlyJSON(prototype2.ToFriendlyJSON()));

					return result;
				}
			}

			foreach (var pair in prototype1.Properties)
			{
				Result result = Compare(prototype1.Properties[pair.Key], prototype2.Properties[pair.Key]);
				if (result.DifferenceType != DifferenceTypes.Same)
				{
					Result result2 = new Result();
					result2.DifferenceType = result.DifferenceType;
					result2.IsolatedNew = result.IsolatedNew;
					result2.IsolatedOriginal = result.IsolatedOriginal;
					result2.OriginalParent = GetParentPathOfProperty(prototype1, pair.Key, result.OriginalParent);
					result2.NewParent = GetParentPathOfProperty(prototype2, pair.Key, result.NewParent);
					return result2;
				}
			}

			List<Prototype> lstChildren1 = prototype1.Children;
			List<Prototype> lstChildren2 = prototype2.Children;

			for (int i = 0; i < lstChildren1.Count && i < lstChildren2.Count; i++)
			{
				Prototype child1 = lstChildren1[i];
				Prototype child2 = lstChildren2[i];

				Result resultChild = Compare(child1, child2);
				if (resultChild.DifferenceType == DifferenceTypes.Same)
					continue;

				Result result = new Result();
				result.OriginalParent = GetParentPathOfChild(prototype1, resultChild.OriginalParent);
				result.NewParent = GetParentPathOfChild(prototype2, resultChild.NewParent);

				//N20200818-03 - the collection contain the change has been detected, then just bubble this up 
				if (resultChild.DifferenceType != DifferenceTypes.Differenct)
				{
					result.DifferenceType = resultChild.DifferenceType;
					result.IsolatedNew = resultChild.IsolatedNew;
					result.IsolatedOriginal = resultChild.IsolatedOriginal;
					return result;
				}

				//The rest tries to use the collection to determine if the context helps to isolate the type of change

				//look for statement added
				if (IsChildAdded(lstChildren1, lstChildren2, i))
				{
					//statement added
					result.DifferenceType = DifferenceTypes.Added;
					result.IsolatedNew = lstChildren2[i];
					return result;
				}

				//look for statement deleted
				if (IsChildDeleted(lstChildren1, lstChildren2, i))
				{
					result.DifferenceType = DifferenceTypes.Deleted;
					result.IsolatedOriginal = lstChildren1[i];
					return result;
				}

				var tuple = IsChildPasted(lstChildren1, lstChildren2, i);

				if (tuple.Item1)
				{
					result.DifferenceType = DifferenceTypes.Added;
					result.IsolatedOriginal = prototype1.ShallowClone();
					result.IsolatedNew = prototype2.ShallowClone();
					result.IsolatedNew.Children.AddRange(lstChildren2.GetRange(i, tuple.Item2 - i));
					return result;
				}

				var tuple2 = IsChildCut(lstChildren1, lstChildren2, i);

				if (tuple2.Item1)
				{
					result.DifferenceType = DifferenceTypes.Deleted;
					result.IsolatedOriginal = prototype1.ShallowClone();
					result.IsolatedOriginal.Children.AddRange(lstChildren1.GetRange(i, tuple2.Item2 - i));
					result.IsolatedNew = prototype2.ShallowClone();
					return result;
				}


				if (IsChildChanged(lstChildren1, lstChildren2, i))
				{
					result.DifferenceType = DifferenceTypes.Changed;
					result.IsolatedOriginal = lstChildren1[i];
					result.IsolatedNew = lstChildren2[i];
					return result;
				}

				//The collection context couldn't find the type of change, so bubble up to the next level 
				result.DifferenceType = resultChild.DifferenceType;
				return result;				
			}

			//Cut from the end 
			if (lstChildren1.Count > lstChildren2.Count)
			{
				Result result = new Result();
				result.DifferenceType = DifferenceTypes.Deleted;

				if (lstChildren1.Count == lstChildren2.Count + 1)
				{
					result.IsolatedOriginal = lstChildren1.Last();
				}
				else
				{
					result.IsolatedOriginal = prototype1.ShallowClone();
					result.IsolatedOriginal.Children.AddRange(lstChildren1.GetRange(lstChildren2.Count, lstChildren1.Count - lstChildren2.Count));
					result.IsolatedNew = prototype2.ShallowClone();
				}
				return result;
			}

			//Pasted at the end 
			else if (lstChildren2.Count > lstChildren1.Count)
			{
				Result result = new Result();
				result.DifferenceType = DifferenceTypes.Added;

				if (lstChildren2.Count == lstChildren1.Count + 1)
				{
					result.IsolatedNew = lstChildren2.Last();
				}
				else
				{
					result.IsolatedOriginal = prototype1.ShallowClone();
					result.IsolatedNew = prototype2.ShallowClone();
					result.IsolatedNew.Children.AddRange(lstChildren2.GetRange(lstChildren1.Count, lstChildren2.Count - lstChildren1.Count));
				}

				return result;
			}		

			return new Result() { DifferenceType = DifferenceTypes.Same };
		}


		static private bool IsChildAdded(List<Prototype> lstChildren1, List<Prototype> lstChildren2, int i)
		{
			if (lstChildren1.Count + 1 != lstChildren2.Count)
				return false;
				
			for (; i + 1 < lstChildren2.Count; i++)
			{
				Prototype child1 = lstChildren1[i];
				Prototype child2 = lstChildren2[i];
				Prototype child2Next = lstChildren2[i + 1];

				if (!PrototypeGraphs.AreEqual(child1, child2Next))
				{
					return false;
				}
			}

			return true;
		}

		static private bool IsChildDeleted(List<Prototype> lstChildren1, List<Prototype> lstChildren2, int i)
		{
			if (lstChildren1.Count != lstChildren2.Count + 1)
				return false;

			for (; i + 1 < lstChildren1.Count; i++)
			{
				Prototype child1 = lstChildren1[i];
				Prototype child1Next = lstChildren1[i + 1];

				Prototype child2 = lstChildren2[i];

				if (!PrototypeGraphs.AreEqual(child1Next, child2))
					return false;
			}

			return true;
		}

		static private bool IsChildChanged(List<Prototype> lstChildren1, List<Prototype> lstChildren2, int i)
		{
			if (lstChildren1.Count != lstChildren2.Count)
				return false;

			for (;  i + 1 < lstChildren1.Count && i + 1 < lstChildren2.Count; i++)
			{
				Prototype child1 = lstChildren1[i];
				Prototype child1Next = lstChildren1[i + 1];

				Prototype child2 = lstChildren2[i];
				Prototype child2Next = lstChildren2[i + 1];

				if (!PrototypeGraphs.AreEqual(child1Next, child2Next))
					return false;
			}

			return true;
		}

		static private Tuple<bool, int> IsChildPasted(List<Prototype> lstChildren1, List<Prototype> lstChildren2, int iStart)
		{
			bool bPasted = false;
			int iNextIndex = 0;

			if (lstChildren2.Count > lstChildren1.Count)
			{
				Prototype child1 = lstChildren1[iStart];
				Prototype child2 = lstChildren2[iStart];

				for (int i = iStart + 1; i < lstChildren2.Count; i++)
				{
					Prototype child2Next = lstChildren2[i];

					bPasted = PrototypeGraphs.AreEqual(child1, child2Next);

					if (bPasted)
					{
						iNextIndex = i;
						break;
					}
				}
			}

			return new Tuple<bool, int>(bPasted, iNextIndex);
		}

		static private Tuple<bool, int> IsChildCut(List<Prototype> lstChildren1, List<Prototype> lstChildren2, int iStart)
		{
			bool bCut = false;
			int iNextIndex = 0;

			if (lstChildren1.Count > lstChildren2.Count)
			{
				Prototype child1 = lstChildren1[iStart];
				Prototype child2 = lstChildren2[iStart];

				for (int i = iStart + 1; i < lstChildren1.Count; i++)
				{
					Prototype child1Next = lstChildren1[i];

					bCut = PrototypeGraphs.AreEqual(child1Next, child2);

					if (bCut)
					{
						iNextIndex = i;
						break;
					}
				}
			}

			return new Tuple<bool, int>(bCut, iNextIndex);
		}


	}

}
