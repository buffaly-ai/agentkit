namespace ProtoScript.Interpretter.Compiled
{
public class DotNetFieldReference : Expression
{
public System.Reflection.FieldInfo Field;
public Expression Object;
public bool IsNullConditional;
}

public class DotNetPropertyReference: Expression
{
public System.Reflection.PropertyInfo Property;
public Expression Object;
public bool IsNullConditional;
}
}
