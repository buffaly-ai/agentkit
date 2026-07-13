
using BasicUtilities;
using BasicUtilities.Collections;
using Ontology.BaseTypes;
using System;
using System.Runtime.CompilerServices;

namespace Ontology
{
	public class NativeValuePrototype : Prototype
	{
		public Object NativeValue;
		private bool m_bObjectInstance = false;

		private NativeValuePrototype()
		{
		}



		//public NativeValuePrototype(Prototype prototype, Object obj) 
		//{			
		//	this.NativeValue = obj;
		//	this.PrototypeName = prototype.PrototypeName + "[" + obj.GetHashCode() + "]";
		//	this.PrototypeID = TemporaryPrototypes.GetOrCreateTemporaryPrototype(PrototypeName, prototype).PrototypeID;
		//	this.InsertTypeOf(prototype);
		//}

		//This method is for wrapping a prototype in a NativeValuePrototype (so it can be returned as one) but doesn't create a NativeValue
		//public NativeValuePrototype(Prototype prototype)
		//{
		//	this.PrototypeID = prototype.PrototypeID;
		//	this.PrototypeName = prototype.PrototypeName;
		//	this.Properties = prototype.Properties;
		//	this.Children = prototype.Children;
		//	this.Value = prototype.Value;

		//}


		static public NativeValuePrototype GetOrCreateNativeValuePrototype(string strValue)
		{
			string strPrototypeName = System_String.Prototype.PrototypeName + "[" + strValue + "]";
			Prototype? prototype = TemporaryPrototypes.GetTemporaryPrototypeOrNull(strPrototypeName);
			if (null == prototype)
			{
				NativeValuePrototype nv = new NativeValuePrototype();
				nv.NativeValue = strValue;
				nv.PrototypeName = strPrototypeName;
				nv.PrototypeID = TemporaryPrototypes.GetOrInsertPrototype(nv).PrototypeID;
				nv.InsertTypeOf(System_String.Prototype);
				return nv;
			}

			if (prototype is not NativeValuePrototype nv2)
			{
				throw new Exception("Prototype with name '" + strPrototypeName + "' is not a NativeValuePrototype, but " + prototype.GetType().Name);
			}

			return nv2;
		}

		static public NativeValuePrototype GetOrCreateNativeValuePrototype(int iValue)
		{
			string strPrototypeName = System_Int32.Prototype.PrototypeName + "[" + iValue + "]";
			Prototype? prototype = TemporaryPrototypes.GetTemporaryPrototypeOrNull(strPrototypeName);
			if (null == prototype)
			{
				NativeValuePrototype nv = new NativeValuePrototype();
				nv.NativeValue = iValue;
				nv.PrototypeName = strPrototypeName;
				nv.PrototypeID = TemporaryPrototypes.GetOrInsertPrototype(nv).PrototypeID;
				nv.InsertTypeOf(System_Int32.Prototype);

				return nv;
			}
			if (prototype is not NativeValuePrototype nv2)
			{
				throw new Exception("Prototype with name '" + strPrototypeName + "' is not a NativeValuePrototype, but " + prototype.GetType().Name);
			}
			return nv2;
		}

		static public NativeValuePrototype GetOrCreateNativeValuePrototype(bool bValue)
		{
			string strPrototypeName = System_Boolean.Prototype.PrototypeName + "[" + bValue + "]";
			Prototype? prototype = TemporaryPrototypes.GetTemporaryPrototypeOrNull(strPrototypeName);
			if (null == prototype)
			{
				NativeValuePrototype nv = new NativeValuePrototype();
				nv.NativeValue = bValue;
				nv.PrototypeName = strPrototypeName;
				nv.PrototypeID = TemporaryPrototypes.GetOrInsertPrototype(nv).PrototypeID;
				nv.InsertTypeOf(System_Boolean.Prototype);
				return nv;
			}
			if (prototype is not NativeValuePrototype nv2)
			{
				throw new Exception("Prototype with name '" + strPrototypeName + "' is not a NativeValuePrototype, but " + prototype.GetType().Name);
			}
			return nv2;
		}

