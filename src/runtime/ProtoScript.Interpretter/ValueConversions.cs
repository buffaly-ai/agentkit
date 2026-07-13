using Ontology;
using Ontology.BaseTypes;
using Ontology.Simulation;
using ProtoScript.Interpretter.RuntimeInfo;

namespace ProtoScript.Interpretter
{
	/// <summary>
	/// Centralized conversion helpers for ProtoScript runtime values.
	/// 
	/// This class is the single source of truth for converting between:
	/// - CLR primitives (`string`, `int`, `bool`, `double`)
	/// - ProtoScript wrapper objects (`StringWrapper`, `IntWrapper`, `BoolWrapper`, `DoubleWrapper`)
	/// - Native prototype-backed values (`NativeValuePrototype`)
	/// - Runtime metadata wrappers (`ValueRuntimeInfo`, `PrototypeTypeInfo`)
	/// - Prototype objects (`Prototype`)
	/// </summary>
	public static class ValueConversions
	{
		/// <summary>
		/// Attempts to convert a runtime value to the requested CLR type.
		/// Returns <c>null</c> when no safe conversion exists.
		/// </summary>
		public static object? GetAs(object? value, System.Type targetType)
		{
			if (targetType == null)
				throw new ArgumentNullException(nameof(targetType));

			TypeInfo? sourceTypeInfo;
			object? unwrapped = UnwrapRuntimeValue(value, out sourceTypeInfo);
			if (unwrapped == null)
				return null;

			if (targetType.IsAssignableFrom(unwrapped.GetType()))
				return unwrapped;

			if (unwrapped is StringReference stringReference)
			{
				if (targetType == typeof(string))
				{
					if (stringReference.TryResolveString(out string? strValue))
						return strValue;
				}
				else if (targetType == typeof(Prototype))
				{
					if (stringReference.TryResolvePrototype(out Prototype? prototypeValue))
						return prototypeValue;
				}
				else if (targetType == typeof(StringWrapper))
				{
					if (stringReference.TryResolvePrototype(out Prototype? wrapperPrototype))
						return new StringWrapper(wrapperPrototype!);
				}
			}

			if (unwrapped is Prototype prototype)
			{
				object? convertedPrototype = ConvertPrototypeToPrimitive(prototype, targetType);
				if (convertedPrototype != null)
					return convertedPrototype;
			}

			object? wrapperValue = UnwrapWrapperValue(unwrapped);
			if (wrapperValue != null && targetType.IsAssignableFrom(wrapperValue.GetType()))
				return wrapperValue;

			if (wrapperValue is System.IConvertible && typeof(System.IConvertible).IsAssignableFrom(targetType))
			{
				try
				{
					return System.Convert.ChangeType(wrapperValue, targetType, System.Globalization.CultureInfo.InvariantCulture);
				}
				catch
				{
				}
			}

			return null;
		}

		/// <summary>
		/// Converts primitives/wrappers/runtime values into a <see cref="Prototype"/> when possible.
		/// Returns <c>null</c> when conversion cannot be performed.
		/// </summary>
		public static Prototype? ToPrototype(object? value)
		{
			TypeInfo? sourceTypeInfo;
			object? unwrapped = UnwrapRuntimeValue(value, out sourceTypeInfo);
			if (unwrapped == null)
				return null;

			Prototype? asPrototype = SimpleInterpretter.GetAsPrototype(unwrapped);
			if (asPrototype != null)
				return asPrototype;

			if (unwrapped is StringReference stringReference)
			{
				if (stringReference.TryResolvePrototype(out Prototype? referencedPrototype))
					return referencedPrototype;
			}

			if (unwrapped is string str)
				return StringWrapper.ToPrototype(str);

			if (unwrapped is int i)
				return IntWrapper.ToPrototype(i);

			if (unwrapped is double d)
				return DoubleWrapper.ToPrototype(d);

			if (unwrapped is bool b)
				return BoolWrapper.ToPrototype(b);

			if (unwrapped != null)
			{
				try
				{
					return NativeValuePrototypes.ToPrototype(unwrapped);
				}
				catch
				{
				}
			}

			return null;
		}

