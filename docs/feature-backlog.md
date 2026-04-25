# Feature Backlog — To Scope

Working list of features and improvements that need their own scope/design docs. Captured here so they don't get lost; pull items into their own `*-plan.md` when ready to scope.

Each entry: brief description, current state in the codebase, key considerations / dependencies, open questions. Not full designs — just enough context to refresh memory later.

---

## 1. Quest log

A structured per-character quest tracker with a UI panel showing in-progress, completed, and available quests.

**Current state:** [QuestMetadata.cs](../hybrasyl/Internals/Metafiles/QuestMetadata.cs) defines a catalog (`Id`, `Title`, `Summary`, `Result`, `Reward`, `Prerequisite`) but the `Prerequisite` field is unused at runtime. Per-user quest state today is entirely cookie-driven — Lua scripts write things like `questid:mileth_orphans:completed` and check those cookies elsewhere. No structured "you are on step 3 of N" tracking, no quest list panel, no "available quests in your area" surface.

**Considerations:**
- Significant overlap with the Achievements subsystem: counter-driven progress, prerequisite gating, hot-reloadable catalog. Worth deciding whether quests *are* a kind of achievement or share infrastructure rather than duplicate it.
- Cookie-based quest state is brittle but pervasive — migration path matters.
- Quest UI is new client work (panel + active-quest tracker on HUD).

**Open questions:**
- Per-character or account-wide quest state? (Some achievements-style content benefits from cross-character.)
- Branching / multi-outcome quests — schema needs to support divergent paths.
- Time-limited / repeatable quests — daily/weekly quest scaffolding.
- Auto-tracking on HUD vs explicit "track this quest" pin.

---

## 2. Chat refactor — channels, length, combat log

Multiple chat channels (global / party / guild / trade / whisper / system / combat), longer message length, structured combat log.