		static public NativeValuePrototype GetOrCreateNativeValuePrototype(double dValue)
		{
			string strPrototypeName = System_Double.Prototype.PrototypeName + "[" + dValue + "]";
			Prototype? prototype = TemporaryPrototypes.GetTemporaryPrototypeOrNull(strPrototypeName);
			if (null == prototype)
			{
				NativeValuePrototype nv = new NativeValuePrototype();
				nv.NativeValue = dValue;
				nv.PrototypeName = strPrototypeName;
				nv.PrototypeID = TemporaryPrototypes.GetOrInsertPrototype(nv).PrototypeID;
				nv.InsertTypeOf(System_Double.Prototype);
				return nv;
			}
			if (prototype is not NativeValuePrototype nv2)
			{
				throw new Exception("Prototype with name '" + strPrototypeName + "' is not a NativeValuePrototype, but " + prototype.GetType().Name);
			}
			return nv2;
		}




		//static public NativeValuePrototype GetOrCreateNativeObjectPrototype(object obj, Prototype protoType)
		//{
		//	// We follow the “instance name” convention you used historically: TypeName[hash]
		//	string strPrototypeName = protoType.PrototypeName + "[" + obj.GetHashCode() + "]";

		//	Prototype? existing = TemporaryPrototypes.GetTemporaryPrototypeOrNull(strPrototypeName);
		//	if (existing != null)
		//	{
		//		if (existing is not NativeValuePrototype nvExisting)
		//			throw new Exception("Prototype with name '" + strPrototypeName + "' is not a NativeValuePrototype, but " + existing.GetType().Name);

		//		// Keep the native attached (best-effort; native instance identity may vary across calls)
		//		nvExisting.NativeValue = obj;
		//		return nvExisting;
		//	}

		//	NativeValuePrototype nv = new NativeValuePrototype();
		//	nv.NativeValue = obj;
		//	nv.PrototypeName = strPrototypeName;
		//	nv.PrototypeID = TemporaryPrototypes.GetOrInsertPrototype(nv).PrototypeID;

		//	// Type the instance
		//	nv.InsertTypeOf(protoType);

		//	// Mark as object-instance so ShallowEquivalent uses base-type comparison rules
		//	// (matches the intent of m_bObjectInstance behavior already in this file)
		//	nv.m_bObjectInstance = true;

		//	return nv;
		//}

		internal static Prototype GetOrCreateTypePrototype(System.Type type)
		{
			return ExtractTypeHierarchy(type);
		}

		internal static NativeValuePrototype GetOrCreateNativeObjectPrototype(object obj, Prototype protoType, string strInstanceName = null)
		{
			string strPrototypeName;
			if (strInstanceName != null)
				strPrototypeName = protoType.PrototypeName + "[" + strInstanceName + "]";
			else
				strPrototypeName = protoType.PrototypeName + "[" + RuntimeHelpers.GetHashCode(obj) + "]";

			Prototype? prototype = TemporaryPrototypes.GetTemporaryPrototypeOrNull(strPrototypeName);
			if (null == prototype)
			{
				NativeValuePrototype nv = new NativeValuePrototype();
				nv.NativeValue = obj;
				nv.PrototypeName = strPrototypeName;
				nv.PrototypeID = TemporaryPrototypes.GetOrInsertPrototype(nv).PrototypeID;
				nv.InsertTypeOf(protoType);
				nv.m_bObjectInstance = true;
				nv.m_bIsInstance = true;
				return nv;
			}

			if (prototype is not NativeValuePrototype nv2)
				throw new Exception("Prototype with name '" + strPrototypeName + "' is not a NativeValuePrototype, but " + prototype.GetType().Name);

			nv2.NativeValue = obj;
			return nv2;
		}

