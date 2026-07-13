namespace ProtoScript.Interpretter.Symbols
{
	public class Stack : List<object>
	{
		new public int Add(object obj)
		{
			int iIndex = this.Count;
			base.Add(obj);
			return iIndex;
		}

		public Stack Clone()
		{
			var clone = new Stack();

			foreach (object obj in this)
			{
				if (obj is ICloneable cloneable)
				{
					clone.Add(cloneable.Clone());
				}
				else if (obj is null)
				{
					clone.Add(null);
				}
				else
				{
					throw new InvalidOperationException($"Object of type {obj?.GetType().Name} does not implement ICloneable.");
				}
			}

			return clone;
		}
	}
}
