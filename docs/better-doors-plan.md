# Better Doors — Plan

Fix Hybrasyl's door handling so that all sizes (1/2/3/4-tile) of doors documented in the companion `doors.md` audit toggle correctly, and drop the inherited sprite-data errors that cause multiple visible bugs on the retail client and on the Chaos.Client.

## Context

The current door system has three compounding problems:

1. **Corrupt sprite data.** `Sprites.ClosedDoorSprites`, `OpenDoorSprites`, and `DoorSprites` (in [hybrasyl/Internals/Sprites.cs](../hybrasyl/Internals/Sprites.cs)) were copied from an incomplete extraction of the `DarkAges.exe` door table at offset `0x0068b8b0`. The extraction contains junk pairs (e.g. `2675/2682`, `2687/2694` — static decorative tiles that aren't doors), cross-paired entries (e.g. `2163 → 4519` pairing unrelated doors' closed/open halves across different door families), a reversed pair (`3151 / 3159`), and — crucially — is missing **roughly half** of all retail door sprites, including most `18xxx` Undine-style doors, all `14xxx`/`15xxx` four-tile doors, `8262/8263`, and many others.
2. **No multi-tile-door model.** The server tracks each door tile as its own `Door` object keyed by `(x, y)` in `Map.Doors`. [`MapObject.ToggleDoors`](../hybrasyl/Objects/MapObject.cs#L484) handles 2-tile doors by scanning `±1` along the `IsLeftRight` axis, but that only works if the clicked tile happens to be adjacent to its sibling. For 3-tile doors (Piet, Undine city gates), clicking the wrong panel means only 1–2 of the 3 panels toggle. [`Game.IsDoorCollision`](../hybrasyl/Game.cs#L897) only checks one variant pair, so 3+ tile doors can end up with inconsistent walkability across panels — hence the pre-existing comment in that method acknowledging "collision updates only occur for two tiles."
3. **No center-only support.** Several 3-tile retail doors visually occupy three tiles but only the center sprite actually changes between states — the outer tiles are static jamb art. The current code either flips nothing on the side tiles (if not registered as doors) or flips them into a mismatched visual (if paired wrongly in the extraction).

The Chaos.Client ships a corrected `DoorTable.cs` regenerated from [Chaos.Client/docs/doors.md](../../Chaos.Client/docs/doors.md). It is the source of truth for this server work. Until Hybrasyl is patched, ~70 door types known to the client render inert against the server (server doesn't recognize them as doors, so no `0x32` flows).

## Data source of truth

[Chaos.Client/docs/doors.md](../../Chaos.Client/docs/doors.md). Hand-audited against retail assets. 81 door definitions covering 1/2/3/4-tile doors plus permanently-open archways. Each row carries the full set of closed-state sprite IDs, open-state sprite IDs, orientation (N/S or E/W), and — for 3-tile doors — whether only the center sprite actually toggles.

## Target behavior

- Any documented door toggles correctly when any of its panels is clicked, regardless of tile count.
- Center-only doors leave the side panels untouched (no spurious `0x32` emissions, no collision changes on those tiles — they keep whatever static walkability they have from SOTP).
- Archways (no-closed-version doors) are recognized so collision is correct but are not interactive.
- `IsLeftRight` / axis is derived from the door definition, not guessed from map geometry.
- Wire protocol (`0x32 Door`) is unchanged; this is purely a server-side data + logic fix.

## Scope

**In:**
- [hybrasyl/Internals/Sprites.cs](../hybrasyl/Internals/Sprites.cs) — full data rewrite
- [hybrasyl/Game.cs](../hybrasyl/Game.cs) — `IsDoorCollision` rewrite
- [hybrasyl/Objects/MapObject.cs](../hybrasyl/Objects/MapObject.cs) — `LoadMapFile` door-scanning, `InsertDoor`, `ToggleDoor`, `ToggleDoors`; introduce `DoorGroup`
- [hybrasyl/Objects/Door.cs](../hybrasyl/Objects/Door.cs) — back-reference to its group
- [hybrasyl/Objects/User.cs](../hybrasyl/Objects/User.cs) — `SendDoorUpdate`, initial-door-blast on map entry (only the actually-changing panels)

**Out:**
- Wire protocol changes
- Lua scripting integration for doors (locked/keyed/timed — future work)
- Chaos.Client changes (already done)
- Integration test harness (separate effort if Hybrasyl doesn't have one)

## Approach — `DoorGroup` abstraction

Replace the three `Sprites` dicts with a single `DoorDefinition[]` table ported from `doors.md` plus derived lookups:

```csharp
public sealed record DoorDefinition(
    ushort[] ClosedSprites,    // length = tile count; index order matches spatial order along axis
    ushort[] OpenSprites,      // same length; null if HasClosedVersion == false
    DoorAxis Axis,             // NorthSouth | EastWest
    bool OnlyCenterChanges,    // 3-tile only; side panels are static jamb art
    bool HasClosedVersion      // false = permanently-open archway
);
```

Derived at static init:
- `Dictionary<ushort, (DoorDefinition Def, int PanelIndex, bool IsOpenState)> SpriteLookup` — any sprite → its definition, panel position, and whether it's the closed or open side.

At map load, walk tiles. When a sprite matches `SpriteLookup`, check neighbors along the definition's axis for the remaining panels. If all expected panels are present and adjacent, instantiate a `DoorGroup` owning that set of `(x, y)` positions. Every tile in the group gets an entry in `Map.Doors` pointing at the shared group. Each `Door` object keeps its position but delegates state to its group.

`DoorGroup` owns:
- `Closed` (bool) — the single authoritative state
- `Panels` (list of `(x, y, closedSprite, openSprite, isCenter)`)
- `Definition` (the DoorDefinition)
- `UpdateCollision` (derived from `IsDoorCollision` over the definition)

`DoorGroup.Toggle(User invoker)`:
- Flips `Closed`.
- For each panel: if `OnlyCenterChanges && !isCenter`, skip. Else update collision (if the whole group carries collision), emit `SendDoorUpdate(x, y, Closed, IsLeftRight)` to nearby users.

`IsDoorCollision(sprite)` becomes a property on `DoorDefinition` — if any panel's closed-state sprite has the wall flag in the engine's collision table, the group is collision-toggling.

## Phases and review gates

### Phase 1 — Data rewrite

Port all 81 entries from `doors.md` into `Sprites.cs` as a `DoorDefinition[]` plus the derived `SpriteLookup`. Keep the existing `ClosedDoorSprites` / `OpenDoorSprites` / `DoorSprites` as thin shims over the new structure so call sites compile during the migration (the shims were later dropped in Phase 4 once all callers had switched over).

**Review gate:**
- *Bug/regression:* diff every entry against `doors.md` line-by-line; confirm the shim dicts produce identical behavior to the old hand-maintained dicts for every door in the previous tables (i.e., the migration doesn't break any door that already worked).
- *Architecture/design:* `DoorDefinition` is a record (immutable), table is `static readonly`, lookup is built once. No reflection, no runtime parsing.

### Phase 2 — `DoorGroup` + map-load connected-component

Introduce `DoorGroup`. Rewrite `LoadMapFile`'s door-scanning: for each matching tile sprite, check neighbors along the definition's axis for the rest of the panels. If the full panel set is present and adjacent, instantiate a group; else skip (malformed map — log and continue). Register every panel tile in `Map.Doors` with a back-reference.

Initial `Closed` state: if any panel's current sprite matches its definition's closed-state ID, the group is closed; else open. (Maps author doors in one state or the other; the sprite at load time is ground truth.)

**Review gate:**
- *Bug/regression:* a 1-tile door (e.g. `12484/12485`), a 2-tile (`1993,1994 / 1996,1997`), a 3-tile all-change (`2163-65 / 2167-69`), a 3-tile center-only (`3058-60 / 3066-68`), and a 4-tile (`14874-77 / 14904-07`) all register as a single group with correct `Closed` state and axis. Verified via `GameLog` at map load + manual walk in a test map.
- *Architecture/design:* no scanning fallbacks; if map data doesn't match `doors.md` we log and skip, never guess.

### Phase 3 — Toggle + collision rewrite

Replace `ToggleDoor`/`ToggleDoors` with `DoorGroup.Toggle()`. `Door.OnClick` → `Group.Toggle(invoker)`. The `±1` scan goes away. `IsDoorCollision` walks the group's panels.

For center-only groups: `Toggle` updates `Closed` on the group, updates collision (if the group carries collision) only for the center panel's tile, and emits `SendDoorUpdate` only for the center panel's `(x, y)`.

**Review gate:**
- *Bug/regression:* click any panel of a 3-tile all-change door → all 3 flip. Click side panel of a 3-tile center-only door → only center flips. Click a 4-tile door's end panel → all 4 flip. Close, walk through, open, all work.
- *Architecture/design:* `Door.OnClick` is a 1-line dispatch; state lives on the group. No duplication of state across tile-level `Door` objects.

### Phase 4 — Archways and collision edge cases

Register `HasClosedVersion == false` definitions in `SpriteLookup` so the server recognizes their sprites, but do NOT create `DoorGroup` objects for them — they're non-interactive. Remove them from `Map.Doors`. Verify `IsDoorCollision` returns a sensible answer (archways are walkable; collision comes from SOTP).

**Review gate:**
- *Bug/regression:* walk through an archway (sprite `4519`/`4520`/`4521`) — no block, no menu on Alt+right-click-near in the client (because the client's menu only shows doors with both states and archways have no closed state in `doors.md` so aren't in `DoorTable.cs`).
- *Architecture/design:* archways are data, not a special code path.

### Phase 5 — Initial door blast

`User.cs`'s map-entry door-update loop emits one `SendDoorUpdate` per panel for each `DoorGroup` in view (skipping side panels for center-only groups). Ensures client renders correct state on map entry without relying on retail's "initial state matches map sprite" assumption.

**Review gate:**
- *Bug/regression:* log in to a map with a mix of door types, confirm all visible doors render in their authored state. Alt+right-click menu labels match.
- *Architecture/design:* loop is a single pass over `Map.Doors.Values.Distinct()` (groups, not tiles).

### Final review

Full changeset review. Confirm:
- Every door in `doors.md` has been manually exercised at least once on a test map
- The protocol bytes on the wire are bit-identical to before for any door that used to work
- No `GameLog.Error` from unrecognized sprites on any current-production map at startup

## Verification

Manual integration tests against a Hybrasyl instance running this branch with the Chaos.Client on `feature/better-doors`-compatible build:

1. **1-tile door** — N/S, `12484/12485`: open, close, walk through, confirm collision flips with state.
2. **2-tile E/W door** — Mileth Inn, `1993,1994 / 1996,1997`: click either panel, both flip in one frame.
3. **3-tile all-change** — `2163,2164,2165 / 2167,2168,2169` (E/W): click any panel, all three flip.
4. **3-tile center-only** — `3058,3059,3060 / 3066,3067,3068` (E/W): click any panel, only 3059 ↔ 3067 flips visually.
5. **4-tile door** — `14874-77 / 14904-07` (E/W) and `14878-81 / 14908-11` (N/S): click any panel, all four flip.
6. **Reversed-pair door** — `3149,3150,3151 / 3157,3158,3159` (N/S center-only): visual matches closed/open state correctly on both client and server.
7. **Archway** — `4519,4520,4521` (E/W): walkable, no interaction menu, no `0x32` flows on click.
8. **Odd pair** — `12379 → 11448` and `12380 → 11445`: open/close sprite IDs from different numeric ranges — confirm both work.
9. **Regression sweep** — every door that worked on `develop` still works on this branch.

## Out of scope / follow-ups

- **Locked/keyed/timed/scripted doors** — require a richer `DoorGroup` model (state enum, Lua hooks, UI prompt for key entry). Defer.
- **Client-server door data sync** — currently `doors.md` is hand-mirrored into both `Chaos.Client/Definitions/DoorTable.cs` and Hybrasyl's `Sprites.cs`. A build-time codegen step would eliminate drift. Defer to a standalone effort once the Chaos.Client Godot migration or a shared-package strategy crystallizes.
- **Protocol modernization** — a generic `ObjectState` opcode carrying richer per-door state (locked, animated, keyed) rather than the binary `0x32 Door { closed, openRight }`. Tracked in [Chaos.Client/docs/doors-modernization-direction.md](../../Chaos.Client/docs/doors-modernization-direction.md).
