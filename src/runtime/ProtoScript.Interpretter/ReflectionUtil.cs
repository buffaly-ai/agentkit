using Ontology.Simulation;
using System;
using System.Reflection;


namespace ProtoScript.Interpretter
{
	public static class ReflectionUtil
	{
		//──────────────────── helpers ──────────────────────────────────────────
		private static System.Type Normalise(System.Type t) => t switch
		{
			var x when x == typeof(IntWrapper) => typeof(int),
			var x when x == typeof(StringWrapper) => typeof(string),
			var x when x == typeof(DoubleWrapper) => typeof(double),
			var x when x == typeof(BoolWrapper) => typeof(bool),
			_ => t
		};

		private static bool IsWrapperFor(System.Type wrapper, System.Type primitive) =>
			(wrapper, primitive) switch
			{
				(System.Type w, System.Type p) when w == typeof(IntWrapper) && p == typeof(int) => true,
				(System.Type w, System.Type p) when w == typeof(StringWrapper) && p == typeof(string) => true,
				(System.Type w, System.Type p) when w == typeof(DoubleWrapper) && p == typeof(double) => true,
				(System.Type w, System.Type p) when w == typeof(BoolWrapper) && p == typeof(bool) => true,
				_ => false
			};

		private static bool IsParamArray(ParameterInfo[] parameters)
		{
			if (parameters.Length == 0)
				return false;

			ParameterInfo lastParameter = parameters[parameters.Length - 1];
			return lastParameter.IsDefined(typeof(ParamArrayAttribute), inherit: false);
		}

		private static int GetRequiredParameterCount(ParameterInfo[] parameters, bool isParamArray)
		{
			int limit = isParamArray ? parameters.Length - 1 : parameters.Length;
			int required = 0;
			for (int i = 0; i < limit; i++)
			{
				if (!parameters[i].IsOptional)
					required++;
			}

			return required;
		}

		private static bool TryScoreTypeMatch(
			System.Type? argType,
			System.Type? originalArgType,
			System.Type destinationType,
			out int score)
		{
			score = 0;

			if (argType == null)
			{
				if (destinationType.IsClass || destinationType.IsInterface || Nullable.GetUnderlyingType(destinationType) != null)
				{
					score = 1;
					return true;
				}

				return false;
			}

			if (argType == destinationType)
				return true;

			System.Type? nullableUnderlying = Nullable.GetUnderlyingType(destinationType);
			if (nullableUnderlying != null)
			{
				if (argType == nullableUnderlying || nullableUnderlying.IsAssignableFrom(argType))
				{
					score = 1;
					return true;
				}
			}

			if (originalArgType != null && IsWrapperFor(originalArgType, destinationType))
			{
				score = 1;
				return true;
			}

			if (originalArgType != null && nullableUnderlying != null && IsWrapperFor(originalArgType, nullableUnderlying))
			{
				score = 1;
				return true;
			}

			if (destinationType.IsAssignableFrom(argType))
			{
				score = 2;
				return true;
			}

			return false;
		}

		//──────────────────── public API ───────────────────────────────────────
		/// <summary>Enumerate all methods with the given <paramref name="name"/> and arity.</summary>
		public static IEnumerable<MethodInfo> GetCandidateMethods(System.Type type, string name, int arity) =>
			type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
				.Where(m => m.Name == name && m.GetParameters().Length == arity);

		/// <summary>Enumerate all constructors with the given arity.</summary>
		public static IEnumerable<ConstructorInfo> GetCandidateConstructors(System.Type type, int arity) =>
			type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
				.Where(c => c.GetParameters().Length == arity);

