using Ontology;
using BasicUtilities;
using BasicUtilities.Collections;
using ProtoScript.Interpretter.RuntimeInfo;
using System.Text;

namespace ProtoScript.Interpretter.Symbols
{
	public class SymbolTable
	{
		private readonly object m_syncRoot = new object();

		protected List<Scope> m_stackActivationRecords = new List<Scope>();
		protected List<Scope> m_stackActiveScopes = new List<Scope>();

		public List<Scope> ActiveScopes
		{
			get
			{
				return m_stackActiveScopes;
			}
		}

		protected Scope ? m_pActiveScope = null;

		public Stack GlobalStack
		{
			get
			{
				return this.GetGlobalScope().Stack;
			}
		}

		public Stack LocalStack
		{
			get
			{
				return this.ActiveScope().Stack;
			}
		}

		public Scope ? MethodScope
		{
			get
			{
				return GetTopScope(Scope.ScopeTypes.Method);
			}

		}

		public string ToLogging()
		{
			StringBuilder sb = new StringBuilder();
			if (ActiveScope() == this.GetGlobalScope())
			{
				sb.AppendLine("(global)");
				return sb.ToString();
			}


			sb.AppendLine("Active Scope:");
			sb.AppendLine(ActiveScope().ToLogging());

			for (int i = m_stackActiveScopes.Count - 1; i >= 0; i--)
			{
				if (m_stackActiveScopes[i] == this.GetGlobalScope())
					sb.AppendLine("(global)");
				else
					sb.AppendLine(m_stackActiveScopes[i].ToLogging());
			}

			return sb.ToString();

		}

		public Scope ? GetTopScope(Scope.ScopeTypes scopeType)
		{
			if (ActiveScope().ScopeType == scopeType)
				return ActiveScope();

			for (int i = this.ActiveScopes.Count - 1; i >= 0; i--)
			{
				Scope scope = this.ActiveScopes[i];
				if (scope.ScopeType == scopeType)
					return scope;
			}

			return null;
		}
		public SymbolTable()
		{

		}

		public SymbolTable(SymbolTable oCopy)
		{
			lock (oCopy.m_syncRoot)
			{
				foreach (Scope oScope in oCopy.m_stackActivationRecords)
				{
					if (null == oScope)
						throw new InvalidOperationException("Cannot clone SymbolTable: activation record scope was null.");

					this.m_stackActivationRecords.Add(oScope);
				}

				foreach (Scope oScope in oCopy.m_stackActiveScopes)
				{
					if (null == oScope)
						throw new InvalidOperationException("Cannot clone SymbolTable: active scope was null.");

					this.m_stackActiveScopes.Add(oScope);
				}

				this.m_pActiveScope = oCopy.m_pActiveScope;
			}
		}

		public void AddUsing(Scope scope)
		{
			if (null == scope)
				throw new ArgumentNullException(nameof(scope), "Cannot add a null scope to using directives.");

			lock (m_syncRoot)
			{
				//Without this check, we add the namespaces every time we use the file
				if (!m_stackActivationRecords.Any(x => x.Name == scope.Name))
					m_stackActivationRecords.Add(scope); //N20200507-01
			}
		}

		public Scope GetGlobalScope()
		{
			return m_stackActivationRecords[0];
		}

		public void EnterGlobalScope()
		{
			lock (m_syncRoot)
			{
				if (m_stackActivationRecords.Count != 0 ||
					m_stackActiveScopes.Count != 0)
					throw new InvalidOperationException($"Cannot enter global scope because scopes already exist. ActivationRecords={m_stackActivationRecords.Count}, ActiveScopes={m_stackActiveScopes.Count}.");

				m_stackActivationRecords.Add(new Scope() { Name = "(global)" });
				m_pActiveScope = m_stackActivationRecords.Back();
			}
		}

		public void LeaveGlobalScope()
		{
			lock (m_syncRoot)
			{
				if (m_stackActiveScopes.Count != 0)
				{
					throw new InvalidOperationException($"Cannot exit global scope while {m_stackActiveScopes.Count} active scope(s) remain.");
				}

				if (m_stackActivationRecords.Count > 1)
				{
					throw new InvalidOperationException($"Cannot exit global scope while {m_stackActivationRecords.Count} activation record(s) remain.");
				}

				m_stackActivationRecords.Clear();
				m_pActiveScope = null;
			}
		}

		public void ReturnToGlobalScope()
		{
			lock (m_syncRoot)
			{
				m_stackActiveScopes.Clear();
				m_pActiveScope = m_stackActivationRecords.Back();
			}
		}

		public void EnterScope(Scope pAct)
		{
			if (null == pAct)
				throw new ArgumentNullException(nameof(pAct), "Cannot enter a null scope.");

			lock (m_syncRoot)
			{
				if (null == m_pActiveScope)
					throw new InvalidOperationException("Cannot enter a new scope because there is no current active scope.");

				m_stackActiveScopes.Add(m_pActiveScope);
				m_pActiveScope = pAct;
			}
		}

