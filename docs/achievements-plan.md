# Achievements Subsystem — Plan

Add a structured achievement system that tracks player progress against named goals, persists per-character state, and surfaces unlocks to Chaos.Client. Independent of Legend, but able to grant a Legend mark on unlock when an achievement opts in.

## Context

The server has no formal achievement system today. Equivalent state is scattered:

- **Cookies** (ad-hoc string k/v on `Creature`, see [hybrasyl/Objects/Creature.cs](../hybrasyl/Objects/Creature.cs)) — used by Lua scripts to track quest progress, "have you met X" flags, etc. No schema, no progress aggregation.
- **`RecentKills`** ([hybrasyl/Objects/User.cs](../hybrasyl/Objects/User.cs) lines 51–55, 728–734) — a 25-item recent-kill list, populated from `Monster.OnDeath`. Useful for last-N display, useless for "kill 1000 of X" goals.
- **`QuestMetadata`** ([hybrasyl/Internals/Metafiles/QuestMetadata.cs](../hybrasyl/Internals/Metafiles/QuestMetadata.cs)) — catalog-style quest definitions with no per-user progress tracking; per-user state lives in cookies.
- **Legend** ([hybrasyl/Subsystems/Players/Legend.cs](../hybrasyl/Subsystems/Players/Legend.cs)) — fully featured display layer for character history (icon/color/text/prefix/timestamp/quantity), but marks are 100% programmatic with no catalog. Anyone with `LegendCommand` or Lua access can write any mark.

What the team actually wants: a designer-authored catalog of named goals with structured progress counters, persistent unlock state, automatic Legend mark generation on unlock for the marks that should be public, and a notification path to the client. Independent of Legend (an achievement does not require a Legend mark), in tandem with Legend (achievements are the cleanest way to grant marks consistently).

The choice of how to surface achievements to **Chaos.Client** is its own design fork — captured in detail below — because the client is mid-migration off the `Chaos.Networking` NuGet (per [`Chaos.Client/docs/chaos-networking-removal-direction.md`](../../Chaos.Client/docs/chaos-networking-removal-direction.md)) and the right answer depends on whether we wait, fork, or sidestep that constraint.

`Chaos.Client.sichii` is explicitly out of scope; it's the upstream Sichii baseline, not the Hybrasyl-targeted client.

## Target behavior

