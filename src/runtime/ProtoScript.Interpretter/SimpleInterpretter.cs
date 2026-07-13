using BasicUtilities;
using BasicUtilities.Collections;
using Ontology;
using Ontology.BaseTypes;
using Ontology.Simulation;
using ProtoScript.Interpretter.RuntimeInfo;
using ProtoScript.Interpretter.Symbols;

namespace ProtoScript.Interpretter
{
	public class SimpleInterpretter
	{
		static public Prototype NewPrototype(string strName)
		{
			Prototype prototype = TemporaryPrototypes.GetOrCreateTemporaryPrototype(strName);
			return prototype;
		}

		static public Prototype NewInstance(string strParent)
		{
			Prototype? prototype = TemporaryPrototypes.GetTemporaryPrototypeOrNull(strParent);
			
			if (null == prototype)
				throw new InvalidOperationException($"Cannot create instance of prototype '{strParent}' because it is not declared.");

			return NewInstance(prototype);
		}

		static public Prototype NewInstance(Prototype protoParent)
		{
			Prototype protoInstance = protoParent.CreateInstance();

			//if (protoParent is TemporaryPrototype)
			//	protoInstance = TemporaryPrototypes.CreateInstance(protoParent as TemporaryPrototype);
			//else
			//	protoInstance = protoParent.Clone();

			return protoInstance;
		}

		public static Tuple<Prototype, Prototype> ResolveProperty(Prototype prototype, string strSubObj)
		{
			//if (prototype.Properties.Contains(strSubObj))
			//	return


			{
				string strField = prototype.PrototypeName + ".Field." + strSubObj;
				Prototype? protoProp = TemporaryPrototypes.GetTemporaryPrototypeOrNull(strField);
				if (null != protoProp)
				{
					return new Tuple<Prototype, Prototype>(protoProp, prototype);
				}
			}

			//This version should be faster, and can handle circular inherits 
			foreach (int protoTypeOfID in prototype.GetAllParents())
			{
				Prototype protoTypeOf = Prototypes.GetPrototype(protoTypeOfID);
				string strField = protoTypeOf.PrototypeName + ".Field." + strSubObj;
				Prototype? protoProp = TemporaryPrototypes.GetTemporaryPrototypeOrNull(strField);
				if (null != protoProp)
				{
					return new Tuple<Prototype, Prototype>(protoProp, protoTypeOf);
				}
			}


			return null;
		}


		public static Tuple<Prototype, Prototype> ResolveMethod(Prototype prototype, string strSubObj)
		{
			string strField = prototype.PrototypeName + ".Method." + strSubObj;
			Prototype? protoProp = TemporaryPrototypes.GetTemporaryPrototypeOrNull(strField);
			if (null != protoProp)
			{
				return new Tuple<Prototype, Prototype>(protoProp, prototype);
			}

			//TODO: probably should GetAllParents
			foreach (int protoTypeOfID in prototype.GetTypeOfs())
			{
				Prototype protoTypeOf = Prototypes.GetPrototype(protoTypeOfID);

				var tuple = ResolveMethod(protoTypeOf, strSubObj);

				if (null != tuple)
				{
					return tuple;
				}
			}

			return null;
		}

	
		static public Prototype NewPrototypeField(Prototype prototype, string strPropertyName, Prototype protoType)
		{
			string strPropPrototypeName = prototype.PrototypeName + ".Field." + strPropertyName;
			Prototype protoProp1 = TemporaryPrototypes.GetOrCreateTemporaryPrototype(strPropPrototypeName);

			//N20220111-03 - Don't add to the base, so that we can create static properties dynamically
			//	prototype.Properties[protoProp1.PrototypeID] = protoType;

			return protoProp1;
		}

