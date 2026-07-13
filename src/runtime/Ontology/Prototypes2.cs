using BasicUtilities;
using BasicUtilities.Collections;
using Ontology.BaseTypes;

namespace Ontology
{
	public partial class Prototypes
	{
		static public bool AreShallowEqual(Prototype prototype1, Prototype prototype2)
		{
			if (null == prototype1 || null == prototype2)
			{
				return null == prototype1 && null == prototype2;
			}

			return prototype2.ShallowEqual(prototype1);
		}

		static public Prototype CloneCircular(Prototype prototype)
		{
			Map<int, Prototype> mapPrototypes = new Map<int, Prototype>();
			return CloneCircular(prototype, mapPrototypes);
		}

		static public Prototype CloneCircular(Prototype prototype, Map<int, Prototype> mapPrototypes)
		{
			int iHash = prototype.GetHashCode();
			Prototype copy = null;
			if (mapPrototypes.TryGetValue(iHash, out copy))
			{
				return copy;
			}

			copy = prototype.ShallowClone();

			mapPrototypes[iHash] = copy;

			copy.PrototypeID = prototype.PrototypeID;
			copy.PrototypeName = prototype.PrototypeName;
			copy.Value = prototype.Value;

			PrototypePropertiesCollection clone = new PrototypePropertiesCollection(copy);
			copy.Properties = clone;

			foreach (var pair in prototype.Properties)
			{
				if (pair.Value != null)
					clone[pair.Key] = CloneCircular(pair.Value, mapPrototypes);
				else
					clone[pair.Key] = null;
			}

			foreach (Prototype child in prototype.Children)
			{
				copy.Children.Add(CloneCircular(child, mapPrototypes));
			}

			return copy;
		}

		static public object ? FromPrototype(Prototype prototype)
		{
			return FromPrototypeByReflection(prototype, new Map<int, object>());
		}


		static private object? FromPrototypeByReflection(Prototype prototype, Map<int, object> m_mapPrototypeToObjects)
		{
			if (prototype == null)
				return null;

			object existing;
			if (prototype.IsInstance() && m_mapPrototypeToObjects.TryGetValue(prototype.PrototypeID, out existing))
				return existing;

			// Recover the CLR type name from "Namespace.Type[...]" or "Namespace.Type"
			string strTypeName = prototype.PrototypeName;
			int idx = strTypeName.IndexOf('[');
			if (idx >= 0)
				strTypeName = strTypeName.Substring(0, idx);

			System.Type type = ResolveTypeByFullName(strTypeName);
			if (type == null)
				throw new Exception("Could not resolve type from prototype name: " + strTypeName);

			// Mirror your ToPrototype policy: block System/Microsoft non-atoms
			string ns = type.Namespace;
			if (ns != null && (ns.StartsWith("System", StringComparison.Ordinal) || ns.StartsWith("Microsoft", StringComparison.Ordinal)))
			{
				// Atoms were already handled by FromPrototype(...) before calling reflection.
				return null;
			}

			object? obj;
			if (type == typeof(string))
				obj = string.Empty;
			else
				obj = Activator.CreateInstance(type);

			// circular break
			m_mapPrototypeToObjects[prototype.PrototypeID] = obj;

			// Populate members based on the property-key prototypes you generated:
			//   "{TypeFullName}.Property.{Name}" and "{TypeFullName}.Field.{Name}"
			foreach (var pair in prototype.Properties)
			{
				int iKey = pair.Key;
				Prototype protoKey = Prototypes.GetPrototype(iKey);
				string strKeyName = protoKey.PrototypeName;

				// Only accept keys that are in this type's namespace (or nested types that used the same base)
				// You can relax this if you intentionally allow cross-type keys.
				if (!strKeyName.StartsWith(strTypeName + ".", StringComparison.Ordinal))
					continue;

				Prototype protoValue = pair.Value;

				string prefixProp = strTypeName + ".Property.";
				string prefixField = strTypeName + ".Field.";

				if (strKeyName.StartsWith(prefixProp, StringComparison.Ordinal))
				{
					string propName = strKeyName.Substring(prefixProp.Length);
					System.Reflection.PropertyInfo? pi = obj.GetType().GetProperty(propName);
					System.Type? expectedType = pi?.PropertyType;
					object? oValue = FromPrototypeCircular(protoValue, m_mapPrototypeToObjects, expectedType);
					ReflectionUtil.SetPropertyOrIgnore(obj, oValue, propName);
				}
				else if (strKeyName.StartsWith(prefixField, StringComparison.Ordinal))
				{
					string fieldName = strKeyName.Substring(prefixField.Length);
					System.Reflection.FieldInfo? fi = obj.GetType().GetField(fieldName);
					System.Type? expectedType = fi?.FieldType;
					object? oValue = FromPrototypeCircular(protoValue, m_mapPrototypeToObjects, expectedType);
					ReflectionUtil.SetFieldOrIgnore(obj, oValue, fieldName);
				}
			}