		public void LeaveScope()
		{
			lock (m_syncRoot)
			{
				m_pActiveScope = m_stackActiveScopes.Back();
				m_stackActiveScopes.PopBack();
			}
		}

		public object? GetSymbol(string in_strSymbol)
		{
			object? oObj = null;
			if (!TryGetSymbol(in_strSymbol, out oObj))
				oObj = null;

			return oObj;
		}

		public object? GetSymbolAndScope(string in_strSymbol, out Scope oScope)
		{
			object? oObj = null;
			if (!TryGetSymbolAndScope(in_strSymbol, out oObj, out oScope))
				return null;

			return oObj;
		}

		public bool TryGetSymbol(string in_strSymbol, out object oObj)
		{
			lock (m_syncRoot)
			{
				if (null == m_pActiveScope)
					throw new InvalidOperationException($"Cannot resolve symbol '{in_strSymbol}' because there is no active scope.");

				bool bResult = false;
				oObj = null;

				for (Scope ? pAct = m_pActiveScope; !bResult && pAct != null; pAct = pAct.Parent)
				{
					bResult = pAct.TryGetSymbol(in_strSymbol, out oObj);
				}

				if (!bResult && !m_stackActiveScopes.Empty())
				{
					for (int i = m_stackActiveScopes.Count - 1; !bResult && i >= 0; i--)
					{
						for (Scope? pAct = m_stackActiveScopes[i]; !bResult && pAct != null; pAct = pAct.Parent)
						{
							bResult = pAct.TryGetSymbol(in_strSymbol, out oObj);
						}
					}
				}

				//N20200507-01
				if (!bResult)
				{
					for (int i = 0; i < m_stackActivationRecords.Count && !bResult; i++)
					{
						bResult = m_stackActivationRecords[i]?.TryGetSymbol(in_strSymbol, out oObj) ?? false;
					}
				}

				return bResult;
			}
		}

		public bool TryGetSymbol<T>(string in_strSymbol, out T oObj) where T : class
		{
			//>use TryGetSymbol (by object) and convert to T
			if (TryGetSymbol(in_strSymbol, out object oObj2))
			{
				oObj = oObj2 as T;
				return oObj != null;
			}
			oObj = null;
			return false;
		}

		public bool TryGetSymbolAndScope(string in_strSymbol, out object oObj, out Scope oScope)
		{
			lock (m_syncRoot)
			{
				if (null == m_pActiveScope)
					throw new InvalidOperationException($"Cannot resolve symbol '{in_strSymbol}' with scope because there is no active scope.");

				bool bResult = false;
				oObj = null;
				oScope = null;

				for (Scope? pAct = m_pActiveScope; !bResult && pAct != null; pAct = pAct.Parent)
				{
					bResult = pAct.TryGetSymbol(in_strSymbol, out oObj);
					if (bResult)
						oScope = pAct;
				}

				if (!bResult && !m_stackActiveScopes.Empty())
				{
					for (int i = m_stackActiveScopes.Count - 1; !bResult && i >= 0; i--)
					{
						for (Scope? pAct = m_stackActiveScopes[i]; !bResult && pAct != null; pAct = pAct.Parent)
						{
							bResult = pAct.TryGetSymbol(in_strSymbol, out oObj);
							if (bResult)
								oScope = pAct;
						}
					}
				}

				return bResult;
			}
		}

		public void InsertSymbol(string in_strSymbol, Object oObj)
		{
			lock (m_syncRoot)
			{
				ActiveScope().InsertSymbol(in_strSymbol, oObj);
			}
		}

		public bool TryInsertSymbol(string in_strSymbol, Object oObj)
		{
			if (StringUtil.IsEmpty(in_strSymbol))
				return false;


			return ActiveScope().TryInsertSymbol(in_strSymbol, oObj);
		}

		public Scope ActiveScope()
		{
			lock (m_syncRoot)
			{
				if (m_stackActivationRecords.Count == 0)
					throw new InvalidOperationException("No activation records exist in the symbol table.");

				if (m_pActiveScope == null)
					throw new InvalidOperationException("No active scope exists to insert or resolve symbols.");

				return m_pActiveScope;
			}
		}

		public bool TryGetScope(string in_strSymbol, out Scope pAct)
		{
			bool bResult = false;
			object oObj = null;

			if (TryGetSymbol(in_strSymbol, out oObj) && oObj is Scope)
			{
				pAct = ((Scope)oObj);
				bResult = true;
			}
			else
				pAct = null;

			return bResult;
		}

