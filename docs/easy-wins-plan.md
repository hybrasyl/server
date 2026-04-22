# Plan: Easy Wins Branch

## Context

Bundle of small, independent fixes from the Jira backlog and feature gaps identified during codebase exploration. All items are either 1-line changes or < 30 lines each, no architectural decisions needed. Creates a single branch with discrete commits.

## Items

### 1. HS-1481 — Spawn log level (verify)

**File:** `hybrasyl/Subsystems/Spawning/Monolith.cs:215-216`

Investigation found this is already `GameLog.SpawnDebug()`, not Fatal. **Verify** this is the right message — the Jira ticket quotes `[FTL]` level. If a different spawn message is still logging as Fatal, find and fix it. Otherwise mark the ticket as already resolved.

### 2. HS-1491 — NPC DisplayName in chat

**File:** `hybrasyl/Objects/User.cs:457-465` (OnHear method)

Lines 462 and 465 use `e.Speaker.Name` for chat messages. Merchants already have a `DisplayName` property (`Merchant.cs:62`) and use the fallback pattern at line 116: `string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName`.

**Fix:** In `OnHear`, replace `e.Speaker.Name` with a display-name-aware resolution. Since `Speaker` is a `VisibleObject`, check if it has a `DisplayName` property. If only `Merchant` has it, cast-check:

```csharp
var speakerName = e.Speaker is Merchant m && !string.IsNullOrWhiteSpace(m.DisplayName)
    ? m.DisplayName : e.Speaker.Name;
```

Use `speakerName` in both the shout and normal message formatting.

### 3. DisableForget (HS-1295)

**File:** `hybrasyl/Interfaces/IPursuitable.cs:111-127`

The `DisplayPursuits()` method adds "Forget Skill" (line 116-117) and "Forget Secret" (line 125-126) unconditionally when the merchant has Skills/Spells jobs.

**Fix:** Before adding the Forget options, check `merchant.Template.Roles?.DisableForget != true`:

```csharp
if (merchant?.Jobs.HasFlag(MerchantJob.Skills) ?? false)
{
    optionsCount++;
    options.Options.Add(new MerchantDialogOption
    { Id = (ushort)MerchantMenuItem.LearnSkillMenu, Text = "Learn Skill" });
    if (merchant.Template.Roles?.DisableForget != true)
    {
        optionsCount++;
        options.Options.Add(new MerchantDialogOption
        { Id = (ushort)MerchantMenuItem.ForgetSkillMenu, Text = "Forget Skill" });
    }
}
```

Same pattern for Spells block.

### 4. Cone radius cap removal

**File:** `hybrasyl/Objects/Creature.cs:403`

Current: `var radius = Math.Min(tile.Radius, Game.ActiveConfiguration.Constants.ViewportSize / 2);`

Cone is the ONLY shape with a viewport cap — Line, Cross, and Square don't have one.

**Fix:** Remove the cap entirely:

```csharp
var radius = tile.Radius;
```

The cone geometry (`2*i-1` width at distance `i`) already handles any radius correctly. The XML schema can still constrain reasonable values via the XSD.

### 5. ItemObject FormulaVariables

**File:** `hybrasyl/Objects/ItemObject.cs`

Add `[FormulaVariable]` attribute to these existing public properties:

| Line | Property | Token becomes |
|------|----------|---------------|
| 133 | `MinLevel` | `ITEMMINLEVEL` |
| 135 | `MaxLevel` | `ITEMMAXLEVEL` |
| 134 | `MinAbility` | `ITEMMINABILITY` |
| 136 | `MaxAbility` | `ITEMMAXABILITY` |
| 154 | `Value` | `ITEMVALUE` |
| 105 | `Weight` | `ITEMWEIGHT` |
| 148 | `MinLDamage` | `ITEMMINLDAMAGE` |
| 149 | `MaxLDamage` | `ITEMMAXLDAMAGE` |
| 150 | `MinSDamage` | `ITEMMINSDAMAGE` |
| 151 | `MaxSDamage` | `ITEMMAXSDAMAGE` |
| 204 | `Durability` | `ITEMDURABILITY` |
| 114 | `MaximumDurability` | `ITEMMAXIMUMDURABILITY` |

The `[FormulaVariable]` attribute is defined at `hybrasyl/Objects/StatInfo.cs:31-32`. FormulaParser already scans `typeof(ItemObject)` in its static constructor (`hybrasyl/Subsystems/Formulas/FormulaParser.cs:49`), so adding the attribute is all that's needed — no other code changes.

Also update the formula transpiler in creidhne (`creidhne/src/main/formulaTranspiler.js`) to add these to the token map so the Lua stub generator stays in sync.

### 6. Repair.Type filtering

**File:** `hybrasyl/Subsystems/Mundanes/MerchantController.cs:211-285`

`RepairAll` (line 211) and `RepairItem` (line 257) currently repair any item regardless of the NPC's `Repair.Type` setting (Armor/Weapon/All).

**Fix:** At the start of each method, read the merchant's repair type from `request.Merchant.Template.Roles.Repair.Type`. When iterating items to repair, filter:
- `NpcRepairType.Armor` → only repair items in armor/accessory slots
- `NpcRepairType.Weapon` → only repair items in weapon/shield slots
- `NpcRepairType.All` (or null/default) → repair everything (current behavior)

The `NpcRepairType` enum is in the Hybrasyl.Xml package. For `RepairItem`, check the item's slot against the repair type before proceeding.

### 7. Castable FormulaVariables