**Current state:** [SystemMessage](../hybrasyl/Networking/ServerPackets/SystemMessage.cs) (opcode `0x0A`) is the single text-pushing mechanism — type byte + `String16` payload. `OnHear` in [User.cs:457-465](../hybrasyl/Objects/User.cs#L457-L465) handles speaker-to-listener formatting. There's a `combatlog:on/off` cookie precedent in [CombatLogCommand.cs](../hybrasyl/Subsystems/Messaging/ChatCommands/CombatLogCommand.cs) for opt-in combat verbosity, but it's not routed to a structured channel — combat events ride the same SystemMessage stream as everything else.

**Considerations:**
- The `String16` payload technically supports 65,535 bytes; the legacy client likely truncates well below that. Real ceiling needs measurement.
- Multi-channel implies channel-tagged routing on the server (whose subscribers receive what) and a client-side channel-aware chat panel that doesn't exist today.
- Combat log as a structured channel: events are already fired at the natural sites (Monster.OnDeath, damage application, etc.) — wiring them into a typed event channel is straightforward server-side; client UI is the hard part.
- Whisper / direct messages exist in some form already (verify); cross-channel formatting (e.g., guild chat color) is a new concern.

**Open questions:**
- New opcode(s) for channel-aware messages, or extend SystemMessage's type byte to encode channel?
- Server-side combat log persistence (replay later) vs client-only ephemeral?
- Per-channel mute / filter / opt-in policy.
- Cross-channel formatting (color, prefixes, timestamps).

---

## 3. Tooltips — items, buffs, debuffs, creatures

Hover-or-click tooltips that surface server-side metadata to the player: item description + stats + requirements, buff/debuff effect descriptions + remaining duration, creature info (level, HP, abilities) at appropriate reveal levels.

**Current state:** Item, Status, and Monster XML schemas in `Hybrasyl.Xml` carry rich descriptive data — server-side it's all there. Today the wire protocol pushes minimal data: item icon + sprite + name; status icon + duration; monster sprite + name. Tooltips don't exist as a structured feature; players rely on external wikis.

**Considerations:**
- Lots of data per query, but most of it is static (item descriptions don't change at runtime). Strong case for a "describe this id, I'll cache it" pattern rather than push-everything-at-equip.
- Status duration is dynamic — needs frequent updates if shown.
- Creature info raises a privacy/reveal question — should an unidentified creature show full stats? Probably staged reveal (level visible, HP not, until identify).
- Client-side tooltip rendering is significant new UI work but well-understood pattern (every modern MMO has it).

**Open questions:**
- One unified `Tooltip` opcode keyed by (entity_type, id), or separate opcodes per kind?
- Cache invalidation: do tooltip strings change at runtime? (If only via hot-reload, simple invalidate-all-on-reload works.)
- Reveal rules for creatures — flat by class/level, or gated by an identify spell?
- gRPC vs new packet — the same fork as achievements / Dreamweaver. Static-ish data is a natural fit for a query/response gRPC service.

---

## 4. Spell/skill panel — tabbed info display

Replace the current right-click action on a spell/skill icon with a tabbed information panel: tab 1 surfaces skill info (cast cost, description, cooldown, prerequisites), tab 2 displays the spell's "lines" (the cast-component breakdown shown in the legacy cast bar).

**Current state:** Castable XML in `Hybrasyl.Xml` carries the full data: lines, cooldown, MP/HP cost, description, mastery requirements, learn prerequisites. Server has all of it. The legacy client right-click likely surfaces a subset via the existing castable info packet (verify). "Lines" data is in the Castable's `Lines` / animation block.

**Considerations:**
- Most data is already server-side; the missing piece is structured push to the client and the panel UI itself.
- Right-click input behavior is client-side input handling — server doesn't dictate it. The server-side scope is "expose the data the panel needs"; the panel itself is Chaos.Client work.
- Connects with the broader Tooltips item (item #3) — probably one underlying server-side data-exposure mechanism handles both.

**Open questions:**
- Push proactively (when SpellBook updates) vs on-demand request (when player opens the panel).
- Mastery percentage display — is per-castable mastery exposed today? (`Hybrasyl.Tests/CastableRestrictions.cs` shows mastery prereqs in tests, suggesting it exists.)
- Per-class differentiation — same spell, different display per class (different lines)?
- Should the panel offer "favorite / pin" so the player can promote a spell to a hotkey?

---

## 5. Suggestions for review

Items I noticed during the recent design work (traits, achievements, Dreamweaver's Path) that warrant their own scoping. None are committed — flag-or-discard as you see fit.

### 5a. NumberCruncher heal-modifier bug

A small, contained pre-existing bug found during the traits exploration. [NumberCruncher.cs:218](../hybrasyl/Subsystems/Formulas/NumberCruncher.cs#L218) in the status heal path reads `target.Stats.OutboundHealModifier` where it should be reading `InboundHealModifier`. Standalone fix, no design needed — could land as a one-line PR or as another entry in [easy-wins-plan.md](easy-wins-plan.md). Flagged in both `traits-plan.md` and `achievements-plan.md` as deferred; lock it in or merge it before content depends on the broken behavior.

### 5b. Hot-reload infrastructure

`IXmlReloadable` interface exists at `hybrasyl/Interfaces/IXmlReloadable.cs` but [ReloadCommand.cs](../hybrasyl/Subsystems/Messaging/ChatCommands/ReloadCommand.cs) is a stub ("not yet implemented" at line 31). The Traits, Achievements, and Dreamweaver's Path docs all promise `/reloadtraits` / `/reloadachievements` / `/reloadpaths` — implementing those individually means three near-duplicate hot-reload pipelines. Scoping a unified hot-reload mechanism that works for any `IXmlReloadable` content type, including walking online users to re-baseline state, would unblock content-author iteration speed and make all three reload commands consistent. Probably the highest-leverage infrastructure scope on this list.

### 5c. Multi-currency wallet groundwork

Gold today is a single `uint Stats.Gold` ([StatInfo.cs:112](../hybrasyl/Objects/StatInfo.cs#L112)) with no wallet abstraction. Dreamweaver's Path needs 4 new currencies; future faction / event / premium currencies would also need a home. A standalone Wallet refactor — gold + N other currencies, persisted under one structure, displayed via one packet path — could land before Dreamweaver and de-risk the harder parts of that doc's MVP. Scope is small (file changes are bounded; client side is one new opcode or stat-packet extension), value is broad.

### 5d. Account manager design

Existing memory note says "per-character auth today, no Account entity, needs full design before implementation" — and now Dreamweaver's Path, planned achievements expansion (cross-character), Memories currency, account-wide guild perks, and ~5 of the Dreamweaver Paths all want it. `AccountGuid` is on `User` ([User.cs:93](../hybrasyl/Objects/User.cs#L93)) and Vault is account-scoped via that GUID, but there's no Account entity, no shared state, no cross-character iteration, no account-event pub/sub. Scoping a full Account design — entity, persistence, cross-character notification, migration from `AccountGuid` to a real `Account` — would unblock a meaningful slice of the upcoming roadmap. Probably the highest-priority item on this list if multiple downstream features actually want to ship.

### 5e. Species system implementation

Designed in [species_design.md memory note](#) — 50-species enum, Human default, ushort sprite index, body style mapping in ServerConfig XML — but verified unimplemented during the achievements exploration: no Species enum, no Species field on User, no game mechanics keying off it. Blocks the Reputation Path and Species Path in Dreamweaver, blocks species-driven trait scenarios, and is referenced as "the natural vehicle" for trait grants in the traits doc's forward-compat section. Has a design memo but needs a proper plan: schema, persistence, character-creation integration, server-side mechanics hooks, sprite/body-style wiring on the client. Not infrastructure-light — at least a medium-sized scope.

### 5f. Player-facing respec / refund UX

Today there's no player respec for `LevelPoints` (stat points spent via [World.cs:3638 `PacketHandler_0x47_StatPoint`](../hybrasyl/Servers/World.cs#L3638)) and Dreamweaver's Path will create another permanent-spend system. Both need a player-facing refund mechanism eventually — admin-only refund is a launch-day-only stance. Scoping a unified respec UX (NPC-driven? Token-gated? Cooldowns?) once, applied to both LevelPoints and Dream nodes, beats two separate refund implementations. Worth a design before either system has shipped to players.

---

## How to use this list

- When an item is ready for proper scoping, create a `<topic>-plan.md` in this folder using the convention from `traits-plan.md` / `achievements-plan.md` / `dreamweavers-path-plan.md`.
- Remove the entry from this file (or leave it with a "→ scoped at [link]" note) once it has its own plan.
- New items get appended — keep the file sorted only by where you want to look at it next, not alphabetical or chronological.