		internal static NativeValuePrototype GetOrCreateNativeEnumerablePrototype(System.Collections.IEnumerable obj)
		{
			// Collections do not need per-instance names; children define identity.
			string strPrototypeName = Ontology.Collection.Prototype.PrototypeName + "[" + RuntimeHelpers.GetHashCode(obj) + "]";

			Prototype? prototype = TemporaryPrototypes.GetTemporaryPrototypeOrNull(strPrototypeName);
			if (null == prototype)
			{
				NativeValuePrototype nv = new NativeValuePrototype();
				nv.NativeValue = obj;
				nv.PrototypeName = strPrototypeName;
				nv.PrototypeID = TemporaryPrototypes.GetOrInsertPrototype(nv).PrototypeID;
				nv.InsertTypeOf(Ontology.Collection.Prototype);
				nv.m_bObjectInstance = true;
				nv.m_bIsInstance = true;
				return nv;
			}

			if (prototype is not NativeValuePrototype nv2)
				throw new Exception("Prototype with name '" + strPrototypeName + "' is not a NativeValuePrototype, but " + prototype.GetType().Name);

			nv2.NativeValue = obj;
			return nv2;
		}



		private static Prototype ExtractTypeHierarchy(System.Type type)
		{
			string strTypeName = type.FullName ?? throw new Exception("Cannot get type name");
			Prototype? prototype = TemporaryPrototypes.GetTemporaryPrototypeOrNull(strTypeName);
			if (null == prototype)
			{
				prototype = TemporaryPrototypes.GetOrCreateTemporaryPrototype(strTypeName);
				if (type != typeof(object))
				{
					Prototype protoBase = ExtractTypeHierarchy(type.BaseType!);
					prototype.InsertTypeOf(protoBase.PrototypeID);
				}
			}
			return prototype;
		}

		public override bool ShallowEquivalent(Prototype rhs)
		{
			//Rule: primitive types compare like AreEqual (System.Int[1] does not equal System.Int[2])
			//Object types need to ignore the instance and descend
			if (this.m_bObjectInstance)
			{
				Prototype lhs = this.IsInstance() ? this.GetBaseType() : this;
				rhs = rhs.IsInstance() ? rhs.GetBaseType() : rhs;

				return lhs.ShallowEqual(rhs);
			}

			return this.ShallowEqual(rhs);
		}

		public override Prototype Clone()
		{
			if (m_bObjectInstance)			
				return base.Clone();

			return this.ShallowClone();
		}

		public override Prototype ShallowClone()
		{
			NativeValuePrototype nv = new NativeValuePrototype();

			PopulateClone(nv);
			nv.NativeValue = this.NativeValue;
			nv.m_bObjectInstance = m_bObjectInstance;

			return nv;
		}

		public override JsonObject ToJsonObject(bool bRemoveNulls = false)
		{
			JsonObject jsonPrototype = new JsonObject();
			jsonPrototype[nameof(PrototypeName)] = this.PrototypeName;

			//N20210109-03 - NativeValue can be cast back to base type
			if (null != this.NativeValue)
			{
				if (this.PrototypeID == System_Int32.PrototypeID)
				{
					jsonPrototype[nameof(NativeValue)] = (int)this.NativeValue;
				}
				else if (this.PrototypeID == System_Boolean.PrototypeID)
				{
					jsonPrototype[nameof(NativeValue)] = (bool)this.NativeValue;
				}
				else if (this.PrototypeID == System_Double.PrototypeID)
				{
					jsonPrototype[nameof(NativeValue)] = (double)this.NativeValue;
				}
				else if (this.PrototypeID == System_String.PrototypeID)
				{
					jsonPrototype[nameof(NativeValue)] = (string)this.NativeValue;
				}
			}

			foreach (var pair in this.Properties)
			{
				if (!bRemoveNulls || pair.Value != null)
					jsonPrototype[pair.Key.ToString()] = pair.Value?.ToJsonObject();
			}

			if (this.Children.Count > 0)
			{
				JsonArray jsonChildren = new JsonArray();

				foreach (Prototype child in this.Children)
				{
					jsonChildren.Add(child?.ToJsonObject());
				}

				jsonPrototype[nameof(Children)] = jsonChildren;
			}

			return jsonPrototype;
		}

