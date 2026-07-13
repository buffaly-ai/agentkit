namespace Ontology
{
	// Tracks all prototype async-local singletons so a global cache reset can clear them safely.
	public sealed class ResettablePrototypeAsyncLocal
	{
		private static readonly object s_lock = new object();
		private static readonly List<ResettablePrototypeAsyncLocal> s_slots = new List<ResettablePrototypeAsyncLocal>();

		private readonly AsyncLocal<Prototype?> m_value = new AsyncLocal<Prototype?>();

		public ResettablePrototypeAsyncLocal()
		{
			lock (s_lock)
			{
				s_slots.Add(this);
			}
		}

		public Prototype? Value
		{
			get { return m_value.Value; }
			set { m_value.Value = value; }
		}

		public static void ResetAll()
		{
			lock (s_lock)
			{
				foreach (ResettablePrototypeAsyncLocal slot in s_slots)
				{
					slot.m_value.Value = null;
				}
			}
		}
	}
}
