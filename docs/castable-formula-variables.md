# Castable FormulaVariable Design

Castable is the only FormulaParser-scanned type without any [FormulaVariable] properties. The fix follows the ItemObject pattern — annotate server-side wrapper properties, not the XML type.

**Why not move FormulaVariable to XML package:** Every other type (StatInfo, Creature, ItemObject) has its annotated properties in the server. Castable should conform to the same pattern.

**Current state:**

- `CastableObject` exists at `Subsystems/Casting/CastableObject.cs`, wraps `Castable Template`
- Only created for scripted castables (`World.cs:317-338`, line 319 skips non-scripted with `continue`)
- `FormulaParser` scans `typeof(Castable)` (XML type) — finds zero [FormulaVariable] properties
- `FormulaEvaluation.Castable` holds raw XML `Castable`

**Changes needed:**

1. **World.cs:317-338** — Create `CastableObject` for ALL castables, not just scripted ones. Scripted ones get script/dialog setup as before; non-scripted ones get a basic `CastableObject { Template = castable, Guid = castable.Guid }`. Store all in WorldState.

2. **CastableObject.cs** — Add [FormulaVariable] properties delegating to Template:
   - `Lines` → CASTABLELINES
   - `Cooldown` → CASTABLECOOLDOWN
   - `UseLevel` → CASTABLEUSELEVEL (set at runtime, player's trained rank)
   - `LevelMin` → CASTABLELEVELMIN (Requirements.FirstOrDefault()?.Level?.Min ?? 0)
   - `MaxTargets` → CASTABLEMAXTARGETS (Intents.FirstOrDefault()?.MaxTargets ?? 0)
   - `StatusDuration` → CASTABLESTATUSDURATION
   - `StatusTick` → CASTABLESTATUSTICK
   - `StatusIntensity` → CASTABLESTATUSINTENSITY

3. **FormulaParser.cs:46** — Change `typeof(Castable)` to `typeof(CastableObject)`

4. **FormulaParser.cs:75-76** — Change `eval.Castable` to `eval.CastableObject` for CASTABLE\* tokens

5. **FormulaEvaluation.cs** — Add `CastableObject CastableObject` field. Keep `Castable Castable` for non-formula usage.

6. **Call sites** — Where `FormulaEvaluation` is created with a Castable:
   - `NumberCruncher._evalFormula` (line 71) — look up CastableObject from WorldState by Guid
   - `Creature.cs:600` (proc formula) — same lookup
   - All NumberCruncher public methods take raw Castable — internal \_evalFormula does the lookup

7. **Revert XML package changes** — Remove FormulaVariable.cs from xml repo, revert [FormulaVariable] on Castable.cs and Extensions/Castable.cs. The UseLevel property rename in Extensions (CastableLevel → UseLevel) should stay since CastableObject will delegate to it.

**How to apply:** This is a server-only change (no XML package dependency). Can go in feature/better-doors immediately once the XML FormulaVariable.cs is reverted.