		public override string ToJSON(bool bRemoveNulls = false)
		{
			return ToJsonObject(bRemoveNulls).ToJSON();
		}


		public override JsonObject ToFriendlyJsonObject()
		{
			JsonObject jsonPrototype = new JsonObject();
			jsonPrototype[nameof(PrototypeName)] = this.PrototypeName;

			//N20210109-03 - NativeValue can be cast back to base type
			if (null != this.NativeValue)
			{
				if (this.NativeValue is int)
				{
					jsonPrototype[nameof(NativeValue)] = (int)this.NativeValue;
				}
				else if (this.NativeValue is bool)
				{
					jsonPrototype[nameof(NativeValue)] = (bool)this.NativeValue;
				}
				else if (this.NativeValue is double)
				{
					jsonPrototype[nameof(NativeValue)] = (double)this.NativeValue;
				}
				else if (this.NativeValue is string)
				{
					jsonPrototype[nameof(NativeValue)] = (string)this.NativeValue;
				}
			}

			foreach (var pair in this.Properties)
			{
				if (pair.Value != null)
					jsonPrototype[Prototypes.GetPrototypeName(pair.Key)] = pair.Value?.ToJsonObject();
			}

			if (this.Children.Count > 0)
			{
				JsonArray jsonChildren = new JsonArray();

				foreach (Prototype child in this.Children)
				{
					jsonChildren.Add(child == null ? null : child.ToJsonObject());
				}

				jsonPrototype[nameof(Children)] = jsonChildren;
			}

			return jsonPrototype;
		}

		//Needed for inheritance
		public override JsonObject ToFriendlyJsonObjectCircular(Set<int> setHashes)
		{
			return ToFriendlyJsonObject();
		}


		public override IEnumerable<int> GetParents()
		{
			//Parent includes the base type here
			if (null != NativeValue)
				yield return PrototypeID;

			foreach (int protoParent in base.GetParents())
				yield return protoParent;
		}

		new public static Prototype ? FromJSON(string strJSON)
		{
			return FromJsonValue(new JsonValue(strJSON));
		}

		public static Prototype ? FromJSON(string strJSON, bool bConvertErrorToNull)
		{
			return FromJsonValue(new JsonValue(strJSON), bConvertErrorToNull);
		}

		new public static Prototype ? FromJsonValue(JsonValue jsonValue)
		{
			return FromJsonValue(jsonValue, false);
		}