		static public bool AreEquivalentCircular(Prototype proto1, Prototype proto2)
		{
			//N20220207-01 - Differs from PrototypeGraphs.AreEquivalentCircular 
			//because it ignores Functions
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

			if (!Prototypes.AreShallowEqual(proto1, proto2))
			{
				string strPrototypeName1 = proto1.PrototypeName;
				if (proto1.PrototypeName.Contains("#"))
					strPrototypeName1 = StringUtil.LeftOfFirst(strPrototypeName1, "#");

				string strPrototypeName2 = proto2.PrototypeName;
				if (proto2.PrototypeName.Contains("#"))
					strPrototypeName2 = StringUtil.LeftOfFirst(strPrototypeName2, "#");

				if (strPrototypeName1 != strPrototypeName2)
				{
					if (bLog)
						Logs.DebugLog.WriteEvent("Not Equal", proto1.PrototypeName + " vs " + proto2.PrototypeName);

					return false;
				}
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

					if (null != prop1 && prop1.PrototypeName == "ProtoScript.Interpretter.RuntimeInfo.FunctionRuntimeInfo")
						continue;

					if (null != prop2 && prop2.PrototypeName == "ProtoScript.Interpretter.RuntimeInfo.FunctionRuntimeInfo")
						continue;

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

		static public Prototype ChangeType(Prototype prototype, Prototype protoNewType)
		{
			Prototype protoParent = prototype.GetBaseType();

			prototype.PrototypeID = protoNewType.PrototypeID;
			prototype.PrototypeName = protoNewType.PrototypeName;

			TypeOfs.Merge(prototype, protoNewType);
			TypeOfs.Remove(prototype, protoParent);

			return prototype;
		}

		static public Prototype Bind(Prototype prototype1, Prototype prototype2, Prototype protoNew)
		{
			//Allow the object to be constructed first so it can initialize any properties
			Prototype protoNewInstance = protoNew.IsInstance() ? protoNew : NewInstance(protoNew);

			foreach (var pair in prototype1.NormalProperties)
			{
				//Don't overwrite any initialized properties 
				if (!protoNewInstance.Properties.ContainsKey(pair.Key))
					protoNewInstance.Properties[pair.Key] = pair.Value;
			}

			foreach (var pair in prototype2.NormalProperties)
			{
				//Don't overwrite any initialized properties 
				if (!protoNewInstance.Properties.ContainsKey(pair.Key))
					protoNewInstance.Properties[pair.Key] = pair.Value;
			}

			TypeOfs.Merge(protoNewInstance, prototype1);
			TypeOfs.Remove(protoNewInstance, prototype1.GetBaseType());

			return protoNewInstance;
		}

		static public Prototype BindAll(Prototype collection, Prototype protoNew)
		{
			//Allow the object to be constructed first so it can initialize any properties
			Prototype protoNewInstance = protoNew.IsInstance() ? protoNew : NewInstance(protoNew);

			foreach (Prototype protoChild in collection.Children)
			{
				foreach (var pair in protoChild.NormalProperties)
				{
					//Don't overwrite any initialized properties 
					if (!protoNewInstance.Properties.ContainsKey(pair.Key))
						protoNewInstance.Properties[pair.Key] = pair.Value;
				}

				TypeOfs.Merge(protoNewInstance, protoChild);
				TypeOfs.Remove(protoNewInstance, protoChild.GetBaseType());
			}

			return protoNewInstance;
		}

		static public Prototype BindMultiToken(Prototype collection, Prototype protoNew)
		{
			//N20221214-02 - Only allow plurality in the last token and any properties 
			//in the first token
			//Allow the object to be constructed first so it can initialize any properties
			Prototype protoNewInstance = protoNew.IsInstance() ? protoNew : NewInstance(protoNew);

			bool bFirst = true;
	
			foreach (Prototype protoChild in collection.Children)
			{
				foreach (var pair in protoChild.NormalProperties)
				{
					if (!bFirst && pair.Key != TemporaryPrototypes.GetTemporaryPrototypeOrNull("Object.Field.Plurality").PrototypeID)
						return null; 

					protoNewInstance.Properties[pair.Key] = pair.Value;
				}
				bFirst = false;
			}

			return protoNewInstance;
		}


		static public Prototype MergeInto(Prototype protoTarget, Prototype protoSource)
		{
			protoTarget.InsertTypeOf(protoSource);
			TypeOfs.Merge(protoTarget, protoSource);

			foreach (var pair in protoSource.NormalProperties)
			{
				//Don't overwrite any initialized properties 
				if (!protoTarget.Properties.ContainsKey(pair.Key))
					protoTarget.Properties[pair.Key] = pair.Value;
			}

			return protoTarget;
		}

		static public Prototype GetBaseTypesRecursive(Prototype prototype)
		{
			return GetBaseTypesRecursiveCircular(prototype, new Map<int, Prototype>());
		}

		static private Prototype GetBaseTypesRecursiveCircular(Prototype prototype, Map<int, Prototype> mapPrototypes)
		{
			if (null == prototype)
				return null;

			int iHash = prototype.GetHashCode();
			Prototype copy = null;
			if (mapPrototypes.TryGetValue(iHash, out copy))
			{
				return copy;
			}

			copy = prototype.GetBaseType();
			mapPrototypes[iHash] = copy;

			foreach (var pair in prototype.NormalProperties)
			{
				copy.Properties[pair.Key] = GetBaseTypesRecursiveCircular(pair.Value, mapPrototypes);
			}

			foreach (Prototype child in prototype.Children)
			{
				copy.Children.Add(GetBaseTypesRecursiveCircular(child, mapPrototypes));
			}

			TypeOfs.Merge(copy, prototype);

			return copy;
		}


		static public List<Prototype> GetSubTypesOf(Prototype prototype, SymbolTable symbols)
		{
			List<Prototype> lstPrototypes = new List<Prototype>();

			foreach (var pair in symbols.GetGlobalScope().Symbols)
			{
				if (pair.Value is RuntimeInfo.PrototypeTypeInfo)
				{
					RuntimeInfo.PrototypeTypeInfo info = pair.Value as RuntimeInfo.PrototypeTypeInfo;
					if (Prototypes.TypeOf(info.Prototype, prototype))
						lstPrototypes.Add(info.Prototype);
				}
			}

			return lstPrototypes;
		}

		static public Prototype GetAsPrototype(object obj)
		{
			Prototype protoResult = null;
			
			if (obj is ValueRuntimeInfo)
			{
				obj = (obj as ValueRuntimeInfo).Value;
			}

			if (obj == null)
			{
				protoResult = null;
			}
			else if (obj is PrototypeTypeInfo)
			{
				protoResult = (obj as PrototypeTypeInfo).Prototype;
			}

			else if (obj is Prototype)
			{
				protoResult = obj as Prototype;
			}

			else if (obj is DotNetTypeInfo)
			{
				DotNetTypeInfo dotNetTypeInfo = obj as DotNetTypeInfo;
				Prototype prototype = Prototypes.GetPrototypeByPrototypeName(dotNetTypeInfo.Type.FullName);
				if (null == prototype)
					throw new NotImplementedException("No prototype mapping exists for .NET type: " + dotNetTypeInfo.Type.FullName);
				protoResult = prototype;
			}

			else if (obj is List<Prototype>)
			{
				protoResult = new Collection(obj as List<Prototype>);
			}



			return protoResult;
		}

		static public object GetAs(object oValue, System.Type type)
		{
			return ValueConversions.GetAs(oValue, type);
		}

		static public bool IsAssignableFrom(TypeInfo infoSource, TypeInfo infoTarget)
		{
			return ValueConversions.IsAssignableFrom(infoSource, infoTarget);
		}

    }
}
