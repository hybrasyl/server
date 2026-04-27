# Dreamweaver's Path — Plan

Add a horizontal progression system that runs parallel to traditional leveling. Players earn currencies through gameplay (including auto-conversion of XP at max level), spend them in talent-tree-style "Paths" composed of prerequisite-gated nodes, and gain permanent character or account benefits. Independent of class/level, integrates with Traits for the actual stat/effect plumbing.

## Context

Dark Ages tops out at level 99 with no further character growth — XP earned at max level today just accumulates in `Stats.Experience`, capped at `uint.MaxValue`, doing nothing. The classical post-99 progression (Insight / Ability) exists in the codebase as `Stats.Ability` and `Stats.AbilityExp` ([hybrasyl/Objects/StatInfo.cs](../hybrasyl/Objects/StatInfo.cs) lines 132, 152–160) but `GiveExperience` at [User.cs:818](../hybrasyl/Objects/User.cs#L818) doesn't feed it — there's no implemented post-cap progression.

Dreamweaver's Path is the planned post-cap (and parallel-to-cap) horizontal progression. Players earn one of four currencies through different gameplay loops, spend them in named "Paths" (talent trees with purchasable nodes), and accumulate permanent enhancements. Some Paths are character-specific (one wallet per character); others are account-wide (one wallet per account). Most node effects compose with the Traits subsystem — a node grants a trait, the trait does the actual stat/effect work — so this design doesn't reinvent the modifier-application wheel.

The full design is broad: 4 currencies, 13 Paths, account-level vs character-level split, species/religion/college/reputation Path content. Per discussion, the **MVP scope**:

- Build the **framework** (currencies, wallet, node graph, prerequisite engine, XP-conversion engine, purchase API, Lua, admin commands).
- Ship **4 character-level Paths** as proof-of-concept content: Essence, Combat, Exploration, Class.
- **Account-level concepts (Crystallized Memories currency, Core / Politics / Religion / College / Guild / Reputation / Expansion Paths) are designed forward** — schemas and integration points documented — but stubbed at runtime until the Account-manager work lands.

## Target behavior

- Players earn currencies through specific gameplay loops:
  - **Crystallized Reflections** — auto-earned from XP at level 99: every 500,000 XP gained at max level yields 1 Reflection. Per-character; spendable in any Path as a secondary cost (paired with the path's primary currency). The throttle on horizontal progression speed.
  - **Dream Essence** — earned through character-specific activities (kills, quests, milestones at any level). Per-character primary cost in character-level Paths (Essence, Combat, Exploration, Class, Species, Crafting, Conquest).
  - **Vital Essence** — earned through combat/leveling/challenge content. Per-character primary cost in the Essence Path _only_. Replaces Dark Ages's unlimited stat-purchase model with a capped, currency-gated investment system.
  - **Crystallized Memories** _(designed, not implemented in MVP)_ — earned through exploration/quests/lore. Account-wide primary cost in account-level Paths. Pending Account manager.
- Players spend currencies on **nodes** in **Paths**. Nodes have prerequisites (other nodes), costs (multi-currency), and effects (stat grants via Traits, currency multipliers, future kinds). Each node is one-shot per character (or per account, for account-level Paths).
- Each Path is a DAG of nodes with optional tier gates ("must own N total nodes in this Path before tier 2 unlocks"). Pure-linear tracks and pure-DAG trees are both expressible.
- Wallet state, owned-node set, and post-cap XP counter persist to Redis on the character. Account-level wallet (Memories) persists per-`AccountGuid` once accounts exist.
- Lua scripts and admin commands can grant/revoke nodes, grant/revoke currency, and manually trigger XP-conversion for testing. Hot reload (`/reloadpaths`) rebuilds the catalog and re-validates owned nodes against new XML.

## Scope

**In:**

- New content type **`DreamPath`** added to the `Hybrasyl.Xml` NuGet package — root for one Path, containing its nodes, their prereqs, costs, and effect bindings.
- New subsystem under `hybrasyl/Subsystems/Dreamweaver/`: `Wallet`, `DreamProgress` (per-character POCO with owned-node set + post-cap XP counter), `IDreamEffect`, `StatTraitEffect` (binds a node to a Traits-subsystem trait), `CurrencyMultiplierEffect`, `DreamPathManager`.
- Edits to [hybrasyl/Objects/Creature.cs](../hybrasyl/Objects/Creature.cs) — persisted `DreamProgress` field alongside `Cookies`, `Traits`, `AchievementProgress`.
- Edits to [hybrasyl/Objects/User.cs](../hybrasyl/Objects/User.cs) — public wrappers for the wallet and node-purchase API.
- XP-conversion hook in [User.cs `GiveExperience`](../hybrasyl/Objects/User.cs#L818) that increments `DreamProgress.PostCapExperience` when `Stats.Level == 99` and mints Reflections at threshold crossings.
- Lua API in [hybrasyl/Subsystems/Scripting/HybrasylUser.cs](../hybrasyl/Subsystems/Scripting/HybrasylUser.cs).
- Admin commands following the cookie/trait pattern.
- Four character-level Path XML files as proof-of-concept content.
- Tests in `Hybrasyl.Tests/Dreamweaver.cs`.

**In (designed-forward, schema-only, runtime-stubbed):**

- Crystallized Memories currency (carried in `Wallet`, addable via admin command for testing, but no auto-earn loop and no Path that consumes it ships).
- Account-level Path schemas (Core, Politics, Religion, College, Guild, Reputation, Expansion). Catalog can load them; runtime returns "account-level Paths not yet available" on purchase attempts. Forward-compatible — when accounts ship, the runtime stub flips to a real path resolver.
- Species Path is documented but stubbed; depends on the species_design memo's system, also unimplemented.

**Out:**

- The Account-manager work itself. Scoped separately. This doc designs against the assumption that `User.AccountGuid` will eventually point to a real Account entity.
- Religion, College, Reputation, Conquest, Politics underlying systems. Each is its own subsystem; this doc does not scope them.
- Client UI panel for browsing Paths and purchasing nodes. Wire-data contract is documented; client-side panel is a separate Chaos.Client effort (same shape as achievement panel work).
- Refund / respec UX. Admin command for refund ships in MVP; in-game respec is a follow-up.
- Cross-server / cross-shard progression sync. Single-server only.

## Approach

### Currencies and the wallet

A new `Wallet` lives on `Creature` alongside `Cookies` and `Traits`:

```csharp
public sealed class Wallet
{
    public long Reflections { get; set; }
    public long DreamEssence { get; set; }
    public long VitalEssence { get; set; }
    public long Memories { get; set; }   // account-level, populated by admin only in MVP
}
```

For MVP, the wallet is per-character — including `Memories`, even though Memories is conceptually account-wide. When the Account manager ships, `Memories` migrates to an `AccountWallet` keyed by `AccountGuid`; the `Wallet.Memories` field becomes a derived/read-through pointer to the account wallet. The schema choice today (single field on the per-character Wallet) keeps the migration path clean — a small one-shot script aggregates per-character Memories totals into the Account wallet on first load.

Currency operations: `User.AddCurrency(CurrencyType, long)`, `User.SpendCurrency(CurrencyType, long) -> bool`, `User.GetCurrency(CurrencyType) -> long`. All go through a single `Wallet.Spend()` that does the bounds-check (no negative balances) and emits a stat-update push to the client.

Gold stays where it is on `Stats.Gold` ([hybrasyl/Objects/StatInfo.cs:112](../hybrasyl/Objects/StatInfo.cs#L112)) — it's not part of this wallet. The wallet is for Dreamweaver currencies only.

### XP → Reflections conversion

A new field `DreamProgress.PostCapExperience` (long, monotonic) tracks XP earned while at max level. Hook in `GiveExperience` ([User.cs:818](../hybrasyl/Objects/User.cs#L818)):

```csharp
if (Stats.Level == Game.ActiveConfiguration.Constants.PlayerMaxLevel)
{
    DreamProgress.PostCapExperience += exp;
    var earned = DreamProgress.PostCapExperience / Constants.ReflectionsPerXp;  // 500_000
    var newReflections = earned - DreamProgress.ReflectionsMinted;
    if (newReflections > 0)
    {
        Wallet.Reflections += newReflections;
        DreamProgress.ReflectionsMinted = earned;
        // emit notification: "You gained N Crystallized Reflections."
    }
}
```

Integer division on a monotonic counter — no race condition, no double-mint risk, idempotent on replay. `ReflectionsMinted` is the high-water mark of "Reflections we've already paid out for the current `PostCapExperience` total." Test data from the exploration: typical XP awards range 2.5k–10k, so a max-level player earns 1 Reflection roughly every 50–200 combat encounters. Tunable via the `ReflectionsPerXp` constant.

The existing post-cap behavior in `GiveExperience` (XP accumulates capped at `uint.MaxValue` in `Stats.Experience`) remains unchanged. The new `PostCapExperience` is a parallel `long` counter solely for conversion math.

Dream Essence and Vital Essence are NOT auto-converted from XP. They're awarded by Lua scripts at quest completions, milestones, kills (for Dream Essence) and combat / level-up / challenge content (for Vital Essence). The framework provides `user:AddCurrency("DreamEssence", 50)`; specific earn-loop authoring is content work, not framework.

### Path schema

One XML file per Path under `world/xml/dreampaths/`. Schema added to `Hybrasyl.Xml`:

```xml
<DreamPath Id="path.essence" Name="Essence Path" Scope="Character"
           PrimaryCurrency="VitalEssence" SecondaryCurrency="Reflections">
  <Description>Invest in the foundations of your being — health, mana, and the core of your spirit.</Description>

  <!-- Optional: tier gates by total purchased -->
  <Tiers>
    <Tier Index="1" RequiresTotalPurchased="0" />
    <Tier Index="2" RequiresTotalPurchased="5" />
    <Tier Index="3" RequiresTotalPurchased="12" />
  </Tiers>

  <Nodes>
    <Node Id="essence.vitality.1" Name="Heart of the Dreamer I" Tier="1">
      <Description>Increase maximum HP.</Description>
      <Cost>
        <Currency Type="VitalEssence">10</Currency>
        <Currency Type="Reflections">1</Currency>
      </Cost>
      <Prerequisites />                            <!-- root node -->
      <Effects>
        <GrantTrait>dream.essence.vitality.1</GrantTrait>   <!-- Trait defines the actual +HP -->
      </Effects>
    </Node>

    <Node Id="essence.vitality.2" Name="Heart of the Dreamer II" Tier="2">
      <Cost>
        <Currency Type="VitalEssence">25</Currency>
        <Currency Type="Reflections">2</Currency>
      </Cost>
      <Prerequisites>
        <Node>essence.vitality.1</Node>
      </Prerequisites>
      <Effects>
        <GrantTrait>dream.essence.vitality.2</GrantTrait>
      </Effects>
    </Node>

    <Node Id="essence.acuity.1" Name="Mind of the Weaver I" Tier="1">
      <Cost>
        <Currency Type="VitalEssence">10</Currency>
        <Currency Type="Reflections">1</Currency>
      </Cost>
      <Prerequisites />
      <Effects>
        <GrantTrait>dream.essence.acuity.1</GrantTrait>
      </Effects>
    </Node>
  </Nodes>
</DreamPath>
```

A Path declares its scope (`Character` | `Account`), primary and secondary currencies, optional tier gates, and a list of nodes. Each node has cost (multi-currency), prerequisites (other node ids in the same Path), and effects.

### Reflections cost model

Per the design summary, Crystallized Reflections is "spendable in all paths as a secondary resource (requires one of the other currencies)." Concrete interpretation: **every node in every Path lists Reflections as one of its costs alongside the path's primary currency.** A node in the Essence Path costs both Vital Essence and Reflections; a node in a Politics Path (account-level) would cost both Memories and Reflections. Reflections never stand alone as a sole cost; the primary currency never stands alone either. Reflections is the throttle that ties horizontal progression to time-at-max-level engagement, regardless of which Path the player is investing in.

Alternative interpretations (Reflections as cap-gating, Reflections as multiplicative discount) were considered and rejected as more complex without a clear gameplay benefit. If the design intent was different, this is the place to flag it.

### Effect kinds

```csharp
public interface IDreamEffect
{
    void Apply(User user);
    void Remove(User user);   // for refund / hot reload
}
```

MVP ships two effect kinds:

- **`GrantTraitEffect`** — looks up a Trait id in the Traits catalog and calls `user.AddTrait(trait_id, source: "dream:" + node_id)`. The trait does the actual stat-modification work via the Traits subsystem (`Stats.Apply` etc.). This is how 90% of nodes will work — designers author one Trait XML for the stat package and one DreamPath node that grants it, and the existing Traits machinery handles modifier composition, hot reload, and Legend-mark synthesis.
- **`CurrencyMultiplierEffect`** — modifies the auto-earn rate for a currency. Example: a node "Wellspring of Memory" reduces `ReflectionsPerXp` from 500,000 to 450,000 for that character. Implemented as a per-character multiplier that the conversion engine reads.

Future effect kinds (deferred): `CastableUnlock` (grants known castable, parallels Traits' deferred CastableGrant), `SpeciesUnlock` (account-wide, gates new species at character creation), `PathUnlock` (one Path opens another), `AccessGrant` (zone/dialog access).

### Prerequisite engine

Each node declares zero or more prerequisite node ids in the same Path. The purchase check at `DreamPathManager.TryPurchase(user, path_id, node_id)`:

1. Resolve node in catalog. Unknown id → false.
2. If already owned, false (idempotent).
3. Walk prerequisites. Each must be owned. Otherwise false.
4. Walk tiers. If the node's `Tier` is N, the player's `total purchased in this Path` must be ≥ `Tiers[N-1].RequiresTotalPurchased`. Otherwise false.
5. Check costs against wallet. Insufficient → false.
6. Check Path scope. Account scope on a `Character`-resolved query when accounts aren't ready → false (with "not yet available" message).
7. All checks pass: deduct from wallet, add node id to `DreamProgress.OwnedNodes` (set keyed by `path_id|node_id`), call `effect.Apply(user)` for each effect, send notification.

Refund (admin only in MVP, via `/refundnode <player> <path> <node>`):

1. Verify owned.
2. Verify no other owned node lists this one as a prerequisite (cascading refund is out of scope).
3. Call `effect.Remove(user)` for each effect.
4. Refund cost to wallet.
5. Remove from `OwnedNodes`.

Cycle detection on the prerequisite graph is enforced at catalog load — a Path with a cycle fails to load and logs an error. No runtime cycle check; the catalog is trusted post-load.

### Per-character state

```csharp
public sealed class DreamProgress
{
    public HashSet<string> OwnedNodes { get; set; } = new();   // "path.essence|essence.vitality.1"
    public long PostCapExperience { get; set; }
    public long ReflectionsMinted { get; set; }
}
```

That's the full persistent state. Wallet is separate (above). Both fields live on `Creature`, both `[JsonProperty]`, both initialized via `[OnDeserialized]` defensive init. Existing characters deserialize cleanly with empty progress and zero wallets — no migration script.

### Lifecycle

**On login** (slotted into [hybrasyl/Servers/World.cs](../hybrasyl/Servers/World.cs) login sequence after Traits apply, before `UpdateAttributes(Full)`):

1. Validate `OwnedNodes` against catalog. Stale ids (node deleted from XML) → log warning, drop silently.
2. For each owned node, call its effects' `Apply(user)`. For `GrantTraitEffect`, this re-grants the trait via `user.AddTrait()` (idempotent, so no double application if traits also persisted).
3. Wallet packet pushed to client.

**On runtime purchase:** see prerequisite-engine flow above. Single `UpdateAttributes(Full)` after effects apply.

**On `/reloadpaths`:** rebuild catalog from XML. For each online user: validate owned nodes against new catalog, drop stale, re-apply remaining effects (idempotently — Trait grants are no-ops if already held, but allow values to refresh if the underlying Trait XML changed).

**On XP gain at max level:** the conversion path described above, fires once per `GiveExperience` call.

### Lua API

```lua
-- Currency operations
user:AddCurrency("DreamEssence", 50)              -- award via quest reward
user:GetCurrency("Reflections")                   -- read balance
user:SpendCurrency("VitalEssence", 10)            -- returns bool; consumed only if true

-- Node operations
user:HasNode("path.essence", "essence.vitality.1")
user:GrantNode("path.essence", "debug.test")      -- bypasses prereqs/cost; logs admin source
user:ListOwnedNodes("path.essence")               -- table of owned node ids in this path

-- Path scope inquiry
user:CanPurchaseNode("path.essence", "essence.vitality.2")  -- (bool, reason)
```

`SpendCurrency` is the contract for content-authored quest rewards or NPC vendors — Lua scripts can charge a player Dreamweaver currency directly without going through the node-purchase machinery, e.g., a custom NPC that sells a service for 5 Dream Essence. The framework doesn't care.

### Admin commands

- `/grantcurrency <player> <type> <amount>` — manual currency grant for testing.
- `/spendcurrency <player> <type> <amount>` — manual deduction for testing.
- `/grantnode <player> <path> <node>` — bypass prereqs/cost.
- `/refundnode <player> <path> <node>` — see refund rules above.
- `/listpaths <player>` — print owned nodes per Path with currency balances.
- `/reloadpaths` — rebuild catalog, re-validate online users.
- `/forcereflections <player>` — force-recompute reflection mints from `PostCapExperience` (test/repair tool for "I think the math drifted" cases).

### Client communication

Same fork as Achievements. Three data flows: wallet/balance updates, node-purchase events, and catalog/progress queries (for the future panel). Recommendation: **Option D hybrid** — MVP rides existing infrastructure, defer protocol-extension work to v2.

- **Wallet display:** extend the stat-update packet (0x08) to include Dreamweaver currencies, OR introduce a new `WalletUpdate` opcode. Recommend the latter — keeps the legacy stat packet untouched and gives the client a clean place to subscribe. New opcode means Hybrasyl.Xml + Chaos.Networking decoupling work, same constraint as Achievements. **For MVP**, ride a `SystemMessage` text update on every wallet change ("You now have 12 Crystallized Reflections.") — clunky but works against the current client.
- **Node purchase notification:** `SystemMessage` ("Acquired: Heart of the Dreamer I.") + `PlaySound` cue. Same as the Achievement-unlock notification pattern.
- **Catalog / panel:** deferred until the client builds a Dreamweaver panel. When that lands, expose via a new gRPC service (`Dreamweaver.proto`) extending the existing `PatronServer` infrastructure, OR via a new request/response opcode pair. Either way, the server side already has `WorldState.Values<DreamPath>()` ready to serve.

The framework cleanly separates "compute purchase result" from "emit notification." Switching from SystemMessage rides to opcode pushes is a single-file change in the notifier; persistent state is unaffected.

## Critical invariants

1. **`DreamProgress.OwnedNodes` is append-only in normal operation.** Only `/refundnode` removes; auto-flow never does.
2. **`ReflectionsMinted` is monotonic and only increases via integer division of `PostCapExperience`.** Never reset, never decremented in normal flow. The `/forcereflections` admin command is the only path that recomputes; even there it's idempotent.
3. **Trait grants from nodes use `source: "dream:" + node_id`.** This makes refund + Legend debugging tractable: `/listtraits` shows where each trait came from.
4. **Account-scope Paths refuse purchase in MVP** with a structured error message, not a silent failure. Forward-compatible: when accounts ship, the same code path resolves to the account wallet.
5. **Path catalog cycle check at load.** Cyclic prerequisite graphs fail catalog load with a logged error; the Path is dropped, the rest of the catalog still loads.
6. **Node effects are idempotent on `Apply`.** Login replay must not double-grant. `GrantTraitEffect` rides on Traits' idempotent `AddTrait`; `CurrencyMultiplierEffect` overwrites the per-character multiplier rather than stacking.

## Migration

Existing characters in Redis don't have `DreamProgress` or `Wallet` fields. Newtonsoft handles missing fields by leaving the property at default; combined with `[OnDeserialized]` defensive init, existing characters deserialize cleanly with empty progress and zero balances. No migration script for MVP.

When the Account-manager work lands, a one-shot migration script aggregates per-character `Wallet.Memories` totals into the new `AccountWallet` keyed by `AccountGuid` (zero in MVP since no auto-earn loop ships). The per-character `Wallet.Memories` field is then deprecated; the read-through stays for backward-compat one release, then is removed.

## Forward compatibility

- **Account-level Paths and Memories currency:** schema is already defined; the runtime stub becomes a real path resolver pointing at the Account wallet. Catalog loading is unchanged. The per-character wallet's `Memories` field is replaced by a read-through to the account wallet.
- **More effect kinds:** `IDreamEffect` is the extension point. Adding `CastableUnlockEffect`, `SpeciesUnlockEffect`, `PathUnlockEffect`, `AccessGrantEffect` is a per-effect implementation. No core changes.
- **Species Path:** depends on the Species subsystem (per the species_design memo) being built. Once species exists, a `SpeciesPath` per playable species drops in as content. Reputation Path's species-unlock nodes use `SpeciesUnlockEffect`.
- **Religion / College / Politics / Conquest / Guild Paths:** each depends on its underlying subsystem. Path schema is reusable; node effects extend.
- **Cross-Path prerequisites:** the prerequisite graph is currently per-Path. If the design wants "must own X in Combat Path before Y is available in Class Path," that's a one-line schema extension (`<Prerequisite Path="path.combat" Node="combat.weapon.mastery.3" />`).
- **Per-currency multipliers on earn rate:** `CurrencyMultiplierEffect` exists in MVP, applies per-character. Account-wide multipliers are deferred until accounts.
- **Respec / refund UX:** in-game refund follows the admin `/refundnode` command's logic with added cost (e.g., Reflections-tax on respec). Cascading refund (refunding a node automatically refunds dependents) needs a topo-sort pass; not in MVP.

## What we'd communicate to Chaos.Client

Per the same wire-data discussion documented in [achievements-plan.md](achievements-plan.md), three flows to Chaos.Client (the Hybrasyl-targeted client at `e:\Dark Ages Dev\Repos\Chaos.Client`):

**Push (server → client, real-time):**

- Wallet update: `{ reflections, dream_essence, vital_essence, memories }` after every change.
- Node-purchased event: `{ path_id, node_id, name, description }` for the toast.
- Reflection-minted event: `{ amount, total_balance }` when the conversion engine fires at a 500k crossing.

**Pull (request-response):**

- Path catalog: `{ paths: [{ id, name, scope, primary_currency, secondary_currency, tiers, nodes: [{...}] }] }`. Cacheable until catalog reload.
- Owned-node snapshot: `{ owned: ["path.essence|essence.vitality.1", ...], wallet: {...}, post_cap_experience }`.
- Purchasable check: `{ purchasable: bool, reason: string }` for client-side UI gating before the request hits the server.

**MVP wire format:** `SystemMessage` text for purchases and reflection mints; per-wallet stat push or a new `WalletUpdate` opcode (decision deferred; a `SystemMessage` per change works in the meantime). Catalog/snapshot deferred until the client builds a panel.

**Client-side surfaces (out of scope for the server):**

- Dreamweaver panel (`PrefabPanel` subclass) — Path browser, node tree visualization, purchase flow.
- Currency display in the HUD or stat sheet — must coexist with the existing gold display.
- Toast widget for purchase confirmations and reflection mints.
- ViewModel additions in `WorldState` for `Wallet`, `OwnedNodes`, `Paths`.

## Verification

1. **Build:** Server compiles after Hybrasyl.Xml NuGet bump introducing `DreamPath` schema + `Wallet`/`DreamProgress` types.
2. **Catalog load:** Server starts, log shows N DreamPaths loaded with M total nodes; cycle check produces no errors; reverse index built.
3. **Wallet basics:** `/grantcurrency <player> Reflections 10` raises balance; `/listpaths` shows it; SystemMessage confirms.
4. **XP-conversion at max level:** Test character at level 99 with `Stats.Experience` of 0 and `PostCapExperience` of 0. Use `/give exp 1500000` (or kill a monster three times). After: `PostCapExperience == 1_500_000`, `Reflections == 3`, `ReflectionsMinted == 3`. Verify via `/listpaths`.
5. **Conversion is idempotent:** Restart the server. Log back in. Reflections still 3, no double-mint.
6. **Node purchase happy path:** Test character has 10 Vital Essence and 1 Reflection. Purchase `path.essence|essence.vitality.1`. Cost deducted, node added to OwnedNodes, Trait `dream.essence.vitality.1` granted, HP bonus visible on stat sheet.
7. **Prerequisite enforcement:** Same character attempts `essence.vitality.2` before owning `.1`. Purchase rejected with "Prerequisite not met: essence.vitality.1".
8. **Tier gate enforcement:** Owns 4 nodes; attempts a tier-2 node requiring 5. Rejected. Buys a fifth tier-1 node; tier-2 now unlocks.
9. **Idempotent purchase:** Attempts to re-purchase an owned node. Rejected, no double-grant, no double-deduct.
10. **Account-scope Path refusal:** A Path declared `Scope="Account"` is in the catalog. Purchase attempt returns "Account-level Paths not yet available." Wallet unchanged.
11. **Refund:** `/refundnode` on `essence.vitality.2`. Cost refunded, Trait removed (HP bonus drops), node removed from OwnedNodes.
12. **Refund blocked by dependent:** Owns `.1` and `.2`. `/refundnode` on `.1` rejected with "Cannot refund: essence.vitality.2 depends on this node."
13. **Hot reload:** Edit a node's cost in XML, run `/reloadpaths`. No NREs, online users' wallets unaffected, future purchases use new costs. Existing owned nodes' effects re-apply (no-op for stable Trait grants).
14. **Stale node id:** Manually inject `path.essence|nonexistent.node` into a player's `OwnedNodes` in Redis. Log in. Server logs warning, drops the entry, no errors.
15. **Lua end-to-end:** A test NPC dialog awards 50 Dream Essence via `user:AddCurrency("DreamEssence", 50)`, then offers a node purchase. Player accepts, dialog calls `user:CanPurchaseNode(...)` first, then the purchase fires through the framework.
16. **Tests:** `dotnet test --filter Dreamweaver` passes.

## Open design questions

These are flagged for the user to weigh in on before implementation; recommendations are made above but not committed.

- **Reflections cost interpretation.** Doc commits to "co-cost on every node" (Reflections + primary currency). If the design intent was different (cap-gating, multiplicative discount), flag it.
- **Vital Essence cap.** Doc treats the cap as emergent from path content (you can only buy what's in the catalog). If a hard system-level cap on total purchasable Vital Essence per character is wanted, it needs a constant.
- **Dream Essence vs Vital Essence earn loops.** Both are character-level, both earned through "character activities." The split (Vital = HP/MP/stats only, Dream = everything else character-level) implies two separate earn loops. What activities feed which? Not in scope for this doc, but a content-design question for Path authoring.
- **Cross-Path prerequisites.** Schema can support but MVP nodes use within-Path prereqs only. Worth a stance.
- **Refund cost.** MVP refund is admin-only and free. In-game respec design is deferred but should land before the system goes live to players, since "I bought the wrong node and can't undo it" is a major friction point.

## Out of scope / follow-ups

- **Account manager itself.** Pre-req for account-level Paths to be live. Per the existing scoping memo.
- **Species, Religion, College, Reputation, Conquest, Politics, Guild, Expansion subsystems.** Each is its own subsystem; this doc designs the Path framework agnostic to them.
- **Chaos.Client Dreamweaver panel.** Requires the wire-data contract above plus panel + toast prefabs in the client. Scoped separately when server-side ships.
- **Option A / Option C client-channel work.** Same as Achievements — deferred until Chaos.Networking decoupling lands or the team wants to pilot local opcodes.
- **In-game respec UX.** Admin refund ships in MVP; player-facing respec is a v2 feature.
- **Cascading refund.** Refunding a node that has dependents currently fails. A topo-sort-driven cascade refund is a future feature once content stabilizes.
- **Path-completion bonuses.** Some MMOs grant a capstone effect for 100%-completing a Path. Not in MVP; trivial to add via a `<OnPathComplete>` element later.
- **Cross-server / cross-shard sync.** Single-server only.
- **Telemetry / analytics on node-purchase patterns.** Out of scope; can ride on existing logging infrastructure.