			// If the object is a list and the prototype has children, populate the list.
			if (obj is System.Collections.IList list && prototype.Children.Count > 0)
			{
				System.Type? elemType = GetElementTypeFromExpected(obj.GetType());
				foreach (Prototype child in prototype.Children)
				{
					object? oChild = FromPrototypeCircular(child, m_mapPrototypeToObjects, elemType);
					if (oChild != null)
						list.Add(Coerce(oChild, elemType));
				}
			}

			return obj;
		}

		static private object? FromPrototypeCircular(Prototype prototype, Map<int, object> m_mapPrototypeToObjects, System.Type? expectedType)
		{
			if (prototype == null || prototype.ShallowEqual(Compare.Entity.Prototype))
				return null;

			// circular break by prototype id (graph identity)
			//N20260114-01 - Don't trigger this cache for non-instances
			object existing;
			if (prototype.IsInstance() && m_mapPrototypeToObjects.TryGetValue(prototype.PrototypeID, out existing))
				return existing;

			if (prototype is NativeValuePrototype nvp)
			{
				if (nvp.NativeValue is string)
				{
					return nvp.NativeValue;
				}
				if (nvp.NativeValue is int)
				{
					return nvp.NativeValue;
				}
				if (nvp.NativeValue is bool)
				{
					return nvp.NativeValue;
				}
				if (nvp.NativeValue is double)
				{
					return nvp.NativeValue;
				}
			}

			// Base-type instances encoded via PrototypeName "System.String[...]" etc.
			// Prefer your existing native-value path when possible.
			if (prototype.IsInstance())
			{
				if (prototype.TypeOf(System_String.Prototype))
					return StringUtil.Between(prototype.PrototypeName, "[", "]");

				if (prototype.TypeOf(System_Int32.Prototype))
					return Convert.ToInt32(StringUtil.Between(prototype.PrototypeName, "[", "]"));

				if (prototype.TypeOf(System_Boolean.Prototype))
					return Convert.ToBoolean(StringUtil.Between(prototype.PrototypeName, "[", "]"));

				if (prototype.TypeOf(System_Double.Prototype))
					return Convert.ToDouble(StringUtil.Between(prototype.PrototypeName, "[", "]"));
			}


			// Collections
			if (prototype.TypeOf(Ontology.Collection.Prototype))
			{
				return MaterializeCollection(prototype, m_mapPrototypeToObjects, expectedType);
			}

			return FromPrototypeByReflection(prototype, m_mapPrototypeToObjects);
		}

		static private object MaterializeCollection(Prototype prototype, Map<int, object> m_mapPrototypeToObjects, System.Type? expectedType)
		{
			System.Type? elemType = GetElementTypeFromExpected(expectedType);

			if (expectedType != null && expectedType.IsArray)
			{
				int n = prototype.Children.Count;
				Array arr = Array.CreateInstance(elemType ?? typeof(object), n);
				m_mapPrototypeToObjects[prototype.PrototypeID] = arr;

				for (int i = 0; i < n; i++)
				{
					object? oChild = FromPrototypeCircular(prototype.Children[i], m_mapPrototypeToObjects, elemType);
					arr.SetValue(Coerce(oChild, elemType), i);
				}

				return arr;
			}

