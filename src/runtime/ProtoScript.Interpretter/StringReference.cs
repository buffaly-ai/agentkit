using Ontology;
using Ontology.BaseTypes;
using Ontology.Simulation;
using System.Security.Cryptography;
using System.Text;

namespace ProtoScript.Interpretter
{
	// Opaque handle for large string values that crosses the C#/ProtoScript boundary by prototype name.
	public sealed class StringReference
	{
		private const string HandlePrefix = "ref:";

		public string PrototypeName { get; }

		public StringReference(string prototypeName)
		{
			if (string.IsNullOrWhiteSpace(prototypeName))
				throw new ArgumentException("prototypeName cannot be null or whitespace.", nameof(prototypeName));

			PrototypeName = prototypeName;
		}

		public static StringReference FromString(string value)
		{
			Prototype prototype = GetOrCreateOpaqueStringPrototype(value ?? string.Empty);
			return new StringReference(prototype.PrototypeName);
		}

		public static bool TryFromPrototype(Prototype? prototype, out StringReference? reference)
		{
			reference = null;
			if (prototype == null || !Prototypes.TypeOf(prototype, System_String.Prototype))
				return false;

			reference = new StringReference(prototype.PrototypeName);
			return true;
		}

		public bool TryResolvePrototype(out Prototype? prototype)
		{
			prototype = null;

			Prototype? temporary = TemporaryPrototypes.GetTemporaryPrototypeOrNull(PrototypeName);
			if (temporary != null && Prototypes.TypeOf(temporary, System_String.Prototype))
			{
				prototype = temporary;
				return true;
			}

			Prototype? global = Prototypes.GetPrototypeByPrototypeName(PrototypeName);
			if (global != null && Prototypes.TypeOf(global, System_String.Prototype))
			{
				prototype = global;
				return true;
			}

			return false;
		}

		public bool TryResolveString(out string? value)
		{
			value = null;
			if (!TryResolvePrototype(out Prototype? prototype) || prototype == null)
				return false;

			value = StringWrapper.ToString(prototype);
			return true;
		}

		public override string ToString()
		{
			return PrototypeName;
		}

		private static Prototype GetOrCreateOpaqueStringPrototype(string value)
		{
			string hash = ComputeSha256Hex(value);
			string prototypeName = $"{System_String.Prototype.PrototypeName}[{HandlePrefix}{hash}]";

			Prototype? existing = TemporaryPrototypes.GetTemporaryPrototypeOrNull(prototypeName);
			if (existing != null)
			{
				if (existing is not NativeValuePrototype existingNative)
					throw new Exception("Prototype with name '" + prototypeName + "' is not a NativeValuePrototype, but " + existing.GetType().Name);

				existingNative.NativeValue = value;
				if (!Prototypes.TypeOf(existingNative, System_String.Prototype))
					existingNative.InsertTypeOf(System_String.Prototype);
				return existingNative;
			}

			System.Reflection.ConstructorInfo? ctor = typeof(NativeValuePrototype).GetConstructor(
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
				null,
				System.Type.EmptyTypes,
				null);
			if (ctor == null)
				throw new Exception("Could not locate NativeValuePrototype private constructor.");

			NativeValuePrototype created = (NativeValuePrototype)ctor.Invoke(null);
			created.NativeValue = value;
			created.PrototypeName = prototypeName;
			created.PrototypeID = TemporaryPrototypes.GetOrInsertPrototype(created).PrototypeID;
			if (!Prototypes.TypeOf(created, System_String.Prototype))
				created.InsertTypeOf(System_String.Prototype);
			return created;
		}

		private static string ComputeSha256Hex(string input)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(input);
			byte[] hash = SHA256.HashData(bytes);
			return Convert.ToHexString(hash);
		}
	}
}