		public static Prototype ? FromJsonValue(JsonValue jsonValue, bool bConvertErrorToNull)
		{
			Prototype ? prototype = null;

			try
			{

				if (jsonValue.ToJsonObject() != null)
				{
					JsonObject jsonPrototype = jsonValue.ToJsonObject()!;

					if (jsonPrototype.ContainsKey(nameof(NativeValuePrototype.NativeValue)))
					{
						if (!jsonPrototype.ContainsKey(nameof(Prototype.PrototypeName)))
							throw new Exception("JsonObject does not contain PrototypeName - required for NativeValuePrototype");

						JsonValue jsonNV = jsonPrototype[nameof(NativeValuePrototype.NativeValue)]!;
						string strPrototypeName = jsonPrototype[nameof(Prototype.PrototypeName)]!.ToString();

						if (strPrototypeName.StartsWith(System_Boolean.PrototypeName))
							prototype = NativeValuePrototype.GetOrCreateNativeValuePrototype(jsonNV.ToBoolean());
						else if (strPrototypeName.StartsWith(System_Double.PrototypeName))
							prototype = NativeValuePrototype.GetOrCreateNativeValuePrototype(jsonNV.ToDouble());
						else if (strPrototypeName.StartsWith(System_Int32.PrototypeName))
							prototype = NativeValuePrototype.GetOrCreateNativeValuePrototype(jsonNV.ToInteger());
						else if (strPrototypeName.StartsWith(System_String.PrototypeName))
							prototype = NativeValuePrototype.GetOrCreateNativeValuePrototype(jsonNV.ToString());
						else
							throw new Exception("Converting to NV not supported for this type: " + strPrototypeName);

						//There may be augmented properties under the root
						foreach (string strKey in jsonPrototype.Keys)
						{
							if (strKey == nameof(Prototype.PrototypeID) || strKey == nameof(Prototype.PrototypeName) || strKey == nameof(NativeValuePrototype.NativeValue))
								continue;

							if (strKey == nameof(Prototype.NormalProperties)
							//TODO: Currently the JSON serializer is not called on every property
							//so a prototype nested in another object won't have the right format
							|| strKey == nameof(Prototype.Properties) || strKey == nameof(Prototype.Value)
							)
								continue;

							if (strKey == nameof(Prototype.Children))
							{
								Prototype propValue = FromJsonValue(jsonPrototype[strKey], bConvertErrorToNull);
								prototype.Children = propValue.Children;
							}

							else
							{
								Prototype propName = FromJsonValue(strKey, bConvertErrorToNull);

								if (null != propName)
								{
									Prototype propValue = FromJsonValue(jsonPrototype[strKey], bConvertErrorToNull);

									prototype.Properties[propName.PrototypeID] = propValue;
								}
							}
						}
					}

					else
					{
						if (jsonPrototype.ContainsKey(nameof(Prototype.PrototypeID)))
							prototype = Prototypes.GetPrototype(jsonPrototype[nameof(prototype.PrototypeID)].ToInt());

						else if (jsonPrototype.ContainsKey(nameof(Prototype.PrototypeName)))
							prototype = Prototypes.GetPrototypeByPrototypeName(jsonPrototype[nameof(Prototype.PrototypeName)].ToString());

						else
							throw new Exception("JsonObject does not contain PrototypeID or PrototypeName");

						if (prototype.PrototypeID < 0)
							return Prototype.FromJsonValue(jsonPrototype);


						foreach (string strKey in jsonPrototype.Keys)
						{
							if (strKey == nameof(Prototype.PrototypeID) || strKey == nameof(Prototype.PrototypeName) || strKey == nameof(NativeValuePrototype.NativeValue))
								continue;

							if (strKey == nameof(Prototype.Children))
							{
								Prototype propValue = FromJsonValue(jsonPrototype[strKey], bConvertErrorToNull);
								prototype.Children = propValue.Children;
							}

							else
							{
								Prototype propName = FromJsonValue(strKey, bConvertErrorToNull);

								if (null != propName)
								{
									Prototype propValue = FromJsonValue(jsonPrototype[strKey], bConvertErrorToNull);

									prototype.Properties[propName.PrototypeID] = propValue;
								}
							}
						}
					}
				}

				else if (jsonValue.ToJsonArray() != null)
				{
					prototype = Ontology.Collection.Prototype.Clone();
					foreach (JsonValue element in jsonValue.ToJsonArray())
					{
						prototype.Children.Add(FromJsonValue(element, bConvertErrorToNull));
					}
				}

				else if (StringUtil.IsInteger(jsonValue.ToString()))
				{
					prototype = Prototypes.GetPrototype(jsonValue.ToInteger());
				}

				else if (jsonValue.ToString() == "null")
				{
					prototype = null;
				}
				else
				{
					prototype = Prototypes.GetPrototypeByPrototypeName(jsonValue.ToString());
				}
			}
			catch (Exception)
			{
				if (bConvertErrorToNull)
					prototype = null;

				else
					throw;
			}

			return prototype;
		}
	}

	public class NativeValuePrototypes : Prototypes
	{
		internal enum SerializationAction
		{
			Recurse = 0,
			Atom = 1,
			Skip = 2,
		}

