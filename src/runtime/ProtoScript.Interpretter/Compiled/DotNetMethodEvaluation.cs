namespace ProtoScript.Interpretter.Compiled
{
public class DotNetMethodEvaluation : Expression
{
public System.Reflection.MethodInfo Method;
public List<Compiled.Expression> Parameters = new List<Expression>();
public Expression Object;
public bool IsNullConditional;
}
}