		/// <summary>
		/// Attempts to coerce a value to be assignable to <paramref name="targetTypeInfo"/>.
		/// </summary>
		public static bool TryMakeAssignable(object? value, TypeInfo targetTypeInfo, out object? converted, TypeInfo? sourceTypeInfo = null)
		{
			converted = null;

			if (targetTypeInfo == null)
				return false;

			if (value == null)
			{
				converted = null;
				return true;
			}

			TypeInfo? discoveredSourceTypeInfo;
			object? unwrapped = UnwrapRuntimeValue(value, out discoveredSourceTypeInfo);
			TypeInfo? effectiveSourceTypeInfo = sourceTypeInfo ?? discoveredSourceTypeInfo;

			if (unwrapped == null)
			{
				converted = null;
				return true;
			}

			System.Type targetType = targetTypeInfo.Type;
			System.Type effectiveType = System.Nullable.GetUnderlyingType(targetType) ?? targetType;

			if (effectiveType.IsAssignableFrom(unwrapped.GetType()))
			{
				converted = unwrapped;
				return true;
			}

			object? convertedSpecial;
			if (TryConvertByKnownMappings(unwrapped, targetTypeInfo, effectiveType, out convertedSpecial))
			{
				converted = convertedSpecial;
				return true;
			}

			object? wrapperValue = UnwrapWrapperValue(unwrapped);
			if (!object.ReferenceEquals(wrapperValue, unwrapped))
			{
				if (wrapperValue != null && effectiveType.IsAssignableFrom(wrapperValue.GetType()))
				{
					converted = wrapperValue;
					return true;
				}

				if (TryConvertByKnownMappings(wrapperValue, targetTypeInfo, effectiveType, out convertedSpecial))
				{
					converted = convertedSpecial;
					return true;
				}
			}

			if (effectiveType.IsEnum)
			{
				if (wrapperValue is string enumName)
				{
					object? enumValue;
					if (System.Enum.TryParse(effectiveType, enumName, true, out enumValue))
					{
						converted = enumValue;
						return true;
					}
				}

				if (wrapperValue is int intValue)
				{
					converted = System.Enum.ToObject(effectiveType, intValue);
					return true;
				}
			}

			if (wrapperValue is System.IConvertible && typeof(System.IConvertible).IsAssignableFrom(effectiveType))
			{
				try
				{
					converted = System.Convert.ChangeType(wrapperValue, effectiveType, System.Globalization.CultureInfo.InvariantCulture);
					return true;
				}
				catch
				{
				}
			}

			System.Reflection.ConstructorInfo? constructor = targetType.GetConstructor(new[] { unwrapped.GetType() });
			if (constructor != null)
			{
				converted = constructor.Invoke(new object?[] { unwrapped });
				return true;
			}

			if (effectiveSourceTypeInfo != null && IsAssignableFrom(effectiveSourceTypeInfo, targetTypeInfo))
			{
				converted = unwrapped;
				return true;
			}

			return false;
		}

