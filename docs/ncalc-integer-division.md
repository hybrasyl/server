# NCalc Integer Division Fix

NCalc uses C# evaluation semantics: `int / int = int` (truncating division). This silently breaks any formula with integer literal division — `25 / 100` evaluates to `0`, not `0.25`.

**Why:** Discovered via regen formulas returning 0. `SOURCEBASEHP * ((25 - ...) / 100 + SOURCEBONUSREGEN)` — the `25/100` truncates to 0, so regen is always zero. Affects ALL formulas going through `FormulaParser.Eval()`, not just regen.

**How to apply:** Fix once in `FormulaParser.Eval()` (`hybrasyl/Subsystems/Formulas/FormulaParser.cs:95-112`) by regex-replacing integer literals with float literals before NCalc parses the expression:

```csharp
expression = Regex.Replace(expression, @"(?<!\.\d*)\b(\d+)\b(?!\.\d*)", "$1.0");
```

This turns `15/12` into `15.0/12.0` globally. Covers literal/literal and literal/variable division for all current and future formulas. No per-formula `.0` suffixes needed.

Also fix double-evaluate bug on lines 103-104: `e.Evaluate()` is called twice (result of first call discarded). Should evaluate once.
