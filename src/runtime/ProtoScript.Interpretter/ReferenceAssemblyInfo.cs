namespace ProtoScript.Interpretter
{
	public sealed class ReferenceAssemblyInfo
	{
		public string Alias { get; init; } = string.Empty;
		public string RequestedReference { get; init; } = string.Empty;
		public bool IsFileReference { get; init; }
		public string? ResolvedAssemblyPath { get; init; }

		public string? AssemblySimpleName { get; init; }
		public string? AssemblyFullName { get; init; }
		public string? AssemblyVersion { get; init; }
		public string? FileVersion { get; init; }
		public string? InformationalVersion { get; init; }
		public System.DateTime? LastWriteUtc { get; init; }
		public string? LoadedLocation { get; init; }

		public string LoadResolution { get; init; } = string.Empty;
		public bool LoadSucceeded { get; init; }
		public string? Error { get; init; }
	}
}
