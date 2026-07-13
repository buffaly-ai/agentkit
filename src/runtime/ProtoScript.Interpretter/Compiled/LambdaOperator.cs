//added
using Ontology;
using ProtoScript.Interpretter.RuntimeInfo;

namespace ProtoScript.Interpretter.Compiled
{
	public class LambdaOperator : Expression
	{
		public FunctionRuntimeInfo Function = null;

		public NativeInterpretter Interpretter = null;

		public bool Predicate(Prototype prototype)
		{
			object oReturn = null;

			try
			{
				//Enter the function's scope
				
				Interpretter.Symbols.EnterScope(this.Function.Scope);
				ParameterRuntimeInfo infoParam = Function.Parameters[0];

				Function.Scope.Stack[infoParam.Index] = prototype;

				Compiled.Expression expression = (Function.Statements[0] as ExpressionStatement).Expression;
				oReturn = Interpretter.Evaluate(expression);
			}
			finally
			{
				//Leave the function's scope. Note that this deallocates all parameters and return value,
				//and all local automatic variables
				Interpretter.Symbols.LeaveScope();
			}

			if (oReturn is bool b)
				return b;

			object? converted;
			if (ValueConversions.TryMakeAssignable(oReturn, new TypeInfo(typeof(bool)), out converted) && converted is bool convertedBool)
				return convertedBool;

			string actualType = oReturn?.GetType().FullName ?? "null";
			throw new RuntimeException($"Lambda predicate must return bool-compatible value but returned '{actualType}'.", this.Info);
		}
	}
}
