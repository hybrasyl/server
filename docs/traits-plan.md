# Traits Subsystem — Plan

Add a per-character collection of named, persistent, scriptable modifiers that compose cleanly with Equipment and Statuses. Cookies stay narrative state; Traits own mechanics.

## Context

Three concrete use cases triggered this:

1. **Specialization balance tax** — support specs (cookie-driven, since `User.Class` itself doesn't change) take a -25% outgoing damage penalty in exchange for healing throughput. Applied/removed by the same scripts that flip the spec cookie.
2. **Tuning aura** — admin/NPC-applied global ± output knob, persists until removed. A balance dial separate from spec mechanics.
3. **Background** — optional character-creation-time choice (e.g., dockworker → +3 baseStr). Mostly permanent.

The first instinct was "set a cookie that bumps `BaseStr` by 3." That hides a real footgun: `BaseStr` is persisted directly to Redis (`[JsonProperty]` on [hybrasyl/Objects/StatInfo.cs](../hybrasyl/Objects/StatInfo.cs)). Any code path that re-applies the bump silently double-stacks, and the bonus has no source attribution — a support engineer staring at "this character has -25% outgoing damage" has nothing to grep for. Cookies were designed as state flags, not as drivers of character power.

The alternative is a **Traits subsystem**: a per-character collection of named, persistent modifier instances backed by an XML-defined catalog, applied through the existing `Stats.Apply()` pipeline. This composes naturally with how Equipment and Statuses already contribute to `Bonus*` fields, gives every modifier a source identifier, and stays idempotent under hot reload.

The MVP is intentionally narrow: **one effect type (StatModifier), three concrete proof-of-concept traits, full Lua and admin API.** The design accommodates future effect kinds (weapon-skill grants, castable grants, zone access grants, species mechanics) without restructuring.

## Target behavior

- A character can carry zero or more named **Traits**, each producing one or more **Effects**.
- Effects of type `StatModifier` add deltas (positive or negative) to any `Bonus*` field on `StatInfo`, including the percent-style ones (`BonusOutboundDamageModifier` etc.) which the existing damage/heal pipeline in [hybrasyl/Subsystems/Formulas/NumberCruncher.cs](../hybrasyl/Subsystems/Formulas/NumberCruncher.cs) consumes multiplicatively at formula time.
- Trait state persists across logins via Redis (alongside `Cookies`).
- Trait deltas re-baseline automatically when the XML catalog is reloaded — values change for every online holder, no relog needed.
- A trait carries source attribution (`spec:priest`, `admin:tacolejr`, `background:dockworker`) so admins can debug "why does this character have X."
- Lua scripts and admin commands manage traits via a small mirror of the existing cookie API.
- Adding the same trait twice is a no-op. Categorical conflicts (e.g., two opposing tuning auras) are resolved per-trait policy: replace or reject.
- Adding a `StatModifier` trait sends one `UpdateAttributes(Full)` so the client's stat sheet reflects it immediately.

## Scope

**In:**
- Add `Trait` schema to the `Hybrasyl.Xml` NuGet package (new XML root type, reuses existing `StatModifiers` element)
- New subsystem under `hybrasyl/Subsystems/Traits/`: `TraitInstance`, `ITraitEffect`, `StatModifierEffect`, `TraitManager`
- Edits to [hybrasyl/Objects/Creature.cs](../hybrasyl/Objects/Creature.cs) — persisted `Traits` dictionary alongside `Cookies`
- Edits to [hybrasyl/Objects/User.cs](../hybrasyl/Objects/User.cs) — public `AddTrait/RemoveTrait/HasTrait/ListTraits` wrappers, `ApplyAllTraits()` for login
- Edits to [hybrasyl/Servers/World.cs](../hybrasyl/Servers/World.cs) — slot trait apply into login sequence; register catalog during world data load
- Edits to [hybrasyl/Subsystems/Scripting/HybrasylUser.cs](../hybrasyl/Subsystems/Scripting/HybrasylUser.cs) — Lua-facing methods mirroring the cookie API
- Four new chat commands under `hybrasyl/Subsystems/Messaging/ChatCommands/` mirroring the cookie command pattern
- New test file `Hybrasyl.Tests/Traits.cs` covering the contract
- Three proof-of-concept trait XMLs under `world/xml/traits/`

**Out:**
- `EquipmentGrantEffect` (weapon-skill gating, prestige gear access) — designed-for, not built. Future iteration.
- `CastableGrantEffect` (trait-unlocked spells/skills) — future iteration.
- `AccessGrantEffect` (warp/zone gating, dialog gating) — future iteration.
- Background-selection UI in character creation — backgrounds-as-traits work, but [hybrasyl/Servers/Login.cs](../hybrasyl/Servers/Login.cs) currently hardcodes everything in `PacketHandler_0x04_CreateB`. UX work is a separate piece.
- Pre-existing bug at [NumberCruncher.cs:218](../hybrasyl/Subsystems/Formulas/NumberCruncher.cs#L218) reading `OutboundHealModifier` where the status heal path should read `InboundHealModifier` — flagged for separate fix, not part of this plan.

## Approach

### Data model

A `Trait` (catalog entry, defined in XML) is immutable and shared across all characters that hold it. A `TraitInstance` (per-character record, persisted in Redis) carries the runtime state for one application of a trait on one character.

```csharp
public sealed class TraitInstance
{
    public string Id { get; set; }            // matches Trait.Id in catalog
    public DateTime AppliedAt { get; set; }
    public string Source { get; set; }        // "spec:priest", "admin:tacolejr", etc.

    [JsonIgnore]                              // recomputed from current XML on login
    public StatInfo Delta { get; set; }
}
```

**Why `Delta` is `[JsonIgnore]`:** XML edits to a trait's stat values must propagate to existing characters automatically. Persisting the cached delta would freeze characters at whatever values were live when the trait was first added. Recomputing on login (and on hot reload) keeps everyone current without a migration step.

**Why we cache `Delta` at all** (rather than recomputing on Remove): `StatInfo.Apply`/`Remove` is a pure +/- delta accumulator with no attribution. If `Remove` had to re-evaluate the trait's current XML values, a hot reload that changed the values would silently leave residual stat deltas. Caching the exact delta that was applied is the single source of truth for what to subtract.

### Effect abstraction

```csharp
public interface ITraitEffect
{
    void Apply(User user, TraitInstance instance);
    void Remove(User user, TraitInstance instance);
}

public sealed class StatModifierEffect : ITraitEffect
{
    // Reads Trait.StatModifiers (an existing Hybrasyl.Xml element type),
    // calls NumberCruncher.CalculateStatusModifiers to evaluate any formulas,
    // caches the resulting StatInfo on instance.Delta,
    // calls user.Stats.Apply(instance.Delta).
}
```

A `Trait` holds an ordered `IReadOnlyList<ITraitEffect> Effects`. The MVP loader produces a single `StatModifierEffect` per trait. Future effect kinds slot in alongside without touching `TraitInstance` or `TraitManager`.

### XML schema

New root type added to the `Hybrasyl.Xml` package (option (a) per discussion — schema lives in the NuGet, loaded via `WorldData.Values<Trait>()` like every other content type):

```xml
<Trait Id="spec.priest.tax" Name="Priest Specialization Balance" Category="SpecBalance">
  <Description>Support specialization deals 25% less damage in exchange for healing throughput.</Description>
  <Exclusive>SpecBalance</Exclusive>            <!-- only one trait per category at a time -->
  <ExclusivePolicy>Replace</ExclusivePolicy>     <!-- Replace | Reject; Replace = swap on add -->
  <ConflictsWith>
    <Trait>some.other.id</Trait>                 <!-- explicit non-categorical conflicts -->
  </ConflictsWith>
  <StatModifiers>                                <!-- reuses existing Hybrasyl.Xml type -->
    <BonusOutboundDamageModifier>-0.25</BonusOutboundDamageModifier>
  </StatModifiers>
</Trait>
```

Reusing the existing `StatModifiers` element is the key economy: `NumberCruncher.CalculateStatusModifiers` already evaluates formulas inside that element, so traits inherit formula support for free. Anything a status can express in `StatModifiers`, a trait can express identically.

Three proof-of-concept traits ship with the MVP:
- `spec.priest.tax` — class-balance tax (one per support spec; expand as specs are defined)
- `aura.tuning.boost.10` and `aura.tuning.tax.10` — example tuning auras with `Exclusive="TuningAura"`
- `background.dockworker` — proof of background usage (catalog entry only; ships disabled until creation UX exists)

### Spec/prestige integration

Class doesn't change at runtime — specialization and prestige are cookie-driven. So there is **no structural hook** on `User.Class`; the integration is a script contract:

> Any Lua script that flips a spec or prestige cookie also calls `user:AddTrait(...)` (or `RemoveTrait(...)`) for the corresponding balance trait.

Concretely, the script that grants Priest specialization writes the spec cookie and calls `user:AddTrait("spec.priest.tax")`. The script that switches a player from Priest to a different support spec calls `user:RemoveTrait("spec.priest.tax")` and `user:AddTrait("spec.<new>.tax")` — or relies on `Exclusive="SpecBalance"` to auto-replace via the second AddTrait. (Replace policy is the recommended default for this category.)

This keeps the trait subsystem unaware of the spec system and vice versa. If a structured `SetSpecialization()` API is built later, it can wrap `AddTrait/RemoveTrait` internally without touching the trait code.

### Lifecycle

**On login** (slotted into [hybrasyl/Servers/World.cs](../hybrasyl/Servers/World.cs) login sequence, after `RecalculateBonuses()` and before `UpdateAttributes(StatUpdateFlags.Full)`):

1. For each `TraitInstance` in the player's `Traits` dictionary, look up the corresponding `Trait` in the catalog.
2. If missing (deleted from XML since last save), log a warning and drop the instance silently. No ghosts.
3. For each `ITraitEffect` on the trait, call `effect.Apply(user, instance)` — `StatModifierEffect` recomputes the delta from current XML, caches it on the instance, and calls `Stats.Apply(delta)`.
4. The first stat packet to the client (sent by the subsequent `UpdateAttributes(Full)`) reflects all trait deltas.

**On runtime AddTrait** (via Lua, admin command, or auto-call from a spec script):

1. Resolve trait id in catalog. If unknown, return false.
2. Check `ConflictsWith` and `Exclusive` category. On conflict: if policy is `Replace`, remove the conflicting instance first; if `Reject`, return false.
3. If the same id is already present, return false (idempotent).
4. Create a `TraitInstance`, call each effect's `Apply`, store in the dict, send `UpdateAttributes(Full)`, return true.

**On runtime RemoveTrait:**

1. Look up the instance; if absent, return false.
2. For each effect, call `effect.Remove(user, instance)` — `StatModifierEffect` calls `Stats.Remove(instance.Delta)` using the cached delta.
3. Drop the instance from the dict, send `UpdateAttributes(Full)`, return true.

**On `/reloadtraits` (XML hot reload):**

1. Rebuild the catalog from XML.
2. For each online user, walk their `Traits`: for each instance, call effect.Remove (using the OLD cached delta), recompute the delta from new XML, cache it, call effect.Apply.
3. Send `UpdateAttributes(Full)` once per user.

Offline users get fresh deltas automatically on next login because deltas are `[JsonIgnore]`.

### Lua API

Mirrors the cookie API exactly (see [hybrasyl/Subsystems/Scripting/HybrasylUser.cs](../hybrasyl/Subsystems/Scripting/HybrasylUser.cs) `SetCookie` block):

```lua
local ok = user:AddTrait("spec.priest.tax")     -- false if unknown id, conflict-reject, or already present
user:RemoveTrait("spec.priest.tax")             -- false if not present
local has = user:HasTrait("spec.priest.tax")    -- bool
local ids = user:ListTraits()                   -- table (array) of trait id strings
```

`TraitInstance` is intentionally NOT exposed to Lua in MVP — that would require a `UserData.RegisterType<TraitInstance>()` and a stable Lua-facing schema we'd regret. Returning ids only keeps the surface minimal; a `GetTrait(id) -> table` getter for metadata can be added later if scripters need it.

### Admin commands

Mirrors the cookie commands (see `SetCookieCommand.cs` pattern):

- `/addtrait <player> <traitid>` — calls `AddTrait`, reports the boolean result + any conflict details
- `/removetrait <player> <traitid>` — calls `RemoveTrait`
- `/listtraits <player>` — prints the player's current trait ids with sources
- `/reloadtraits` — rebuilds the catalog from XML, reconciles every online user. Killer feature for content authoring; without it, every XML edit means a server restart.

## Critical invariants

These are load-bearing — violating any of them breaks the design:

1. **`TraitInstance.Delta` is the single source of truth for removal.** Never recompute from current XML when calling `Stats.Remove()`. Otherwise hot reload that changes a trait's values silently leaves residual stat deltas on every online holder.
2. **`Delta` is `[JsonIgnore]` and rebuilt on login.** Required for XML edits to propagate to existing characters without a migration step.
3. **`AddTrait` is idempotent.** Dictionary keyed by id; second add of the same id is a no-op returning false. No internal counters, no accidental double-stacking.
4. **Apply order on login:** Equipment first, then Traits, then `UpdateAttributes(Full)`. The first stat packet must reflect everything.
5. **Stale trait ids drop silently.** If an id in the player's dict no longer exists in the catalog, log a warning and remove it. Don't keep ghosts forever.

## Migration

Existing characters in Redis don't have a `Traits` field. Newtonsoft handles missing fields by leaving the property at default. Combined with an `[OnDeserialized]` defensive init in `Creature` (mirroring how `Cookies` initialization is handled), existing characters deserialize cleanly with an empty trait dict on first load. No migration script required, no version flag, no special-case handling.

## Forward compatibility

The `ITraitEffect` abstraction lets future effect kinds slot in without touching `Trait`/`TraitInstance`/`TraitManager`:

- **`EquipmentGrantEffect`** — `CheckRequirements` at [hybrasyl/Objects/ItemObject.cs](../hybrasyl/Objects/ItemObject.cs) gets a single new check; `RequiredTrait` becomes a new restriction axis on items in the Hybrasyl.Xml package. Two model variants (item declares required trait vs. trait bypasses class restriction by weapon type) deferred for later design.
- **`CastableGrantEffect`** — `Creature.UseCastable()` and `User.ShowLearnSkill()` / `ShowLearnSpellAccept()` get `HasTrait` checks at the eligibility step.
- **`AccessGrantEffect`** — `Creature.Walk()` warp logic and dialog invocation gain `HasTrait` checks. Will require a `HashSet<string> GrantedAccess` field on `User` for fast lookups.
- **Species mechanics** — when species ships, each species grants its baseline traits at character creation. Avoids carving a parallel species-mechanics subsystem.

None of these require restructuring `TraitInstance` or `TraitManager`. They each add one new `ITraitEffect` implementation and one or two integration-point checks.

## Verification

1. **Build:** Server compiles after Hybrasyl.Xml NuGet bump that introduces the `Trait` root type. `dotnet build` succeeds.
2. **Catalog load:** Server starts, log shows N traits loaded from `world/xml/traits/`. Malformed XML → log error, skip that file, continue.
3. **Lua API end-to-end:** In a test NPC dialog, call `user:AddTrait("spec.priest.tax")`. `user:HasTrait("spec.priest.tax")` returns true. `/listtraits <name>` shows it with source. The player's outgoing damage drops by 25% on the next assail (verify via combat log).
4. **Persistence round-trip:** Player logs out, logs back in. Trait still present, delta still applied — verify via stat sheet bonus column and an attack against a test dummy.
5. **Hot reload:** Edit `spec.priest.tax` to change `-0.25` → `-0.10`, run `/reloadtraits`. Player's damage adjusts on next attack without relog.
6. **Categorical conflict (Replace):** `AddTrait("aura.tuning.boost.10")` then `AddTrait("aura.tuning.tax.10")`. Second call replaces first (same `Exclusive="TuningAura"`). `ListTraits` shows only one.
7. **Categorical conflict (Reject):** Build a trait with `ExclusivePolicy="Reject"` in a category, add it, attempt to add a second trait in the same category. Second add returns false; `ListTraits` shows only the first.
8. **Idempotent add:** Second `AddTrait` of the same id returns false. No double application.
9. **Stale id:** Manually inject an unknown trait id into a player's `Traits` dict in Redis (via redis-cli). Log in. Server logs a warning, drops the entry, no errors, no NREs.
10. **Tests:** `dotnet test --filter Traits` passes.

## Out of scope / follow-ups

- **`EquipmentGrantEffect`** — weapon-skill traits and prestige-gear access. Needs a follow-up design choice between "items declare required trait" vs "traits bypass class restriction by weapon type." Both are viable; not in MVP.
- **`CastableGrantEffect`** — trait-unlocked spells and skills. Mechanically simple once the trait subsystem exists; deferred.
- **`AccessGrantEffect`** — trait-gated warps and dialogs. Needs a small `GrantedAccess` permission field on `User`.
- **Background-selection UI** — currently [Login.cs](../hybrasyl/Servers/Login.cs) `PacketHandler_0x04_CreateB` hardcodes the new-character payload. Adding a creation-time background picker is its own UX + protocol piece. The catalog can carry `background.*` traits in the meantime; they're applied via the admin command for testing.
- **Trait metadata getter for Lua** — `user:GetTrait(id) -> table` returning name/description/source/applied-at. Add when scripts actually need it.
- **Trait categories beyond `Exclusive` / `ConflictsWith`** — e.g., trait tags for grouping in admin UI, trait-required-for-trait dependencies. Add when a real use case appears.
- **`NumberCruncher.cs:218` heal modifier bug** — pre-existing, separate fix, not part of this work.