			if (expectedType != null && expectedType.IsClass && !expectedType.IsAbstract)
			{
				object inst;
				try
				{
					inst = Activator.CreateInstance(expectedType);
				}
				catch
				{
					inst = CreateDefaultList(elemType);
				}

				m_mapPrototypeToObjects[prototype.PrototypeID] = inst;

				if (inst is System.Collections.IList ilist)
				{
					foreach (Prototype child in prototype.Children)
					{
						object? oChild = FromPrototypeCircular(child, m_mapPrototypeToObjects, elemType);
						if (oChild != null)
							ilist.Add(Coerce(oChild, elemType));
					}
					return inst;
				}

				System.Reflection.MethodInfo? add = FindAddMethod(inst.GetType(), elemType);
				if (add != null)
				{
					foreach (Prototype child in prototype.Children)
					{
						object? oChild = FromPrototypeCircular(child, m_mapPrototypeToObjects, elemType);
						if (oChild != null)
							add.Invoke(inst, new object[] { Coerce(oChild, elemType) });
					}
					return inst;
				}

				return inst;
			}

			object listObj = CreateDefaultList(elemType);
			m_mapPrototypeToObjects[prototype.PrototypeID] = listObj;

			System.Collections.IList list = (System.Collections.IList)listObj;

			foreach (Prototype child in prototype.Children)
			{
				object? oChild = FromPrototypeCircular(child, m_mapPrototypeToObjects, elemType);
				if (oChild != null)
					list.Add(Coerce(oChild, elemType));
			}

			return listObj;
		}

		static private System.Type? GetElementTypeFromExpected(System.Type? expectedType)
		{
			if (expectedType == null)
				return null;

			if (expectedType.IsArray)
				return expectedType.GetElementType();

			if (expectedType.IsGenericType)
			{
				System.Type[] args = expectedType.GetGenericArguments();
				if (args.Length == 1)
					return args[0];
			}

			return null;
		}

		static private object CreateDefaultList(System.Type? elemType)
		{
			System.Type tElem = elemType ?? typeof(object);
			System.Type tList = typeof(List<>).MakeGenericType(tElem);
			return Activator.CreateInstance(tList);
		}

		static private System.Reflection.MethodInfo? FindAddMethod(System.Type t, System.Type? elemType)
		{
			if (elemType == null)
				return t.GetMethod("Add", new System.Type[] { typeof(object) });

			return t.GetMethod("Add", new System.Type[] { elemType });
		}

		static private object? Coerce(object? value, System.Type? expectedType)
		{
			if (value == null || expectedType == null)
				return value;

			if (expectedType.IsInstanceOfType(value))
				return value;

			try
			{
				return Convert.ChangeType(value, expectedType);
			}
			catch
			{
				return value;
			}
		}

		static private System.Type ResolveTypeByFullName(string fullName)
		{
			// Fast path: works if assembly-qualified or in mscorlib/System.Private.CoreLib for some.
			System.Type t = System.Type.GetType(fullName, throwOnError: false);
			if (t != null)
				return t;

			// Search loaded assemblies (this is what you need for domain types like CSharp.File, etc.)
			foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					t = asm.GetType(fullName, throwOnError: false, ignoreCase: false);
					if (t != null)
						return t;
				}
				catch
				{
					// ignore reflection load issues
				}
			}

			return null;
		}


		public static Prototype GetPrototype(int PrototypeID)
		{
			return TemporaryPrototypes.GetTemporaryPrototype(PrototypeID);
		}

		public static string GetPrototypeName(int PrototypeID)
		{
			return TemporaryPrototypes.GetTemporaryPrototype(PrototypeID).PrototypeName;
		}

		public static Prototype GetPrototypeByPrototypeName(string PrototypeName)
		{
			return TemporaryPrototypes.GetTemporaryPrototype(PrototypeName);
		}
		static public Prototype GetOrInsertPrototype(string PrototypeName, string PrototypeParentName)
		{
			Prototype protoParent = GetOrInsertPrototype(PrototypeParentName);
			return TemporaryPrototypes.GetOrCreateTemporaryPrototype(PrototypeName, protoParent);
		}

		static public Prototype GetOrInsertPrototype(string PrototypeName)
		{
			return TemporaryPrototypes.GetOrCreateTemporaryPrototype(PrototypeName);
		}

		static public bool TypeOf(int iPrototypeID, Prototype parent)
		{
			Prototype prototype = Prototypes.GetPrototype(iPrototypeID);

			return TypeOf(prototype, parent);
		}

		static public bool TypeOf(Prototype prototype, Prototype parent)
		{
			if (null == prototype || null == parent)
				return false;

			if (prototype.TypeOf(parent))
				return true;

			return (AreShallowEqual(prototype, parent));
		}

		
	}
}    
		

