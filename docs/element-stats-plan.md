# Plan: Element Expansion & Extended Stats Panel

## Context

Three related improvements:

1. Overhaul the element system — 17 combat elements replacing the current 9, with new Random meta-types
2. Send actual MR/Hit/Dmg values via a dedicated custom packet (replacing the byte-rating workaround in 0x08)
3. Extend that packet to also send Crit, MagicCrit, Dodge, MagicDodge (currently server-only stats) — groundwork for a redesigned client stat panel

---

## 1. Element System Overhaul

### New element list (17 combat elements)

| Ordinal | Element   | Notes                                  |
| ------- | --------- | -------------------------------------- |
| 0       | **Force** | Default / unaspected — replaces `None` |
| 1       | Fire      | Existing                               |
| 2       | Water     | Existing                               |
| 3       | Wind      | Existing                               |
| 4       | Earth     | Existing                               |
| 5       | Flesh     | **New**                                |
| 6       | Spirit    | **New**                                |
| 7       | Nature    | **New** (replaces Wood at ordinal 7)   |
| 8       | Metal     | Existing (was ordinal 8)               |
| 9       | Undead    | Existing (keeps ordinal 9)             |
| 10      | Time      | **New**                                |
| 11      | Stasis    | **New**                                |
| 12      | Light     | Existing (was ordinal 5)               |
| 13      | Dark      | Existing (was ordinal 6)               |
| 14      | Life      | **New**                                |
| 15      | Arcane    | **New**                                |
| 16      | Void      | **New**                                |

**Removed:** `None` (replaced by Force), `Wood` (replaced by Nature)

**Meta-resolution types** (appended after combat elements):

| Ordinal | Type           | Resolves To                                |
| ------- | -------------- | ------------------------------------------ |
| 17      | RandomTemuair  | Fire, Water, Wind, Earth                   |
| 18      | RandomExpanded | Metal, Life, Arcane, Void                  |
| 19      | RandomClassic  | Fire, Water, Wind, Earth, Light, Dark      |
| 20      | RandomAll      | All 17 elements except Undead              |
| 21      | Necklace       | Equipped necklace element                  |
| 22      | Belt           | Equipped belt element                      |
| 23      | Current        | Current offensive element override or base |

### Files to modify

**Hybrasyl.Xml repo** (`e:/Dark Ages Dev/Repos/xml/`):