		/// <summary>
		/// Resolve the most specific overload of <paramref name="strMethodName"/> for the supplied argument types.
		/// Wrapper types (IntWrapper, StringWrapper, …) are treated as their primitive counterparts.
		/// </summary>
		public static MethodInfo? GetMethod(System.Type type,
											string strMethodName,
											IReadOnlyList<System.Type> lstParameters)
		{
			if (lstParameters == null)
				throw new ArgumentNullException(nameof(lstParameters), "Parameter types cannot be null.");

			bool bHasNull = lstParameters.Any(p => p == null);
			var normalised = lstParameters.Select(Normalise).ToArray();

			if (!bHasNull)
			{
				//──── 1. try exact match on normalised parameter list ────────────

				MethodInfo? exact = type.GetMethod(
										strMethodName,
										BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
										binder: null,
										types: normalised,
										modifiers: null);

				if (exact != null)
					return exact;            // best possible match

				//Note: there is no reason behind normalized being first, so it can be switched
				exact = type.GetMethod(
										strMethodName,
										BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
										binder: null,
										types: lstParameters.ToArray(),
										modifiers: null);

				if (exact != null)
					return exact;            // best possible match

			}

			//──── 2. manual scoring among the remaining candidates ───────────
			MethodInfo? best = null;
			int bestScore = int.MaxValue;  // lower == better

			foreach (MethodInfo candidate in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
				.Where(x => x.Name == strMethodName))
			{
				ParameterInfo[] pars = candidate.GetParameters();
				bool isParamArray = IsParamArray(pars);
				int requiredCount = GetRequiredParameterCount(pars, isParamArray);
				if (normalised.Length < requiredCount)
					continue;
				if (!isParamArray && normalised.Length > pars.Length)
					continue;

				bool passArrayAsSingleParam = false;
				if (isParamArray && normalised.Length == pars.Length)
				{
					System.Type? arrArgType = normalised[pars.Length - 1];
					if (arrArgType != null && pars[pars.Length - 1].ParameterType.IsAssignableFrom(arrArgType))
						passArrayAsSingleParam = true;
				}

				int score = 0;
				bool compatible = true;

				for (int i = 0; i < normalised.Length; i++)
				{
					System.Type destinationType;
					bool isExpandedParamArrayElement = false;
					if (isParamArray && !passArrayAsSingleParam && i >= pars.Length - 1)
					{
						System.Type? elementType = pars[pars.Length - 1].ParameterType.GetElementType();
						if (elementType == null)
						{
							compatible = false;
							break;
						}

						destinationType = elementType;
						isExpandedParamArrayElement = true;
					}
					else
					{
						destinationType = pars[i].ParameterType;
					}

					int parameterScore;
					if (!TryScoreTypeMatch(normalised[i], lstParameters[i], destinationType, out parameterScore))
					{
						compatible = false;
						break;
					}

					score += parameterScore;
					if (isExpandedParamArrayElement)
						score += 1;
				}

				if (compatible && normalised.Length < pars.Length)
				{
					for (int i = normalised.Length; i < pars.Length; i++)
					{
						if (isParamArray && i == pars.Length - 1)
							continue;

						if (!pars[i].IsOptional)
						{
							compatible = false;
							break;
						}

						// Prefer overloads that don't require omitted optional parameters.
						score += 1;
					}
				}

				if (compatible && score < bestScore)
				{
					best = candidate;
					bestScore = score;
				}
			}

			return best;
		}
		/// <summary>
		/// Resolve the most specific constructor for the supplied argument types.
		/// Wrapper types (IntWrapper, StringWrapper, …) are treated as their primitive counterparts.
		/// </summary>
		public static ConstructorInfo? GetConstructor(System.Type type, IReadOnlyList<System.Type> lstParameters)
		{
			if (lstParameters == null || lstParameters.Any(p => p == null))
				throw new ArgumentNullException(nameof(lstParameters), "Parameter types cannot be null.");

			System.Type[] normalised = lstParameters.Select(Normalise).ToArray();

			ConstructorInfo? exact = type.GetConstructor(
				BindingFlags.Public | BindingFlags.Instance,
				binder: null,
				types: normalised,
				modifiers: null);

			if (exact != null)
				return exact;            // best possible match

			exact = type.GetConstructor(
				BindingFlags.Public | BindingFlags.Instance,
				binder: null,
				types: lstParameters.ToArray(),
				modifiers: null);

			if (exact != null)
				return exact;            // best possible match

			ConstructorInfo? best = null;
			int bestScore = int.MaxValue;  // lower == better

			foreach (ConstructorInfo candidate in GetCandidateConstructors(type, normalised.Length))
			{
				ParameterInfo[] pars = candidate.GetParameters();
				int score = 0;
				bool compatible = true;

				for (int i = 0; i < pars.Length; i++)
				{
					System.Type arg = normalised[i];
					System.Type dest = pars[i].ParameterType;

					if (arg == dest)                         // exact
						continue;

					if (IsWrapperFor(lstParameters[i], dest)) // wrapper -> primitive
					{
						score += 1;
						continue;
					}

					if (dest.IsAssignableFrom(arg))         // up-cast
					{
						score += 2;
						continue;
					}

					compatible = false;
					break;
				}

				if (compatible && score < bestScore)
				{
					best = candidate;
					bestScore = score;
				}
			}

			return best;
		}


		public static bool HasBaseType(System.Type typeTarget, System.Type type)
		{
			// Everything ultimately derives from object
			if (type == typeof(object)) return true;

			for (System.Type? t = typeTarget; t != null && t != typeof(object); t = t.BaseType)
				if (t == type) return true;

			return false;
		}
	}
}
