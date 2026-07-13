using ProtoScript.Interpretter.RuntimeInfo;

namespace ProtoScript.Interpretter.Compiled
{
public class PrototypeFieldReference : BinaryExpression
{
public FieldTypeInfo FieldInfo;
public bool AllowLazyInitializaton = true;
public bool IsNullConditional;
}
}