**Files:** `xml/src/Extensions/Castable.cs` (Hybrasyl.Xml repo) and `creidhne/src/main/formulaTranspiler.js`

FormulaParser already scans `typeof(Castable)` at `hybrasyl/Subsystems/Formulas/FormulaParser.cs:46-47`, but **no Castable properties have `[FormulaVariable]` yet**. The infrastructure is ready — just needs the attributes.

**Candidate properties** (from `xml/src/Objects/Castable.cs` and `xml/src/Extensions/Castable.cs`):

| Property | Type | Source | Token becomes |
|----------|------|--------|---------------|
| `Lines` | `byte` | Castable.cs:254 | `CASTABLELINES` |
| `Cooldown` | `int` | Castable.cs:281 | `CASTABLECOOLDOWN` |
| `CastableLevel` | `byte` | Extensions/Castable.cs:43 | `CASTABLECASTABLELEVEL` |
| `IsAssail` | `bool` | Castable.cs:295 | `CASTABLEISASSAIL` |
| `Reflectable` | `bool` | Castable.cs:309 | `CASTABLEREFLECTABLE` |

**Note:** This requires a Hybrasyl.Xml NuGet package update (same as the element expansion). The `[FormulaVariable]` attribute is defined in the server repo (`StatInfo.cs:31-32`), but the Castable class lives in the XML repo. This means the attribute definition needs to be duplicated or referenced in the XML package, OR the properties need to be wrapped in a server-side class. Investigate which approach is cleaner.

Also update creidhne formula transpiler (`src/main/formulaTranspiler.js`) to add CASTABLE* tokens to the token map.

### 8. NPC Role XML — Schema Expansion (Scoping)

Creidhne generates NPC role XML with features the XSD doesn't support yet. This item scopes out the XSD and server changes needed.

**Gap analysis** (Creidhne output vs `xml/src/XSD/Common.xsd:999-1076`):

| Feature | Bank | Post | Repair | Vend | Train |
|---------|------|------|--------|------|-------|
| ExceptCookie attr | MISSING | MISSING | MISSING | MISSING | MISSING |
| OnlyCookie attr | MISSING | MISSING | MISSING | MISSING | MISSING |
| CostAdjustment element | MISSING | MISSING | MISSING | MISSING | MISSING |
| Core structure | OK | OK | OK | OK | OK |

**What already exists in XSD:**
- `DisableForget` on NpcRoleList — supported
- `Repair.Type` — supported (NpcRepairType enum)
- `Vend.Items` with `Item Name/Quantity/Restock` — fully supported
- `Train.Castable` with `Name/Class` — supported
- `Bank.Nation` / `Bank.Discount` — supported (Discount is the predecessor to CostAdjustment)
- `Post.Surcharge` — exists but different structure than CostAdjustment
- Role-level check attributes (`TrainCheck`, `VendCheck`, etc.) — supported on NpcRoleList

**XSD changes needed** (in `xml/src/XSD/Common.xsd`):

1. **New shared type `NpcRoleCostAdjustment`:**
   ```xml
   <xs:complexType name="NpcRoleCostAdjustment">
     <xs:simpleContent>
       <xs:extension base="xs:float">
         <xs:attribute name="Nation" type="hyb:String8" />
       </xs:extension>
     </xs:simpleContent>
   </xs:complexType>
   ```

2. **Add to each role type** (Bank, Post, Repair, Vend, Train):
   - `ExceptCookie` attribute (`type="hyb:String8"`)
   - `OnlyCookie` attribute (`type="hyb:String8"`)
   - `CostAdjustment` child element (`type="hyb:NpcRoleCostAdjustment" minOccurs="0" maxOccurs="unbounded"`)

3. **Post-specific:** Add `Nation` attribute (`type="hyb:String8"`)

**Server changes needed** (after XSD update + NuGet publish):
- `hybrasyl/Subsystems/Mundanes/MerchantController.cs` — Read CostAdjustment to apply per-nation pricing
- Cookie check integration — Read ExceptCookie/OnlyCookie and check player cookies before showing role options (existing cookie infrastructure at `User.HasCookie()`)
- This touches vending, training, banking, repair, and post flows

**Estimated scope:** Medium — XSD changes are mechanical, but server-side cookie/cost logic touches 5+ handler methods. Should be its own PR after the XSD is published.

## Files modified (server)

- `hybrasyl/Objects/User.cs` — DisplayName in OnHear
- `hybrasyl/Interfaces/IPursuitable.cs` — DisableForget check
- `hybrasyl/Objects/Creature.cs` — cone cap removal
- `hybrasyl/Objects/ItemObject.cs` — FormulaVariable attributes
- `hybrasyl/Subsystems/Mundanes/MerchantController.cs` — Repair.Type filter
- `hybrasyl/Subsystems/Spawning/Monolith.cs` — log level (if needed)

## Files modified (creidhne)

- `src/main/formulaTranspiler.js` — add ITEM* tokens to token map

## Verification

1. Build the server (`dotnet build`) — should compile with no errors
2. Run existing tests (`dotnet test`) — no regressions
3. Manual tests:
   - Create NPC with `<Roles DisableForget="true">` and confirm Forget options don't appear
   - Create NPC with `<DisplayName>` different from `<Name>` and confirm chat uses DisplayName
   - Test cone spell with radius > 3 in XML
   - Use `/eval` with a formula containing `ITEMMINLEVEL` to verify formula variables work
   - Test repair NPC with `Type="Weapon"` to confirm it only repairs weapons