		/// <summary>
		/// Type-compatibility rules used during compilation and runtime assignment.
		/// </summary>
		public static bool IsAssignableFrom(TypeInfo? infoSource, TypeInfo infoTarget)
		{
			if (infoTarget == null)
				return false;

			if (infoSource is PrototypeTypeInfo prototypeSourceType)
			{
				Prototype protoReturned = prototypeSourceType.Prototype;

				if (infoTarget is PrototypeTypeInfo prototypeTargetType)
					return Prototypes.TypeOf(protoReturned, prototypeTargetType.Prototype);

				if (infoTarget is DotNetTypeInfo dotNetTypeInfo)
					return dotNetTypeInfo.Type.IsAssignableFrom(protoReturned.GetType());

				return infoTarget.Type == typeof(Prototype);
			}

			if (infoSource == null)
				return true;

			if (infoSource.Type == infoTarget.Type)
				return true;

			if (infoTarget.Type.IsAssignableFrom(infoSource.Type))
				return true;

			if (infoTarget.Type.GetConstructor(new[] { infoSource.Type }) != null)
				return true;

			if (infoSource.Type.IsGenericType && infoSource.Type.GetGenericTypeDefinition() == typeof(Task<>))
			{
				System.Type taskResultType = infoSource.Type.GetGenericArguments()[0];
				if (infoTarget.Type.IsAssignableFrom(taskResultType))
					return true;
			}

			if (infoSource.Type == typeof(IntWrapper))
			{
				if (infoTarget is PrototypeTypeInfo targetPrototype && Prototypes.TypeOf(targetPrototype.Prototype, System_Int32.Prototype))
					return true;

				if (infoTarget.Type == typeof(int))
					return true;
			}

			if (infoSource.Type == typeof(int) && infoTarget.Type == typeof(IntWrapper))
				return true;

			if (infoSource.Type == typeof(int?) && infoTarget.Type == typeof(IntWrapper))
				return true;

			if (infoSource.Type == typeof(StringWrapper))
			{
				if (infoTarget is PrototypeTypeInfo targetPrototype && Prototypes.TypeOf(targetPrototype.Prototype, System_String.Prototype))
					return true;

				if (infoTarget.Type == typeof(string))
					return true;

				if (infoTarget.Type == typeof(StringReference))
					return true;
			}

			if (infoSource.Type == typeof(string) && infoTarget.Type == typeof(StringWrapper))
				return true;

			if (infoSource.Type == typeof(string) && infoTarget.Type == typeof(StringReference))
				return true;

			if (infoSource.Type == typeof(StringReference))
			{
				if (infoTarget.Type == typeof(string) || infoTarget.Type == typeof(StringWrapper) || infoTarget.Type == typeof(StringReference))
					return true;

				if (infoTarget is PrototypeTypeInfo targetPrototypeType && Prototypes.TypeOf(targetPrototypeType.Prototype, System_String.Prototype))
					return true;
			}

			if (infoSource.Type == typeof(BoolWrapper))
			{
				if (infoTarget is PrototypeTypeInfo targetPrototype && Prototypes.TypeOf(targetPrototype.Prototype, System_Boolean.Prototype))
					return true;

				if (infoTarget.Type == typeof(bool))
					return true;
			}

			if (infoSource.Type == typeof(bool) && infoTarget.Type == typeof(BoolWrapper))
				return true;

			if (infoSource.Type == typeof(DoubleWrapper))
			{
				if (infoTarget is PrototypeTypeInfo targetPrototype && Prototypes.TypeOf(targetPrototype.Prototype, System_Double.Prototype))
					return true;

				if (infoTarget.Type == typeof(double))
					return true;
			}

			if (infoSource.Type == typeof(double) && infoTarget.Type == typeof(DoubleWrapper))
				return true;

			if (infoTarget.Type == typeof(Prototype))
				return true;

			return false;
		}

		/// <summary>
		/// Returns candidate CLR receiver types for .NET method resolution on a source runtime type.
		/// </summary>
		public static IReadOnlyList<System.Type> GetDotNetReceiverTypes(TypeInfo? sourceTypeInfo)
		{
			List<System.Type> receiverTypes = new List<System.Type>();
			if (sourceTypeInfo == null)
				return receiverTypes;

			AddUniqueType(receiverTypes, sourceTypeInfo.Type);

			AddKnownWrapperReceiverType(receiverTypes, sourceTypeInfo.Type);

			if (sourceTypeInfo is PrototypeTypeInfo prototypeTypeInfo && prototypeTypeInfo.Prototype != null)
			{
				Prototype prototype = prototypeTypeInfo.Prototype;
				if (Prototypes.TypeOf(prototype, System_String.Prototype))
					AddUniqueType(receiverTypes, typeof(string));
				else if (Prototypes.TypeOf(prototype, System_Int32.Prototype))
					AddUniqueType(receiverTypes, typeof(int));
				else if (Prototypes.TypeOf(prototype, System_Boolean.Prototype))
					AddUniqueType(receiverTypes, typeof(bool));
				else if (Prototypes.TypeOf(prototype, System_Double.Prototype))
					AddUniqueType(receiverTypes, typeof(double));
			}

			return receiverTypes;
		}