- A character can hold zero or more **Achievements**, each catalog-defined in XML.
- Each achievement has one or more **Counters** (e.g., `monsters.killed.beetle`, `gold.earned`, `maps.visited`); progress is tracked as integer counters per-character, persisted to Redis.
- An achievement unlocks when its completion criteria (one or more counter thresholds, possibly with combinator logic) are met. Unlock is one-shot per character.
- On unlock: optional Legend mark is created from the achievement's `<LegendMark>` template (achievements without a `<LegendMark>` element unlock silently in Legend terms — they still record the unlock and notify the client).
- Per-character state is `{ unlocked: Set<id>, progress: Dict<counter_id, long> }`.
- Lua scripts and built-in event hooks drive counter increments. The achievement subsystem re-evaluates affected achievements on each increment and unlocks any whose criteria are now met.
- Hot reload (`/reloadachievements`) rebuilds the catalog and re-evaluates online players against new criteria — characters who already met newly-added criteria unlock immediately.
- Client receives unlock notifications via the chosen communication channel (see [Client communication](#client-communication-options)).

## Scope

**In:**
- New `Achievement` content type added to the `Hybrasyl.Xml` NuGet package; loaded via the existing `WorldState.Values<T>()` pattern in [hybrasyl/WorldStateStore.cs](../hybrasyl/WorldStateStore.cs).
- New subsystem under `hybrasyl/Subsystems/Achievements/`: `AchievementProgress` (per-character POCO), `IAchievementCriterion`, `CounterCriterion`, `AchievementManager`.
- Edits to [hybrasyl/Objects/Creature.cs](../hybrasyl/Objects/Creature.cs) — persisted `AchievementProgress` field alongside `Cookies` and the new `Traits` dict.
- Edits to [hybrasyl/Objects/User.cs](../hybrasyl/Objects/User.cs) — public `IncrementCounter/SetCounter/HasAchievement/GrantAchievement/ListAchievements` wrappers.
- Built-in counter hooks at the natural event points: `Monster.OnDeath` ([Monster.cs:358](../hybrasyl/Objects/Monster.cs#L358)), `User.GiveExperience` ([User.cs:818](../hybrasyl/Objects/User.cs#L818)), `User.SendEquipItem` ([User.cs:1515](../hybrasyl/Objects/User.cs#L1515)), and `Map` entry. Each hook calls `AchievementManager.OnEvent(...)` with a typed event payload.
- Lua API in [hybrasyl/Subsystems/Scripting/HybrasylUser.cs](../hybrasyl/Subsystems/Scripting/HybrasylUser.cs) — `IncrementCounter`, `SetCounter`, `GetCounter`, `HasAchievement`, `GrantAchievement` (admin-flavored manual unlock).
- Admin chat commands under `hybrasyl/Subsystems/Messaging/ChatCommands/` — `/grantachievement`, `/revokeachievement`, `/listachievements`, `/setcounter`, `/reloadachievements`.
- Legend integration: when an achievement unlocks and has a `<LegendMark>` element, synthesize the mark via existing `Legend.AddMark` API.
- Client communication: **one** option from the menu below, selected by the team.
- Tests in `Hybrasyl.Tests/Achievements.cs`.

**Out:**
- General-purpose event bus refactor. We add explicit hook calls at the named integration points; we do NOT redesign the server's eventing model.
- Achievement panel UI in Chaos.Client — flagged as a follow-up; the client-side work is its own scoping effort.
- Retroactive backfill from cookies / `RecentKills` into achievement counters. Existing characters start at zero; counters accumulate going forward.
- Achievement-driven traits / rewards beyond Legend marks. (Forward-compatible: an `<OnUnlock>` element could later grant a trait, item, gold, etc. — not in MVP.)
- Cross-character / account-wide achievements. MVP is per-character; account-level achievements are blocked behind the Account-manager work flagged in [memory/account_manager_scoping.md](#).

## Approach

### Data model

A `Achievement` (catalog entry, defined in XML, immutable, shared) declares its id, display metadata, optional Legend mark template, and one or more `Criterion` entries. An `AchievementProgress` (per-character record, persisted in Redis) is a flat structure carrying the unlocked-id set and the counter dictionary.

```csharp
public sealed class AchievementProgress
{
    public HashSet<string> Unlocked { get; set; } = new();
    public Dictionary<string, long> Counters { get; set; } = new();
}
```

That's the entire persisted state. Re-evaluation is a function `(progress, catalog) -> newly_unlocked_ids` called whenever a counter changes.

### Counter and criterion model

Counters are namespaced strings: `monsters.killed.<template_id>`, `monsters.killed.any`, `gold.earned.lifetime`, `maps.visited.<map_id>`, `xp.gained.lifetime`, `items.equipped.<weapon_type>`. Namespacing is convention, not enforced — the catalog references whatever counter ids it likes; the manager just looks them up.

```csharp
public interface IAchievementCriterion
{
    bool IsMet(AchievementProgress progress);
}

public sealed class CounterCriterion : IAchievementCriterion
{
    public string CounterId { get; init; }
    public long Threshold { get; init; }   // progress[CounterId] >= Threshold

    public bool IsMet(AchievementProgress progress) =>
        progress.Counters.GetValueOrDefault(CounterId) >= Threshold;
}
```

An `Achievement` holds `IReadOnlyList<IAchievementCriterion> Criteria` plus a `CriteriaCombinator` enum (`All` / `Any`). MVP supports both. Future criterion kinds (e.g., a `LegendPrefixCriterion` checking for a Legend mark prefix, or a `TraitCriterion` checking trait presence) plug in without touching the manager.

### XML schema

New `Achievement` root type added to the `Hybrasyl.Xml` NuGet:

```xml
<Achievement Id="first.blood" Name="First Blood" Category="Combat">
  <Description>Defeat your first enemy.</Description>
  <Hidden>false</Hidden>                       <!-- if true, client doesn't see it until unlocked -->
  <Combinator>All</Combinator>                  <!-- All | Any -->
  <Criteria>
    <Counter Id="monsters.killed.any" Threshold="1" />
  </Criteria>
  <LegendMark>                                   <!-- optional -->
    <Icon>Yay</Icon>
    <Color>Yellow</Color>
    <Prefix>achv-first-blood</Prefix>
    <Text>Drew first blood</Text>
    <Public>true</Public>
  </LegendMark>
</Achievement>
```

Three proof-of-concept achievements ship with the MVP:
1. `first.blood` — combat starter, demonstrates `monsters.killed.any` counter and Legend mark synthesis.
2. `mileth.regular` — visits Mileth 100 times, demonstrates a `maps.visited.<id>` counter without a Legend mark (silent unlock).
3. `dual.discipline` — demonstrates `Combinator="All"` across two counters (e.g., reach level 50 AND deal 1M damage).

### Trigger / event integration

Rather than introducing a server-wide event bus, the achievement subsystem exposes a small typed API that the existing event sites call directly:

```csharp
public static class AchievementManager
{
    public static void OnMonsterKilled(User killer, Monster victim);
    public static void OnExperienceGained(User user, uint amount);
    public static void OnItemEquipped(User user, ItemObject item);
    public static void OnMapEntered(User user, MapObject map);
    public static void OnGoldEarned(User user, long amount);
    // … add as needed
}
```

Each method increments the relevant counters and re-evaluates achievements that reference those counter ids. Implementation is a single `IncrementCounter(user, id, delta)` per relevant counter, then the manager looks up which achievements depend on that counter id (precomputed reverse index from the catalog) and checks each one's criteria.

Integration sites:
- [Monster.cs:416](../hybrasyl/Objects/Monster.cs#L416) — alongside `hitter.TrackKill(...)` already there, add `AchievementManager.OnMonsterKilled(hitter, this);`.
- [User.cs:818](../hybrasyl/Objects/User.cs#L818) — in `GiveExperience`, call `AchievementManager.OnExperienceGained(this, exp);`.
- [User.cs:1515](../hybrasyl/Objects/User.cs#L1515) — in `SendEquipItem`, call `AchievementManager.OnItemEquipped(this, item);`.
- Map-entry path in `User.cs` — call `AchievementManager.OnMapEntered(this, newMap);`.

Lua scripts can also drive progress directly via `user:IncrementCounter("orphan.helped", 1)` for arbitrary custom counters — useful for quest-style achievements that don't map cleanly to a built-in event.

### Legend integration

Achievements are independent of Legend by default — unlocking an achievement does not automatically create a Legend mark. An achievement opts into Legend integration by including a `<LegendMark>` child in its XML. On unlock:

1. Manager checks for `<LegendMark>` element.
2. If present, calls `user.Legend.AddMark(...)` with the icon/color/text/prefix/Public values from the template. `Prefix` defaults to `achv-<id>` if not specified, ensuring uniqueness for `RemoveMark`.
3. If absent, unlock proceeds without touching Legend.

The reverse direction (Legend mark → achievement) is **not** supported. Legend marks remain freely creatable by scripts, admin commands, and game logic; achievements are catalog-only. If a designer wants a Legend mark gated by achievement unlock, they put the mark in the achievement's `<LegendMark>` element.

### Client communication options

This is the section the team needs to decide on before implementation. Achievements need **three data flows** to the client:

1. **Unlock event push** — "you just unlocked First Blood." Real-time, low latency, must reach the client without a poll.
2. **Achievement list / progress sync** — "show me my achievement panel." Bulk, request/response, can tolerate small latency.
3. **Catalog metadata** — "what achievements exist, and what are their display names." Static-ish, can be cached client-side.

Four implementation shapes to consider:

#### Option A — New packet opcode (e.g., `0xAC AchievementUnlocked`, `0xAD AchievementListPush`)

Define new opcodes in `Hybrasyl.Internals.Enums.OpCodes` and corresponding `ServerPacket` builders. Client registers handlers in `ConnectionManager.IndexHandlers()`, fires an event consumed by `WorldScreen.Wiring.cs`, updates `WorldState`.

- **Pros:** Native to existing architecture. Real-time push for unlocks. Bulk sync via a request/response opcode pair. Zero new infrastructure. Lowest latency.
- **Cons:** Per [`Chaos.Client/docs/chaos-networking-removal-direction.md`](../../Chaos.Client/docs/chaos-networking-removal-direction.md), the `Chaos.Networking` NuGet currently gates new opcode definitions. Two paths around this:
  - Wait for the planned Chaos.Networking decoupling (~2500 LOC migration on the client side, in progress).
  - Define the opcodes locally (Hybrasyl-specific extension over the legacy protocol space, with a Chaos.Client fork that defines matching deserializers). Achievements is named in the doc as a candidate use case for this pattern — could be the pilot.
- **Backwards compatibility:** Older clients (chaos.client.sichii or any retail-ish client) silently drop the unknown opcode bytes. Achievement unlocks still happen server-side and persist; only the client-side notification is lost. Acceptable.

#### Option B — Ride existing opcodes (SystemMessage + PlaySound + Legend)

No new opcodes. On unlock, the server emits a `SystemMessage` (`0x0A`) with text like `"Achievement Unlocked: First Blood"`, plays a sound (`0x19`), and (if the achievement has a `<LegendMark>`) the new mark appears in the player's Legend on next profile refresh.

- **Pros:** Zero protocol changes. Works against the current Chaos.Client and any retail-faithful client today. Trivial to ship.
- **Cons:** No structured payload — client cannot show an achievement panel, distinguish "unlocked" vs "in progress," or render achievement art. No bulk sync mechanism for the panel use case. List view requires a side channel anyway.

#### Option C — Extend the existing gRPC `Patron` server

[hybrasyl/grpc/PatronServer.cs](../hybrasyl/grpc/PatronServer.cs) is the existing gRPC service (admin/auth ops via `Patron.proto`). Add a new gRPC service (e.g., `Achievements.proto`) with `ListAchievements`, `GetProgress`, and a server-streaming `SubscribeUnlocks` endpoint. Client opens a parallel gRPC connection alongside the legacy game socket.

- **Pros:** Structured data, schema-versioned via protobuf, idiomatic for the panel + progress use cases. Server-streaming gives real-time push for unlocks. Cleanly bypasses the `Chaos.Networking` NuGet constraint.
- **Cons:** Adds a parallel connection — auth/session linkage is non-trivial (today, `PatronServer.Auth` is a one-shot login op, not session-bound). Chaos.Client would gain a second runtime dependency (Grpc.Net.Client + protobuf-generated types). Real-time push works only while the gRPC stream is connected; a reconnect story is required.

#### Option D — Hybrid (recommended): Option B for now, Option A as the v2

Ship Option B as the MVP — it works against the current client, requires no protocol coordination, and proves the server-side machinery. Reserve Option A for the v2 when either (a) the Chaos.Networking decoupling lands on Chaos.Client, making local Hybrasyl opcode definitions clean, or (b) the team wants to pilot the local-opcode pattern early. The server-side achievement subsystem is independent of the wire format; switching from Option B to Option A is purely a notification-emitter change plus a client handler — no migration of stored state.

- **Pros:** Ships fast. Proves the catalog, persistence, criterion engine, and Legend integration end-to-end against a real client. Doesn't gate on client refactor work. Forward-compatible — Option A is purely additive when the team is ready.
- **Cons:** No achievement panel in the MVP — unlocks are visible only as system messages and (when configured) Legend marks. Catalog discovery requires the player to consult external docs or a Lua-driven NPC dialog ("Show me available achievements").

**Recommendation:** Option D. Re-examine when the Chaos.Networking decoupling is closer.

### Lifecycle

**On login** (slotted into [hybrasyl/Servers/World.cs](../hybrasyl/Servers/World.cs) login sequence after Traits apply, before `UpdateAttributes(Full)`): nothing visible happens. `AchievementProgress` deserializes from Redis; counters are already current; unlocked set is current. No re-evaluation pass needed because counter changes always re-evaluate at the time of change.

**On counter increment** (`IncrementCounter(user, id, delta)`):
1. `progress.Counters[id] = progress.Counters.GetValueOrDefault(id) + delta;`
2. Look up affected achievements via the precomputed `Dictionary<string, List<Achievement>>` reverse index (counter id → achievements that reference it).
3. For each affected achievement not already in `Unlocked`, evaluate criteria. If met, call `Unlock(user, achievement)`.

**On `Unlock(user, achievement)`:**
1. Add id to `progress.Unlocked`.
2. If `<LegendMark>` present, call `user.Legend.AddMark(...)`.
3. Emit client notification per chosen Option (A: new opcode; B: SystemMessage + PlaySound; C: gRPC stream message; D: B today, A later).
4. Persist (Redis save is automatic via Newtonsoft on next save tick; consider an explicit save here for unlock durability if the save tick is slow — TBD during implementation).

**On `/reloadachievements`:**
1. Rebuild catalog from XML.
2. Rebuild reverse index.
3. For each online user, run a single re-evaluation pass over all not-yet-unlocked achievements (criteria are pure functions of progress, so this is cheap). New criteria that the user already meets fire `Unlock` immediately.

### Lua API

```lua
user:IncrementCounter("orphan.helped", 1)
user:SetCounter("custom.flag", 0)              -- absolute set, useful for quest reset
local n = user:GetCounter("monsters.killed.any")
local has = user:HasAchievement("first.blood")
user:GrantAchievement("debug.tester")          -- manual unlock, bypasses criteria; logs source
local ids = user:ListAchievements()            -- table of unlocked ids
```

`GrantAchievement` is intentionally separate from automatic unlock — it bypasses criteria evaluation, logs the manual source, and is the right tool for testing or admin-driven unlock. Counters set via `SetCounter` go through the same re-evaluation as `IncrementCounter`.

### Admin commands

Mirrors the existing cookie/trait command pattern (`SetCookieCommand.cs` etc.):

- `/grantachievement <player> <id>` — manual unlock with admin source attribution.
- `/revokeachievement <player> <id>` — for testing; removes from unlocked set, does NOT reset counters and does NOT remove the synthesized Legend mark (use `LegendCommand` for that explicitly, by design — Legend marks may be earned other ways).
- `/listachievements <player>` — prints unlocked + in-progress counter snapshot.
- `/setcounter <player> <counter_id> <value>` — for testing; triggers re-evaluation.
- `/reloadachievements` — rebuild catalog + re-evaluate online users.

## Critical invariants

1. **`AchievementProgress.Unlocked` is append-only in normal operation.** Auto-unlock never removes ids. `/revokeachievement` is the only path that removes; it's an admin tool, not a feature.
2. **Counter increments are the only path that triggers auto-unlock.** No background scan job, no login-time re-evaluation. This keeps the system O(1) per increment (with the reverse index) instead of O(catalog × characters).
3. **The reverse index is rebuilt only on catalog load and `/reloadachievements`.** It's a `Dictionary<counter_id, List<Achievement>>` where each achievement appears once per counter it references.
4. **Legend mark synthesis is one-shot.** Repeat unlocks (which shouldn't happen — see invariant 1) would call `Legend.AddMark` again; the existing `prefix`-uniqueness check in `Legend.AddMark` is the safety net but should never fire.
5. **`Hidden` achievements are NOT sent in any catalog-listing data flow before unlock.** Whatever Option ships, the listing path filters out unranked hidden achievements — the unlock notification is the player's first signal.

## Migration

Existing characters in Redis don't have an `AchievementProgress` field. Newtonsoft handles the missing field by leaving the property at its default. Combined with `[OnDeserialized]` defensive init in `Creature` (mirroring the `Cookies` and forthcoming `Traits` init paths), existing characters deserialize cleanly with empty progress on first load. No migration script.

Counter backfill from cookies/`RecentKills` is **not** in MVP — counters start at zero for everyone. If the team later wants "pre-existing kill counts retroactively count toward `monsters.killed.any`," that's a separate one-shot script (read `RecentKills` lifetime metric if available, or query Redis for cookie values, populate counters).

## Forward compatibility

- **New criterion kinds** (LegendPrefix, Trait, Class, Level): new `IAchievementCriterion` implementations, registered in the XML loader. No core changes.
- **Reward types beyond Legend marks** (grant trait, grant item, gold, XP): add an `<OnUnlock>` element to the XML schema, dispatched in `Unlock(user, achievement)`. Pluggable by reward type.
- **Account-wide / cross-character achievements**: blocked on the Account-manager work. When the Account entity exists, `AchievementProgress` (or a parallel `AccountAchievementProgress`) lives there with the same shape.
- **Server-streaming gRPC** (Option C / D-v2): the achievement subsystem already separates "compute unlock" from "emit notification." Switching the emitter is a single-file change.
- **Achievement panel UI in Chaos.Client**: separate scoping effort once the server side is shipping unlocks. Will need a structured catalog/progress query path — likely the trigger to revisit Option A or C.

## What we'd communicate to Chaos.Client

Per the user's specific ask, this section enumerates the data the achievement subsystem would push or expose to Chaos.Client (the Hybrasyl-targeted client at `e:\Dark Ages Dev\Repos\Chaos.Client`). Concrete payloads depend on the chosen communication option above; the data shape is option-agnostic.

**Push (server → client, real-time):**
- Achievement-unlocked event: `{ id, name, description, icon_ref, category, timestamp, legend_mark_synthesized: bool }`. Triggered immediately on unlock. Client uses to display a toast/notification and update its in-memory state.
- Counter-progress notification (optional — only for achievements with progress display enabled): `{ counter_id, new_value, achievement_ids_affected }`. Throttled or debounced server-side; not every monster kill should generate a wire message.

**Pull / sync (client → server → client, request-response):**
- Catalog listing: `{ achievements: [{ id, name, description, icon_ref, category, hidden, total_criteria, criteria_visibility }] }`. Filtered to exclude `Hidden=true` achievements the player hasn't unlocked. Cacheable client-side until catalog reload.
- Progress snapshot: `{ unlocked: [id, ...], counters: { id: value, ... } }`. Sent on demand when the client opens the achievement panel.

**Existing infrastructure these can ride on:**
- Option B (recommended for MVP): `SystemMessage` (0x0A) for the unlock toast text; `PlaySound` (0x19) for the sound cue; Legend mark in the next profile refresh for marks-enabled achievements.
- Option A (v2): new opcodes in the Hybrasyl-extension space, with matching deserializers in a Chaos.Client fork or post-Chaos.Networking-decoupling registration.
- Option C (alternative v2): new `Achievements.proto` gRPC service alongside the existing `Patron.proto`.

**Client-side surfaces that would need to exist:**
- Notification/toast renderer for the unlock event. Today, `OkPopupMessageControl` is the closest primitive but is modal — a non-modal achievement toast widget is new client work.
- Achievement panel (`PrefabPanel` subclass) for the catalog + progress views. New asset prefab + new screen wiring.
- ViewModel additions in `WorldState` for `Achievements`, `AchievementProgress`. Mirror of the existing `SkillBook` / `SpellBook` pattern.

These client-side surfaces are out of scope for the server work but flagged here so the Chaos.Client team has the wire-data contract to plan against.

## Verification

1. **Build:** Server compiles after the Hybrasyl.Xml NuGet bump introducing the `Achievement` root type.
2. **Catalog load:** Server starts, log shows N achievements loaded. Reverse index built; log a debug summary of counter id → achievement mapping.
3. **Counter increment:** Kill a monster on a test map. Inspect `/listachievements <name>` — `monsters.killed.<id>` and `monsters.killed.any` both incremented.
4. **Auto-unlock + Legend:** With `first.blood` defined as `monsters.killed.any >= 1`, kill the first monster. Player unlocks `first.blood`, Legend gains the `achv-first-blood` mark, client receives a SystemMessage (Option B) confirming.
5. **Silent unlock (no Legend mark):** With `mileth.regular` defined as `maps.visited.mileth >= 100` and no `<LegendMark>` element, simulate 100 visits via `/setcounter`. Achievement unlocks; Legend unchanged; client receives notification.
6. **Combinator=All:** With `dual.discipline` requiring both criteria, increment one counter to threshold — no unlock. Increment the second — unlock fires.
7. **Combinator=Any:** Reverse the above with `Combinator="Any"` — first criterion alone unlocks.
8. **Idempotent unlock:** Kill a second monster. `monsters.killed.any` continues to climb; `first.blood` stays unlocked exactly once; no second SystemMessage, no second Legend mark.
9. **Hot reload:** Add a new achievement `gold.hoarder` requiring `gold.earned.lifetime >= 100000` to the XML. A test character already has 200k earned. Run `/reloadachievements`. Test character unlocks `gold.hoarder` immediately.
10. **Stale id:** Manually inject an unknown achievement id into a player's `Unlocked` set in Redis. Log in. Server logs a warning, drops the entry (matches the Traits stale-id pattern), no errors.
11. **Lua API end-to-end:** A test NPC dialog calls `user:IncrementCounter("orphan.helped", 1)` 5 times. With `village.helper` requiring threshold 5, fifth call unlocks.
12. **Tests:** `dotnet test --filter Achievements` passes.

## Out of scope / follow-ups

- **Chaos.Client achievement panel UI.** Requires the wire-data contract (this doc) plus the panel/toast prefabs in the client. Scoped separately when the server side ships.
- **Option A / Option C client-channel work.** Tied to the Chaos.Networking decoupling timeline and / or the team's appetite to extend the gRPC service. Server is forward-compatible regardless.
- **Account-wide achievements.** Blocked on the Account-manager scoping memo.
- **Cross-character achievements** (e.g., "have any character on the account reach level 99"). Same blocker.
- **Reward types beyond Legend marks** (grant trait, grant item, gold/xp). Add an `<OnUnlock>` element to the schema and dispatch by reward type.
- **Counter backfill from cookies / `RecentKills`.** Optional one-shot script; not part of the MVP.
- **Progress-bar push** for in-progress achievements visible to the client. Today the panel would only know unlocked vs not; partial progress display is a follow-up once the panel exists.
- **Achievement-driven dialog gating** ("only show this NPC option to players with achievement X"). Trivial extension via Lua `user:HasAchievement(...)` — already available in MVP. Documented for content authors.