- **`src/XSD/Common.xsd:557-575`** — Replace ElementType enum with new 16 elements + meta types
- **`src/Objects/ElementType.cs`** — Regenerate from XSD (or manual edit since it's auto-generated)

**Server repo** (`e:/Dark Ages Dev/Repos/server/`):

- **`hybrasyl/Objects/Creature.cs:613-623`** — Update element resolution switch:

  ```csharp
  ElementType.RandomTemuair => new[] { ElementType.Fire, ElementType.Water,
      ElementType.Wind, ElementType.Earth }[Random.Shared.Next(4)],
  ElementType.RandomExpanded => new[] { ElementType.Metal, ElementType.Life,
      ElementType.Arcane, ElementType.Void }[Random.Shared.Next(4)],
  ElementType.RandomClassic => new[] { ElementType.Fire, ElementType.Water,
      ElementType.Wind, ElementType.Earth, ElementType.Light,
      ElementType.Dark }[Random.Shared.Next(6)],
  ElementType.RandomAll => /* all 17 except Undead — pick from array of 16 */,
  ```

  - Remove `ElementType.None` case (line 619) — default (`_`) now falls through to the element itself, and Force is the new default

- **`ElementType.None` → `ElementType.Force` rename** — 29 occurrences across 11 files:
  - `hybrasyl/Objects/StatInfo.cs` (6 refs) — field initializers, element override defaults
  - `hybrasyl/Objects/Creature.cs` (5 refs) — element resolution, damage checks
  - `hybrasyl/Subsystems/Formulas/NumberCruncher.cs` (5 refs) — damage calc
  - `hybrasyl/Objects/User.cs` (3 refs) — equipment element assignment
  - `hybrasyl/Objects/Monster.cs` (3 refs) — default element
  - `hybrasyl/Subsystems/Scripting/HybrasylUser.cs` (2 refs)
  - `hybrasyl/Subsystems/Scripting/HybrasylMonster.cs` (1 ref)
  - `hybrasyl/Subsystems/Statuses/CreatureStatus.cs` (1 ref)
  - `hybrasyl/Objects/Merchant.cs` (1 ref)
  - `hybrasyl/Objects/CombatEvents.cs` (1 ref)
  - `hybrasyl/Subsystems/Messaging/ChatCommands/DamageCommand.cs` (1 ref)

  This is a straightforward find-and-replace: `ElementType.None` → `ElementType.Force`

**World XML data** (`F:\Documents\Hybrasyl\world\`):

- **Element table XML** (`xml/elementtables/element_table.xml`) — Needs full rewrite: 17x17 multiplier matrix for the new elements. User will define the multiplier values.
- **Any XML referencing `Wood`, `Undead`, or `None`** — Search and update to new element names (Nature, Time, Force respectively). This includes items, castables, variant groups, statuses.

**Creidhne** (`e:/Dark Ages Dev/Repos/creidhne/`):

- Element dropdown/picker — update to new element list (if hardcoded anywhere)

### Migration concerns

- **Ordinal shift**: Fire(1), Water(2), Wind(3), Earth(4) keep their ordinals. Light shifts from 5→11, Dark from 6→12, Metal stays 8. The 0x08 packet sends `(byte)Stats.BaseOffensiveElement` — the custom client must understand the new ordinals. Stock client will show wrong element names for shifted values (acceptable since we're moving to custom client).
- **Saved player data**: Any serialized element data using raw bytes will misinterpret after ordinal changes. Player save files use JSON with enum names (not ordinals), so `StatInfo` serialization should be safe. Verify this.
- **Existing XML**: Element names in XML are string-based, so `Wood` → `Nature` etc. needs a find-and-replace across world XML files.

---

## 2. Custom Extended Stats Packet

### Current workaround

**`hybrasyl/Objects/User.cs:1807-1822`** — 0x08 Secondary block sends MR/Hit/Dmg as single bytes via rating conversions defined at **`hybrasyl/Objects/StatInfo.cs:1540-1582`**:

- `MrRating`, `DmgRating`, `HitRating` — compress doubles into 0-255 scale, 128 = baseline (1.0x)
- Lossy: can't express negatives, MR only in 10% steps
- These workarounds existed to appease the stock Kru client — we fully control the client now

### Approach: New dedicated packet on unused opcode, raw floats

Use opcode **0x41** (unused in current opcode table at `hybrasyl/Internals/Enums/OpCodes.cs`).

Sent whenever `StatUpdateFlags.Secondary` triggers (same cadence as the existing 0x08 secondary block) so the custom client always has fresh data.

**All values are IEEE 754 floats (4 bytes each)** — no encoding, no rating conversion, just the raw server values. The client reads them directly and displays however it wants.

### Packet layout

```text
Opcode: 0x41 (ExtendedStats)
  float  MR          — raw multiplier (1.16 = +16% resist)
  float  Hit         — raw multiplier (1.05 = +5% hit)
  float  Dmg         — raw multiplier (0.92 = -8% dmg)
  float  Crit        — raw percentage (0.055 = 5.5% crit chance)
  float  MagicCrit   — raw percentage
  float  Dodge       — raw percentage
  float  MagicDodge  — raw percentage
```

Total: 28 bytes. Extensible — future stats (LifeSteal, ReflectPhysical, etc.) can be appended.

### Files modified

- **`hybrasyl/Internals/Enums/OpCodes.cs`** — Added `public const byte ExtendedStats = 0x41;`
- **`hybrasyl/Networking/ServerPacket.cs`** — Added `WriteSingle(float)` method (big-endian IEEE 754)
- **`hybrasyl/Networking/ServerPackets/ExtendedStats.cs`** — New packet class using `WriteSingle`
- **`hybrasyl/Objects/User.cs`** — Sends 0x41 after 0x08 when Secondary flag is set

### Existing 0x08 stays unchanged

The rating properties (`MrRating`, `DmgRating`, `HitRating`) and the 0x08 Secondary block remain as-is. The custom client reads the 0x41 packet for real values. The rating bytes in 0x08 become vestigial but harmless.

### Stat source properties (already exist, no changes needed)

| Stat       | Property                    | File:Line               |
| ---------- | --------------------------- | ----------------------- |
| MR         | `Stats.Mr` (double)         | `StatInfo.cs:1594-1597` |
| Hit        | `Stats.Hit` (double)        | `StatInfo.cs:1584-1587` |
| Dmg        | `Stats.Dmg` (double)        | `StatInfo.cs:1535-1538` |
| Crit       | `Stats.Crit` (double)       | `StatInfo.cs:558`       |
| MagicCrit  | `Stats.MagicCrit` (double)  | `StatInfo.cs:599`       |
| Dodge      | `Stats.Dodge` (double)      | `StatInfo.cs:1121`      |
| MagicDodge | `Stats.MagicDodge` (double) | `StatInfo.cs:1162`      |

---

## Files modified (summary)

**Hybrasyl.Xml repo:**

- `src/XSD/Common.xsd` — New ElementType enum
- `src/Objects/ElementType.cs` — Regenerated

**Server repo:**

- `hybrasyl/Internals/Enums/OpCodes.cs` — Add ExtendedStats opcode
- `hybrasyl/Networking/ServerPacket.cs` — Add WriteSingle(float) method
- `hybrasyl/Networking/ServerPackets/ExtendedStats.cs` — New file
- `hybrasyl/Objects/User.cs` — Send ExtendedStats packet alongside 0x08; `None` → `Force`
- `hybrasyl/Objects/Creature.cs` — Element resolution switch update; `None` → `Force`
- `hybrasyl/Objects/StatInfo.cs` — `None` → `Force`
- `hybrasyl/Objects/Monster.cs` — `None` → `Force`
- `hybrasyl/Objects/Merchant.cs` — `None` → `Force`
- `hybrasyl/Objects/CombatEvents.cs` — `None` → `Force`
- `hybrasyl/Subsystems/Formulas/NumberCruncher.cs` — `None` → `Force`
- `hybrasyl/Subsystems/Scripting/HybrasylUser.cs` — `None` → `Force`
- `hybrasyl/Subsystems/Scripting/HybrasylMonster.cs` — `None` → `Force`
- `hybrasyl/Subsystems/Statuses/CreatureStatus.cs` — `None` → `Force`
- `hybrasyl/Subsystems/Messaging/ChatCommands/DamageCommand.cs` — `None` → `Force`

**World XML data:**

- `xml/elementtables/element_table.xml` — Full rewrite (17x17 matrix, user provides values)
- Any XML referencing Wood/Undead/None — rename to Nature/Time/Force

**Creidhne:**

- Element picker/dropdown updates

## Verification

1. `dotnet build` — compiles clean after all changes
2. `dotnet test` — no regressions
3. Element changes:
   - Grep for any remaining `ElementType.None` or `ElementType.Wood` or `ElementType.Undead` — should be zero
   - Create castable with each new element, verify damage applies with correct element table multiplier
   - Test Random meta-types resolve to correct new ordinal ranges
4. Extended stats packet:
   - Connect with stock client — confirm it ignores the unknown 0x41 packet (no crash/disconnect)
   - Packet capture — verify 0x41 arrives with correct values
   - Apply status that modifies MR/Crit/Dodge, confirm packet values update
5. Serialization:
   - Verify player saves use enum names not ordinals (JSON check)
   - Load a player with an old element value, confirm it deserializes correctly or gracefully defaults to Force
