using BasicUtilities;
using ProtoScript.Interpretter.RuntimeInfo;
using System.Collections.Concurrent;
using System.Text;

namespace ProtoScript.Interpretter.Symbols
{
	public class Scope 
	{
		protected ConcurrentDictionary<string, object> m_mapSymbols = new ConcurrentDictionary<string, object>();
		public enum ScopeTypes { Global, File, Namespace, Class, Method, Block, Lambda }

		public Scope ? Parent = null;
		public ScopeTypes ScopeType;

		public string Name = string.Empty;

		private static int s_iID = 0;
		public int ID = Interlocked.Increment(ref s_iID);

		public Stack Stack = new Stack();
		public ConcurrentDictionary<string, object> Symbols
		{
			get
			{
				return m_mapSymbols;
			}
		}

		public string ToLogging()
		{
			StringBuilder sb = new StringBuilder();
			if (!StringUtil.IsEmpty(this.Name))
				sb.AppendLine("Scope: " + this.Name);

			sb.AppendLine("ScopeType: " + this.ScopeType.ToString());

			sb.AppendLine("Symbols:");
			foreach (var pair in m_mapSymbols)
			{
				if (pair.Value is VariableRuntimeInfo val)
				{
					VariableRuntimeInfo valStack = (VariableRuntimeInfo) this.Stack[val.Index];
					sb.AppendLine($"\t[{val.Index}] {valStack.Type.ToShortString()} {pair.Key} = {valStack.Value?.ToString()}, {val.Value?.ToString()} (var)");
				}
				else if (pair.Value is ParameterRuntimeInfo val2)
				{
					ParameterRuntimeInfo valStack = (ParameterRuntimeInfo)this.Stack[val2.Index];
					sb.AppendLine($"\t[{val2.Index}] {valStack.Type.ToShortString()} {pair.Key} = {valStack.Value?.ToString()}, {val2.Value?.ToString()} (param)");
				}

				else
					sb.AppendLine("\t" + pair.Key + " = " + pair.Value?.ToString());
			}

			sb.AppendLine("Stack:");
			foreach (var obj in this.Stack)
			{
				sb.AppendLine("\t" + obj?.ToString());
			}


			if (null != this.Parent)
				sb.AppendLine("Parent: " + (null == this.Parent ? "null" : this.Parent.Name));

			return sb.ToString();
		}

		public Scope()
		{
			Parent = null;
		}

		public Scope(ScopeTypes scopeType)
		{
			ScopeType = scopeType;
		}

		public Scope(Scope parent)
		{
			Parent = parent;
		}

		public Scope Clone()
		{
			Scope scope = new Scope();
			scope.Parent = Parent;
			scope.ScopeType = ScopeType;
			scope.Name = Name;
			scope.Stack = this.Stack.Clone();
			scope.ID = this.ID;

			var clonedSymbols = new ConcurrentDictionary<string, object>();
			foreach (var kvp in this.Symbols)
			{
				if (kvp.Value is ICloneable cloneable)
				{
					clonedSymbols[kvp.Key] = cloneable.Clone();
				}
				else if (kvp.Value is null)
				{
					clonedSymbols[kvp.Key] = null;
				}
				else
				{
					throw new InvalidOperationException($"Symbol value of type {kvp.Value?.GetType().Name} does not implement ICloneable.");
				}
			}

			scope.m_mapSymbols = clonedSymbols; 

			return scope;
		}

		public object ? GetSymbol(string strSymbol)
		{
			return m_mapSymbols.TryGetValue(strSymbol, out object obj) ? obj : null;
		}

		public object? GetSymbolRecursively(string strSymbol)
		{
			Scope cursor = this;
			object result;
			while (cursor != null)
			{
				if (cursor.m_mapSymbols.TryGetValue(strSymbol, out result))
					return result;

				cursor = cursor.Parent;
			}
			return null;
		}

		public void InsertSymbol(string strSymbol, object oObj)
		{
			// first attempt: fast path when key is new
			if (!m_mapSymbols.TryAdd(strSymbol, oObj))
			{
				// fallback: overwrite existing value
				m_mapSymbols[strSymbol] = oObj;
			}
		}

		public virtual bool TryGetSymbol(string strSymbol, out object oObj)
		{
			return m_mapSymbols.TryGetValue(strSymbol, out oObj);
		}

		public virtual bool TryGetSymbol<T>(string strSymbol, out T t) where T : class
		{
			object oObj = null;
			t = null; 
			bool bResult = m_mapSymbols.TryGetValue(strSymbol, out oObj);
			if (bResult)
			{
				if (oObj is T)
					t = (oObj as T);
				else
					bResult = false;
			}
			return bResult;
		}

		public bool TryInsertSymbol(string strSymbol, object oObj)
		{
			//bool bResult = false;
			//if (!m_mapSymbols.ContainsKey(strSymbol))
			//{
			//	m_mapSymbols[strSymbol] = oObj;
			//	bResult = true;
			//}
			//return bResult;
			return m_mapSymbols.TryAdd(strSymbol, oObj);
		}


		public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
		{
			foreach (var pair in this.m_mapSymbols)
			{
				yield return pair;
			}

			yield break;
		}

		public override string ToString()
		{
			return "ProtoScript.Interpretter.Symbols.Scope[" + Name + "] (" + ScopeType.ToString() + ")";
		}


	}

	public class FileScope : Scope 
	{
		public FileScope()
		{
			this.ScopeType = ScopeTypes.File;
		}

		protected List<Scope> m_stackActivationRecords = new List<Scope>();
		public List<Scope> Usings
		{
			get
			{
				return m_stackActivationRecords;
			}
		}

		public void AddUsing(Scope scope)
		{
			m_stackActivationRecords.Add(scope); //N20200507-01
		}



		public override bool TryGetSymbol(string strSymbol, out object oObj)
		{
			if (m_mapSymbols.TryGetValue(strSymbol, out oObj))
				return true;

			bool bResult = false; 
			for (int i = 0; i < m_stackActivationRecords.Count && !bResult; i++)
			{
				bResult = m_stackActivationRecords[i].TryGetSymbol(strSymbol, out oObj);
			}

			return bResult;
		}

		public override bool TryGetSymbol<T>(string strSymbol, out T t)
		{
			object oObj = null;
			t = null;

			if (m_mapSymbols.TryGetValue(strSymbol, out oObj))
			{
				if (oObj is T)
				{
					t = oObj as T;
					return true;
				}
			}

			bool bResult = false;
			for (int i = 0; i < m_stackActivationRecords.Count && !bResult; i++)
			{
				bResult = m_stackActivationRecords[i].TryGetSymbol(strSymbol, out t);
			}

			return bResult;
		}
	}
}