		private static object? UnwrapRuntimeValue(object? value, out TypeInfo? sourceTypeInfo)
		{
			sourceTypeInfo = null;
			object? current = value;

			while (current is ValueRuntimeInfo || current is PrototypeTypeInfo)
			{
				if (current is ValueRuntimeInfo runtimeValue)
				{
					sourceTypeInfo = runtimeValue.Type;
					current = runtimeValue.Value;
					continue;
				}

				if (current is PrototypeTypeInfo prototypeTypeInfo)
				{
					sourceTypeInfo = prototypeTypeInfo;
					current = prototypeTypeInfo.Prototype;
					continue;
				}
			}

			if (current is NativeValuePrototype nativeValue)
				current = nativeValue.NativeValue;

			return current;
		}

		private static object? UnwrapWrapperValue(object? value)
		{
			if (value is StringWrapper stringWrapper)
				return stringWrapper.GetStringValue();

			if (value is IntWrapper intWrapper)
				return intWrapper.GetIntValue();

			if (value is BoolWrapper boolWrapper)
				return boolWrapper.GetBoolValue();

			if (value is DoubleWrapper doubleWrapper)
				return doubleWrapper.GetDoubleValue();

			return value;
		}

		private static bool TryConvertByKnownMappings(object? value, TypeInfo targetTypeInfo, System.Type effectiveType, out object? converted)
		{
			converted = null;
			if (value == null)
				return false;

			if (TryConvertJsonValueLike(value, effectiveType, out converted))
				return true;

			if (value is StringReference stringReferenceValue)
			{
				if (effectiveType == typeof(StringReference))
				{
					converted = stringReferenceValue;
					return true;
				}

				if (effectiveType == typeof(string))
				{
					if (stringReferenceValue.TryResolveString(out string? stringValue))
					{
						converted = stringValue;
						return true;
					}

					return false;
				}

				if (effectiveType == typeof(StringWrapper))
				{
					if (stringReferenceValue.TryResolvePrototype(out Prototype? wrappedPrototype))
					{
						converted = new StringWrapper(wrappedPrototype!);
						return true;
					}

					return false;
				}

				if (effectiveType == typeof(Prototype))
				{
					if (stringReferenceValue.TryResolvePrototype(out Prototype? resolvedPrototype))
					{
						converted = resolvedPrototype;
						return true;
					}

					return false;
				}

				if (targetTypeInfo is PrototypeTypeInfo stringPrototypeType
					&& Prototypes.TypeOf(stringPrototypeType.Prototype, System_String.Prototype))
				{
					if (stringReferenceValue.TryResolvePrototype(out Prototype? resolvedStringPrototype))
					{
						converted = resolvedStringPrototype;
						return true;
					}

					return false;
				}
			}

			if (value is Prototype prototype)
			{
				if (TryConvertPrototype(prototype, targetTypeInfo, effectiveType, out converted))
					return true;
			}
			else if (typeof(Prototype).IsAssignableFrom(effectiveType))
			{
				Prototype? boxedPrototype = ToPrototype(value);
				if (boxedPrototype != null && effectiveType.IsAssignableFrom(boxedPrototype.GetType()))
				{
					converted = boxedPrototype;
					return true;
				}
			}
			else if (targetTypeInfo is PrototypeTypeInfo targetPrototypeTypeInfo)
			{
				Prototype? boxedPrototype = ToPrototype(value);
				if (boxedPrototype != null && Prototypes.TypeOf(boxedPrototype, targetPrototypeTypeInfo.Prototype))
				{
					converted = boxedPrototype;
					return true;
				}
			}

			if (value is string str)
			{
				if (effectiveType == typeof(StringReference))
				{
					string trimmed = str?.Trim() ?? string.Empty;
					if (trimmed.StartsWith("System.String[ref:", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith("]", StringComparison.Ordinal))
					{
						converted = new StringReference(trimmed);
						return true;
					}

					converted = StringReference.FromString(str);
					return true;
				}

				if (effectiveType == typeof(StringWrapper))
				{
					converted = new StringWrapper(str);
					return true;
				}

				if (targetTypeInfo is PrototypeTypeInfo prototypeTypeInfo && Prototypes.AreShallowEqual(prototypeTypeInfo.Prototype, System_String.Prototype))
				{
					converted = new StringWrapper(str);
					return true;
				}

				if (effectiveType == typeof(Prototype))
				{
					converted = new StringWrapper(str);
					return true;
				}

				if (effectiveType == typeof(string))
				{
					converted = str;
					return true;
				}
			}

			if (value is int i)
			{
				if (effectiveType == typeof(IntWrapper))
				{
					converted = new IntWrapper(i);
					return true;
				}

				if (targetTypeInfo is PrototypeTypeInfo prototypeTypeInfo && Prototypes.AreShallowEqual(prototypeTypeInfo.Prototype, System_Int32.Prototype))
				{
					converted = new IntWrapper(i);
					return true;
				}

				if (effectiveType == typeof(Prototype))
				{
					converted = new IntWrapper(i);
					return true;
				}

				if (effectiveType == typeof(int))
				{
					converted = i;
					return true;
				}
			}

			if (value is bool b)
			{
				if (effectiveType == typeof(BoolWrapper))
				{
					converted = new BoolWrapper(b);
					return true;
				}

				if (targetTypeInfo is PrototypeTypeInfo prototypeTypeInfo && Prototypes.AreShallowEqual(prototypeTypeInfo.Prototype, System_Boolean.Prototype))
				{
					converted = new BoolWrapper(b);
					return true;
				}

				if (effectiveType == typeof(Prototype))
				{
					converted = new BoolWrapper(b);
					return true;
				}

				if (effectiveType == typeof(bool))
				{
					converted = b;
					return true;
				}
			}

			if (value is double d)
			{
				if (effectiveType == typeof(DoubleWrapper))
				{
					converted = new DoubleWrapper(d);
					return true;
				}

				if (targetTypeInfo is PrototypeTypeInfo prototypeTypeInfo && Prototypes.AreShallowEqual(prototypeTypeInfo.Prototype, System_Double.Prototype))
				{
					converted = new DoubleWrapper(d);
					return true;
				}

				if (effectiveType == typeof(Prototype))
				{
					converted = new DoubleWrapper(d);
					return true;
				}

				if (effectiveType == typeof(double))
				{
					converted = d;
					return true;
				}
			}

			return false;
		}
		private static bool TryConvertJsonValueLike(object value, System.Type effectiveType, out object? converted)
		{
			converted = null;

			if (!string.Equals(effectiveType.FullName, "BasicUtilities.JsonValue", System.StringComparison.Ordinal))
				return false;

			if (effectiveType.IsAssignableFrom(value.GetType()))
			{
				converted = value;
				return true;
			}

			if (value is string stringValue)
			{
				System.Reflection.ConstructorInfo? nonParsingStringConstructor = effectiveType.GetConstructor(new[] { typeof(string), typeof(bool) });
				if (nonParsingStringConstructor != null)
				{
					converted = nonParsingStringConstructor.Invoke(new object[] { stringValue, false });
					return true;
				}
			}

			System.Reflection.ConstructorInfo? objectConstructor = effectiveType.GetConstructor(new[] { typeof(object) });
			if (objectConstructor != null)
			{
				converted = objectConstructor.Invoke(new object[] { value });
				return true;
			}

			return false;
		}
		private static bool TryConvertPrototype(Prototype prototype, TypeInfo targetTypeInfo, System.Type effectiveType, out object? converted)
		{
			converted = null;

			if (effectiveType == typeof(Prototype))
			{
				converted = prototype;
				return true;
			}

			if (effectiveType == typeof(StringReference) && Prototypes.TypeOf(prototype, System_String.Prototype))
			{
				converted = new StringReference(prototype.PrototypeName);
				return true;
			}

			if (targetTypeInfo is PrototypeTypeInfo targetPrototypeTypeInfo && Prototypes.TypeOf(prototype, targetPrototypeTypeInfo.Prototype))
			{
				converted = prototype;
				return true;
			}

			if (effectiveType == typeof(string))
			{
				object? convertedString = ConvertPrototypeToPrimitive(prototype, typeof(string));
				if (convertedString != null)
				{
					converted = convertedString;
					return true;
				}
			}

			if (effectiveType == typeof(int))
			{
				object? convertedInt = ConvertPrototypeToPrimitive(prototype, typeof(int));
				if (convertedInt != null)
				{
					converted = convertedInt;
					return true;
				}
			}

			if (effectiveType == typeof(bool))
			{
				object? convertedBool = ConvertPrototypeToPrimitive(prototype, typeof(bool));
				if (convertedBool != null)
				{
					converted = convertedBool;
					return true;
				}
			}

			if (effectiveType == typeof(double))
			{
				object? convertedDouble = ConvertPrototypeToPrimitive(prototype, typeof(double));
				if (convertedDouble != null)
				{
					converted = convertedDouble;
					return true;
				}
			}

			if (effectiveType == typeof(StringWrapper) && Prototypes.TypeOf(prototype, System_String.Prototype))
			{
				converted = new StringWrapper(prototype);
				return true;
			}

			if (effectiveType == typeof(IntWrapper) && Prototypes.TypeOf(prototype, System_Int32.Prototype))
			{
				converted = new IntWrapper(prototype);
				return true;
			}

			if (effectiveType == typeof(BoolWrapper) && Prototypes.TypeOf(prototype, System_Boolean.Prototype))
			{
				converted = new BoolWrapper(prototype);
				return true;
			}

			if (effectiveType == typeof(DoubleWrapper) && Prototypes.TypeOf(prototype, System_Double.Prototype))
			{
				converted = new DoubleWrapper(prototype);
				return true;
			}

			return false;
		}

		private static object? ConvertPrototypeToPrimitive(Prototype prototype, System.Type targetType)
		{
			try
			{
				if (targetType == typeof(StringReference) && Prototypes.TypeOf(prototype, System_String.Prototype))
					return new StringReference(prototype.PrototypeName);

				if (targetType == typeof(string) && Prototypes.TypeOf(prototype, System_String.Prototype))
					return StringWrapper.ToString(prototype);

				if (targetType == typeof(int) && Prototypes.TypeOf(prototype, System_Int32.Prototype))
					return IntWrapper.ToInteger(prototype);

				if (targetType == typeof(bool) && Prototypes.TypeOf(prototype, System_Boolean.Prototype))
					return BoolWrapper.ToBoolean(prototype);

				if (targetType == typeof(double) && Prototypes.TypeOf(prototype, System_Double.Prototype))
					return DoubleWrapper.ToDouble(prototype);
			}
			catch
			{
			}

			return null;
		}

		private static void AddKnownWrapperReceiverType(List<System.Type> receiverTypes, System.Type sourceType)
		{
			if (sourceType == typeof(StringWrapper))
			{
				AddUniqueType(receiverTypes, typeof(string));
				return;
			}

			if (sourceType == typeof(IntWrapper))
			{
				AddUniqueType(receiverTypes, typeof(int));
				return;
			}

			if (sourceType == typeof(BoolWrapper))
			{
				AddUniqueType(receiverTypes, typeof(bool));
				return;
			}

			if (sourceType == typeof(DoubleWrapper))
			{
				AddUniqueType(receiverTypes, typeof(double));
				return;
			}
		}

		private static void AddUniqueType(List<System.Type> receiverTypes, System.Type type)
		{
			if (!receiverTypes.Contains(type))
				receiverTypes.Add(type);
		}
	}
}