		internal static SerializationAction Classify(object obj)
		{
			if (obj == null)
				return SerializationAction.Skip;

			if (obj is string || obj is bool || obj is int || obj is double || obj is Enum)
				return SerializationAction.Atom;

			if (obj is long || obj is short || obj is byte || obj is uint || obj is ulong || obj is ushort || obj is sbyte)
				return SerializationAction.Atom;

			if (obj is float || obj is decimal)
				return SerializationAction.Atom;

			if (obj is System.DateTime || obj is System.DateTimeOffset || obj is System.TimeSpan)
				return SerializationAction.Atom;

			if (obj is System.Guid)
				return SerializationAction.Atom;

			if (obj is System.Collections.IEnumerable && obj is not string)
				return SerializationAction.Recurse;

			System.Type t = obj.GetType();
			string ? ns = t.Namespace;

			if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.KeyValuePair<,>))
				return SerializationAction.Recurse;

			if (ns != null && (ns.StartsWith("System", StringComparison.Ordinal) || ns.StartsWith("Microsoft", StringComparison.Ordinal)))
				return SerializationAction.Skip;

			//N20260108-01 - Replace with better algorithm later
			if (t.Name.Contains("StatementParsingInfo"))
				return SerializationAction.Skip;

			return SerializationAction.Recurse;
		}

		static public NativeValuePrototype? ToPrototype(object obj)
		{
			return ToPrototypeCircular(obj, new Map<int, NativeValuePrototype>());
		}

		static private NativeValuePrototype? ToPrototypeCircular(object obj, Map<int, NativeValuePrototype> m_mapObjectIdToPrototype)
		{
			if (obj == null)
				return null;

			if (obj is NativeValuePrototype)
				return obj as NativeValuePrototype;

			SerializationAction action = Classify(obj);

			if (action == SerializationAction.Skip)
				return null;

			if (action == SerializationAction.Atom)
			{
				if (obj is string)
					return (NativeValuePrototype)NativeValuePrototype.GetOrCreateNativeValuePrototype((string)obj).ShallowClone();

				if (obj is bool)
					return (NativeValuePrototype)NativeValuePrototype.GetOrCreateNativeValuePrototype((bool)obj).ShallowClone();

				if (obj is int)
					return (NativeValuePrototype)NativeValuePrototype.GetOrCreateNativeValuePrototype((int)obj).ShallowClone();

				if (obj is Enum)
					return (NativeValuePrototype)NativeValuePrototype.GetOrCreateNativeValuePrototype((int)obj).ShallowClone();

				if (obj is double)
					return (NativeValuePrototype)NativeValuePrototype.GetOrCreateNativeValuePrototype((double)obj).ShallowClone();

				Prototype protoType = NativeValuePrototype.GetOrCreateTypePrototype(obj.GetType());
				return (NativeValuePrototype)NativeValuePrototype.GetOrCreateNativeObjectPrototype(obj, protoType).ShallowClone();
			}

			if (obj is System.Collections.IEnumerable && obj is not string)
				return ToPrototype((System.Collections.IEnumerable)obj, m_mapObjectIdToPrototype);

			if (obj is Prototype)
				throw new NotImplementedException();

			return ToPrototypeByReflection(obj, m_mapObjectIdToPrototype);
		}

		static private NativeValuePrototype ToPrototype(System.Collections.IEnumerable obj, Map<int, NativeValuePrototype> m_mapObjectIdToPrototype)
		{
			// Collections: build a Collection node; children are circular-safe conversions.
			NativeValuePrototype protoResult = NativeValuePrototype.GetOrCreateNativeEnumerablePrototype(obj);

			foreach (object el in obj)
			{
				if (el != null)
				{
					NativeValuePrototype ? protoChild = ToPrototypeCircular(el, m_mapObjectIdToPrototype);
					if (protoChild != null)
						protoResult.Children.Add(protoChild);
				}
			}

			return protoResult;
		}

		static private NativeValuePrototype ToPrototypeByReflection(object obj, Map<int, NativeValuePrototype> m_mapObjectIdToPrototype)
		{
			// Object identity key (not overridable GetHashCode())
			int iObjId = RuntimeHelpers.GetHashCode(obj);

			NativeValuePrototype? protoExisting;
			if (m_mapObjectIdToPrototype.TryGetValue(iObjId, out protoExisting))
			{
				return protoExisting;
			}

			System.Type t = obj.GetType();

			// 1) Create/get the temporary prototype representing the CLR type + base chain
			Prototype protoType = NativeValuePrototype.GetOrCreateTypePrototype(t);


			// 3) Create/get a per-instance wrapper (NativeValuePrototype) typed by protoType.
			// IMPORTANT: register in map BEFORE descending to break cycles.
			NativeValuePrototype result = NativeValuePrototype.GetOrCreateNativeObjectPrototype(obj, protoType);
			m_mapObjectIdToPrototype[iObjId] = result;

			// 4) Ensure we have stable “grouping” parents for keys
			Prototype protoPropsParent = TemporaryPrototypes.GetOrCreateTemporaryPrototype(t.FullName + ".Property");
			Prototype protoFieldsParent = TemporaryPrototypes.GetOrCreateTemporaryPrototype(t.FullName + ".Field");

			// 5) Public instance properties
			foreach (System.Reflection.PropertyInfo prop in t.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
			{
				if (!prop.CanRead)
					continue;

				if (prop.Name == "InstanceName")
					continue;

				if (prop.GetIndexParameters().Length != 0)
					continue;

				object propValue;
				try
				{
					propValue = prop.GetValue(obj, null);
				}
				catch
				{
					continue;
				}

				if (propValue == null)
					continue;

				Prototype protoPropKey = TemporaryPrototypes.GetOrCreateTemporaryPrototype(t.FullName + ".Property." + prop.Name, protoPropsParent);

				NativeValuePrototype v = ToPrototypeCircular(propValue, m_mapObjectIdToPrototype);
				if (v == null)
					continue;

				result.Properties[protoPropKey.PrototypeID] = v;
			}

			// 6) Public instance fields
			foreach (System.Reflection.FieldInfo field in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
			{
				if (field.IsStatic)
					continue;

				object fieldValue;
				try
				{
					fieldValue = field.GetValue(obj);
				}
				catch
				{
					continue;
				}

				if (fieldValue == null)
					continue;

				Prototype protoFieldKey = TemporaryPrototypes.GetOrCreateTemporaryPrototype(t.FullName + ".Field." + field.Name, protoFieldsParent);

				NativeValuePrototype? protoValue = ToPrototypeCircular(fieldValue, m_mapObjectIdToPrototype);
				if (protoValue == null)
					continue;

				Prototype? protoExisting2 = result.Properties[protoFieldKey.PrototypeID];
				if (protoExisting2 != null && protoExisting2.TypeOf(Ontology.Collection.Prototype))
				{
					protoExisting2.Children.Clear();
					if (null != protoValue)
						protoExisting2.Children.AddRange(protoValue.Children);
				}
				else
				{
					result.Properties[protoFieldKey.PrototypeID] = protoValue;
				}
			}

			return result;
		}






		static public object ? FromPrototype(NativeValuePrototype prototype)
		{

			if (prototype.NativeValue is string)
			{
				return prototype.NativeValue;
			}
			if (prototype.NativeValue is int)
			{
				return prototype.NativeValue;
			}
			if (prototype.NativeValue is bool)
			{
				return prototype.NativeValue;
			}
			if (prototype.NativeValue is double)
			{
				return prototype.NativeValue;
			}

			return Prototypes.FromPrototype(prototype);
		}


		static public bool IsBaseType(string strPrototypeName)
		{
			switch (strPrototypeName)
			{
				case System_String.PrototypeName:
				case System_Int32.PrototypeName:
				case System_Double.PrototypeName:
				case System_Boolean.PrototypeName:
					return true;
				default:
					return false;
			}
		}

		static public bool IsBaseType(int iPrototypeID)
		{
			if (iPrototypeID == System_String.PrototypeID)
				return true;

			if (iPrototypeID == System_Int32.PrototypeID)
				return true;

			if (iPrototypeID == System_Double.PrototypeID)
				return true;

			if (iPrototypeID == System_Boolean.PrototypeID)
				return true;

			return false;
		}



	}
}