		static public bool AllowNamespaces = true;
		public TypeInfo ? GetTypeInfo(string strTypeName)
		{
			if (!AllowNamespaces)
				return GetGlobalScope().GetSymbol(strTypeName) as TypeInfo;

			if (strTypeName.StartsWith("global."))
				return GetGlobalScope().GetSymbol(StringUtil.RightOfFirst(strTypeName, "global.")) as TypeInfo;

			object ? oObj = null;
			bool bResult = TryGetType(strTypeName, out oObj);
			TypeInfo ? typeInfo = oObj as TypeInfo;

			return typeInfo;
		}

		public TypeInfo? GetTypeInfo(ProtoScript.Type type)
		{
			TypeInfo? typeInfo = GetTypeInfo(type.TypeName);

			if (typeInfo is PrototypeTypeInfo)
			{
				PrototypeTypeInfo ? prototypeTypeInfo = typeInfo as PrototypeTypeInfo;
				if (prototypeTypeInfo?.IsGeneric == true)
				{
					PrototypeTypeInfo? typeGeneric = GetTypeInfo(type.GetNonGenericName()) as PrototypeTypeInfo;
					if (null == typeGeneric)
					{
						typeGeneric = CreateGenericInstance(type, prototypeTypeInfo);
					}

					typeInfo = typeGeneric;
				}
			}
			else if (typeInfo is DotNetTypeInfo)
			{
				DotNetTypeInfo dotNetTypeInfo = (DotNetTypeInfo)typeInfo;
				if (dotNetTypeInfo.Type.IsGenericTypeDefinition)
				{
					System.Type[] typeElements = type.ElementTypes.Select(t => GetTypeInfo(t)?.Type ?? throw new InvalidOperationException($"Could not resolve type info for generic argument '{t.TypeName}' while binding '{type.TypeName}'.")).ToArray();

					dotNetTypeInfo.Type = dotNetTypeInfo.Type.MakeGenericType(typeElements);
				}

			}


			return typeInfo;
		}

		public PrototypeTypeInfo CreateGenericInstance(ProtoScript.Type typeOf, PrototypeTypeInfo genericType)
		{
			lock (m_syncRoot)
			{
				PrototypeTypeInfo nonGeneric = (PrototypeTypeInfo)genericType.Clone();
				nonGeneric.Scope = genericType.Scope.Clone(); //doesn't happen automatically 
				nonGeneric.Scope.Symbols["that"] = nonGeneric;
				nonGeneric.Scope.Parent = genericType.Scope;
				nonGeneric.Generic = genericType;

				foreach (Type type in typeOf.ElementTypes)
				{
					TypeInfo ? typeInfo = GetTypeInfo(type);
					if (null == typeInfo)
					{
						TryGetSymbol(type.TypeName, out object oObj);
						if (oObj is ValueRuntimeInfo)
						{
						}
						//N20240929-02 - See notes on extending this to allow a variable type to be passed in
						throw new InvalidOperationException($"Generic element type '{type.TypeName}' was not found while creating '{typeOf.TypeName}'. Variable-based generic inference is not supported here.");
					}
				}


				string strName = typeOf.GetNonGenericName();
				nonGeneric.Prototype = SimpleInterpretter.NewPrototype(strName);

				//A bound generic type should have the base generic as a parent, e.g. BoundCharacter<C> : BoundCharacter
				TypeOfs.Insert(nonGeneric.Prototype, genericType.Prototype.PrototypeID);

				nonGeneric.Index = this.GlobalStack.Add(nonGeneric);
				this.GetGlobalScope().InsertSymbol(strName, nonGeneric);

				return nonGeneric;
			}
		}

		public bool TryGetType(string in_strSymbol, out object oObj)
		{
			lock (m_syncRoot)
			{
				bool bResult = false;
				oObj = null;

				for (Scope ? pAct = m_pActiveScope; !bResult && pAct != null; pAct = pAct.Parent)
				{
					bResult = pAct.TryGetSymbol(in_strSymbol, out oObj);
					bResult = bResult && IsType(oObj);
				}

				if (!bResult && !m_stackActiveScopes.Empty())
				{
					for (int i = m_stackActiveScopes.Count - 1; !bResult && i >= 0; i--)
					{
						for (Scope ? pAct = m_stackActiveScopes[i]; !bResult && pAct != null; pAct = pAct.Parent)
						{
							bResult = pAct.TryGetSymbol(in_strSymbol, out oObj);
							bResult = bResult && IsType(oObj);
						}
					}
				}

				//N20200507-01
				if (!bResult)
				{
					for (int i = 0; i < m_stackActivationRecords.Count && !bResult; i++)
					{
						bResult = m_stackActivationRecords[i].TryGetSymbol(in_strSymbol, out oObj);
						bResult = bResult && IsType(oObj);
					}
				}

				return bResult;
			}
		}

		public bool IsType(object oObj)
		{
			// true  ↦ any non-null object that is NOT one of the two disallowed types
			// false ↦ null, FieldTypeInfo, or FunctionRuntimeInfo
			return oObj is not null
				&& oObj is not FieldTypeInfo
				&& oObj is not FunctionRuntimeInfo;
		}





	}
}
