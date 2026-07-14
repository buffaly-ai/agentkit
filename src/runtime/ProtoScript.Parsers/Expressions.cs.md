# Expressions.cs Change History

## Standard Arithmetic Precedence (2026-07-14)
- Assign multiplication, division, and remainder higher precedence than addition and subtraction while preserving assignment and logical ordering.
- Design Decision: compile conventional integer arithmetic expressions deterministically so exported ProtoScript rules can calculate input-dependent values.
