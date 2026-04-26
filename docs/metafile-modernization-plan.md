# Metafile Modernization — Plan

Replace the legacy "metafile" content-delivery channel with a gRPC-based `ContentService` extending the existing `PatronServer`. Legacy metafile generation and `0x7B`/`0x6F` transmission stay running in parallel for now — eventual cutover when the new channel is proven stable. Foundation for the richer per-entity content the backlog wants (Tooltips, Quest log, Spell/skill panel, Achievements catalog, Dreamweaver paths).

## Context

The Hybrasyl server delivers static catalog content (item descriptions, per-class skill/spell info, quest summaries, NPC portraits, nation names, lighting overlays) to the client through metafiles: hand-rolled binary, CP949-encoded text, DEFLATE-compressed, CRC32-checksummed. Generation lives in [hybrasyl/Internals/Metafiles/](../hybrasyl/Internals/Metafiles/) (`Metafile`, `CompiledMetafile`, `MetafileNode`) and runs at server startup via [`World.GenerateMetafiles()`](../hybrasyl/Servers/World.cs) (around line 203 onward, with per-type generators continuing to ~line 750). Transmission: client requests via opcode `0x7B`, server replies via opcode `0x6F` either as `AllCheckSums` (just CRCs for cache validation) or `DataByName` (full compressed payload). Active types today: `ItemInfo0-15` (16 metafiles), `SClass1-5` (per-class skills + spells), `SEvent1-6` (quests by circle), `NPCIllust`, `NationDesc`. Chaos.Client also recognizes `Light` (darkness overlays — appears DA-data-derived, not server-generated; verify before migration).

The system works. Chaos.Client's compat matrix in [`Chaos.Client/docs/chaos-networking-removal-direction.md`](../../Chaos.Client/docs/chaos-networking-removal-direction.md) marks `0x6F` as "MATCH" — both sides agree on the format, no protocol divergence today. The client team's roadmap doesn't currently plan to replace metafiles.

But the format is hostile to evolution in two ways:

- **Mechanically:** hand-rolled binary serialization with CP949 encoding (a 1990s Korean retail constraint that no longer applies), fixed-shape per metafile type. Adding a column to `ItemInfo` requires coordinated edits to `CompiledMetafile`'s server-side serializer and Chaos.Client's `ItemMetadataEntry.ParseEntry()`. Every new metafile type is a new bespoke binary schema.
- **Architecturally:** the Tooltips, Spell/skill panel, Quest log, Achievements, and Dreamweaver's Path features in the [feature backlog](feature-backlog.md) all want richer per-entity data than the metafile shape cleanly carries. Pushing them through metafiles means more bespoke types, each requiring matched serializer/parser updates on both sides for any iteration.

Now that we control both server and client (sichii is excluded), there's no retail-compat constraint blocking modernization. The decision: build a gRPC `ContentService` alongside the existing [`PatronServer`](../hybrasyl/grpc/PatronServer.cs), migrate content-delivery to it, keep legacy metafiles running in parallel as a backstop until the new channel is proven stable.

## Target behavior

- A new gRPC service `ContentService` runs alongside `PatronServer` on the existing gRPC port. Defined by `hybrasyl/protos/Content.proto`, implemented in `hybrasyl/grpc/ContentServer.cs`.
- Endpoints serve all six active content domains today carried by metafiles: items, castables-by-class, quests, npc illustrations, nations, lighting. Schema-versioned via protobuf (additive evolution).
- Each domain supports two query shapes:
  - **Bulk sync** with version token: `GetItems(VersionRequest) returns (ItemBundle)` — full domain payload + new version stamp.
  - **Server-streaming hot reload**: `WatchContent(WatchRequest) returns (stream ContentUpdate)` — pushes invalidations and updated entries when XML reloads.
- Auth: client presents a session token (issued by an extended `PatronServer.Auth` endpoint at login) on every `ContentService` call. Token bound to the player's active `Client` connection; expires on disconnect.
- Chaos.Client establishes a gRPC connection after world entry and prefers `ContentRepository` (gRPC-backed) for migrated domains. `MetaFileRepository` retained, refactored to delegate to `ContentRepository` when available; falls back to legacy on-disk metafile cache or legacy `0x7B` request when gRPC unavailable.
- Server continues generating metafiles at startup and responding to `0x7B`/`0x6F` exactly as today. A config flag on the server (`Content.LegacyMetafilesEnabled = true` initially) gates eventual cutover; default stays on through MVP.
- New backlog content (Tooltips with richer fields, Quest log details, Spell/skill panel data, Achievements catalog, Dreamweaver path/wallet catalog) joins `ContentService` rather than spawning new metafile types.

## Scope

**In:**
- New file `hybrasyl/protos/Content.proto` — gRPC service + message definitions for the six legacy domains, structured for forward additive growth.
- New file `hybrasyl/grpc/ContentServer.cs` — service implementation reading from the same `WorldData.Values<T>()` pipeline that feeds metafile generation in [`hybrasyl/WorldStateStore.cs`](../hybrasyl/WorldStateStore.cs).
- Edits to [hybrasyl/Game.cs](../hybrasyl/Game.cs) lines 751–783 — register `ContentService` alongside `PatronServer` on the existing gRPC server.
- Edits to [hybrasyl/grpc/PatronServer.cs](../hybrasyl/grpc/PatronServer.cs) — extend `Auth` to issue a session token; add a `RefreshToken` endpoint or equivalent. Add a `SessionTokenRegistry` keyed by token → `Client.ConnectionId` for `ContentService` to validate against.
- New `hybrasyl/Subsystems/Content/` directory housing the version-stamp tracker, the `WatchContent` subscriber registry, and content-domain bundle builders.
- Hot-reload integration: when XML content is reloaded (whichever mechanism handles that — see [feature-backlog.md item 5b](feature-backlog.md) on hot-reload infrastructure), `ContentService` bumps the relevant domain's version stamp and pushes `ContentUpdate` messages on active `WatchContent` streams.
- Config flag `Content.LegacyMetafilesEnabled` (default `true` in MVP) gating whether `World.GenerateMetafiles()` runs and whether `PacketHandler_0x7B_RequestMetafile` responds with data.
- Chaos.Client side (called out for coordination, not in this scope's implementation): new `Chaos.Client/Networking/ContentRepository.cs` wrapping `Grpc.Net.Client` calls; refactor of [`Chaos.Client/Data/Repositories/MetaFileRepository.cs`](../../Chaos.Client/Data/Repositories/MetaFileRepository.cs) to delegate-then-fallback; gRPC connection establishment in [`Chaos.Client/ChaosGame.cs`](../../Chaos.Client/ChaosGame.cs) (around lines 110–120, 653, 662–681).
- Tests under `Hybrasyl.Tests/Content/` covering: domain bundle correctness, version stamp incrementing, auth token enforcement, fallback path (gRPC down → legacy still works).

**In (parallel-running, not removed):**
- Legacy [`Metafile`/`CompiledMetafile`/`MetafileNode`](../hybrasyl/Internals/Metafiles/) classes stay.
- `World.GenerateMetafiles()` and per-type generators (`World.cs` lines ~389–750) continue to run at startup when the config flag is enabled.
- `PacketHandler_0x7B_RequestMetafile` ([`World.cs:3834`](../hybrasyl/Servers/World.cs#L3834)+) continues responding to client requests.
- Chaos.Client's existing metafile receive path (`ConnectionManager.HandleMetaData` at line 1901–1904, on-disk cache, CRC validation in `ChaosGame` lines 653, 662–681) remains intact as the fallback.

**Out:**
- Removing legacy metafile generation entirely. Separate scope, after sustained `ContentService` stability across a real player population.
- Migrating `Light` if exploration confirms it's DA-data-derived and never server-generated. May stay client-local indefinitely.
- Multi-server / cross-shard content distribution. `ContentService` is single-server.
- Authoring tooling (live XML edit → push to all clients via `WatchContent`). The infrastructure supports it; building the editor UI is separate.
- Chaos.Client team coordination for the client-side refactor. The server-side `ContentService` can ship without client changes — legacy metafile path keeps the client functional. Client-side adoption is a coordinated follow-up.
- New backlog content schemas (Tooltips, Quest log, Spell/skill panel, Achievements, Dreamweaver paths) — those have their own scope docs. This work is the foundation they will use.

## Approach

### Service shape

```protobuf
service ContentService {
  rpc GetNations(VersionRequest) returns (NationBundle);
  rpc GetItems(VersionRequest) returns (ItemBundle);
  rpc GetCastables(CastablesRequest) returns (CastableBundle);   // by class
  rpc GetQuests(VersionRequest) returns (QuestBundle);
  rpc GetNpcIllustrations(VersionRequest) returns (NpcIllustBundle);
  rpc GetLighting(VersionRequest) returns (LightingBundle);

  rpc WatchContent(WatchRequest) returns (stream ContentUpdate);
}

message VersionRequest {
  string session_token = 1;
  uint64 known_version = 2;   // 0 = no cache; server returns full payload
}

message ContentUpdate {
  string domain = 1;          // "items", "castables.priest", "quests", etc.
  uint64 new_version = 2;
  oneof payload {
    Invalidate invalidate = 3;       // "your cache is stale; re-fetch this domain"
    Delta delta = 4;                  // optional: per-entity additions/changes
  }
}
```

`VersionRequest`'s `known_version` field gives the bandwidth optimization: client says "I have version N," server returns either an empty `NotModified` response or the new bundle + version stamp. Equivalent to the legacy CRC-based cache validation but per-domain rather than per-metafile. Per-entity ETags are deferred — the per-domain stamp keeps the protocol simple and is sufficient for content sizes we're working with (largest is items, currently 16 metafiles totaling well under a MB compressed).

`WatchContent` is the hot-reload push channel. Client subscribes after first sync; server fires `ContentUpdate` with new version stamps as XML reloads. Client re-fetches affected domains via the bulk-sync endpoint.

### Auth and session

Today, [`PatronServer.Auth`](../hybrasyl/grpc/PatronServer.cs) is a one-shot login operation — it validates credentials and returns a result, no ongoing session. `ContentService` calls need persistent auth.

Approach:

- Extend `PatronServer.Auth` to return a session token (random GUID) on success. Register the token in a `SessionTokenRegistry` (new file under `hybrasyl/Subsystems/Content/`), keyed by token → `(connection_id, expiry, account_guid)`.
- Token TTL: 24 hours (configurable). Refreshed by the client on use; server bumps the expiry on each successful call.
- Bind the token to the player's active `Client.ConnectionId` from [`hybrasyl/Networking/Client.cs`](../hybrasyl/Networking/Client.cs). When the legacy TCP socket disconnects, the registry entry is invalidated.
- Every `ContentService` RPC takes `session_token` in its request and validates against the registry before serving. Unknown / expired token → `Unauthenticated` gRPC status.
- The legacy gameplay socket continues using its existing per-packet encryption — `ContentService` auth is purely for the gRPC side. No change to legacy auth.

This keeps auth scoped to the active session and avoids inventing a long-lived account-token mechanism. When the [Account manager](feature-backlog.md#5d-account-manager-design) work lands, the registry can switch to keying on `account_guid` for cross-character consistency.

### Data sourcing — single source of truth

Both the legacy metafile generator and `ContentService` read from `WorldData.Values<T>()` — the same `IStateStorable` source. No duplicated content paths, no risk of metafile and gRPC bundle disagreeing on what an item's description says.

The metafile generator currently transforms `WorldData` into the legacy `MetafileNode` shape inside `World.GenerateMetafiles()`. The new bundle builders (one per domain, under `hybrasyl/Subsystems/Content/`) transform the same `WorldData` into the protobuf message shape. Both operate on the same in-memory `WorldData` snapshot — divergence is impossible by construction unless one of the transformers has a bug, in which case tests catch it.

When XML hot-reload reloads `WorldData`, both the metafile generator (via existing flow if/when one exists — see [feature-backlog.md item 5b](feature-backlog.md)) and `ContentService`'s version stamps update from the same trigger.

### Migration phases

Each phase is independently deployable; legacy metafiles work throughout.

1. **Server-side foundation.** Define `Content.proto`, scaffold `ContentServer.cs`, register with the gRPC server, implement `SessionTokenRegistry` and the `Auth` token issuance. No real domain endpoints yet — just plumbing. Verify with `grpcurl` that the service is reachable, auth works, unauthenticated calls are rejected.

2. **First domain — `Nations`.** Implement `GetNations` (smallest payload, smallest blast radius). Bundle builder reads `WorldData.Values<Nation>()`. Verify against the legacy `NationDesc` metafile output for byte-for-byte equivalence in semantic content (not literal bytes — the wire formats differ, the data must match). Server-side only; no client changes yet.

3. **Remaining domains, one at a time.** `NpcIllustrations`, `Lighting` (if server-sourced; defer otherwise), `Quests`, `Items`, `Castables` (largest, last). Each phase: implement bundle builder, add tests, verify legacy parity. Server-side only.

4. **`WatchContent` push channel.** Implement subscription registry + push on version-stamp change. Test by triggering manual version bumps and watching subscribers receive updates.

5. **Chaos.Client adoption.** This is a separate effort coordinated with the client team. Server side has been complete and proven for weeks at this point. Client adds `ContentRepository` + delegate-fallback in `MetaFileRepository`. Migrate domains one at a time, same order as server. Each migrated domain: client requests via gRPC; if it succeeds, skip the legacy metafile request for that name; if gRPC fails, fall back to legacy.

6. **Cutover (separate future scope).** After sustained gRPC stability with real player traffic: flip `Content.LegacyMetafilesEnabled = false` in production. Server stops generating metafiles, `0x7B` returns "not available" or empty, clients depend entirely on gRPC. Removing the legacy code is a follow-up to that cutover, not part of it.

### Fallback behavior

Hard requirement: gRPC failure must not break the client. Three failure modes to handle:

- **gRPC connection refused / timeout at world entry.** Client logs the failure, skips `ContentRepository`, falls through to the legacy metafile request flow. UI behaves exactly as it does today.
- **gRPC call fails mid-session** (network blip, server restart). Repository delegates to legacy on-disk metafile cache for the affected domain. If the cache is stale, the existing CRC-mismatch path triggers a legacy `0x7B` re-request.
- **Auth token expired.** Client re-authenticates via the extended `Auth` endpoint, retries the failed call. Player sees no interruption.

Server side: `ContentService` failures (e.g., bundle builder throws) return a gRPC error status; client treats as fall-through. The legacy metafile path remains a safety net throughout MVP and during initial cutover.

### Cache invalidation

Per-domain version stamp, monotonic, incremented on each XML reload. Stamp lives in-memory on the server; clients pass their last-known stamp on every request, server short-circuits with `NotModified` if unchanged.

Per-entity ETags were considered and rejected as over-engineering for MVP. Content domains are small (few hundred items, fewer NPCs, ~50 nations), and full-domain re-sync after any change is cheap enough to not justify per-entity tracking. Revisit if a domain grows to thousands of entries.

Across server restarts, the version stamp resets to 1 — clients with cached version `> 1` will re-fetch on first request. Acceptable; restarts are infrequent and re-fetch is cheap.

### Forward-compat schema

`Content.proto` is structured so future backlog content slots in additively. Domain message types use protobuf's standard additive evolution rules: new fields are optional, never reordered, never repurposed.

The new content domains the backlog implies:

- **Tooltips** — likely an extension of `Item`, `Castable`, `Status` messages to carry richer description fields. Additive on existing domains.
- **Quest log** — extension of the `Quest` message with branching state, prerequisite graph, time-limited markers.
- **Spell/skill panel** — extension of `Castable` with explicit per-line breakdown, mastery bands, prerequisite descriptions.
- **Achievements catalog** — new domain (`GetAchievements`) per the [achievements-plan.md](achievements-plan.md) wire-data contract.
- **Dreamweaver paths** — new domain (`GetDreamPaths`) per the [dreamweavers-path-plan.md](dreamweavers-path-plan.md) wire-data contract.

None of these need new transport infrastructure — they extend message types or add new domains to the same `ContentService`.

## Critical invariants

1. **Legacy metafile generation stays functional in MVP.** No `if (!ContentEnabled) GenerateMetafiles()` short-circuiting yet. The config flag exists for eventual cutover, defaults `true` until then.
2. **Single source of truth.** `ContentService` and the metafile generator both read from `WorldData.Values<T>()`. No content gathering happens twice; no risk of divergence.
3. **UI controls in Chaos.Client use the repository abstraction.** They never directly bind to either metafile or gRPC source. The repository decides which backend served the data.
4. **gRPC failure must not break the client.** Graceful fallback to legacy cache or legacy `0x7B` request. Tested in CI / verification.
5. **Auth tokens scoped to active session.** Expire on disconnect. No long-lived tokens. No persistence of tokens across server restarts.
6. **Schema additive evolution only.** No field renames, no field removals, no number reuse. Additions only, marked optional.

## Migration

The server-side `ContentService` is purely additive — no existing code changes behavior in MVP. Existing characters, existing clients, existing metafile traffic all unchanged. The only risk surface is the extended `PatronServer.Auth` returning a new field (the session token) — additive in the proto, but verify that the existing one-shot login flow on the legacy admin tools still works after the change.

Chaos.Client adoption is its own migration, with per-domain rollback simply by reverting the `MetaFileRepository` delegate for that domain.

There's no per-character data migration — `ContentService` carries catalog data only, no per-player state.

## Forward compatibility

- **Account manager arrival.** When the [Account entity](feature-backlog.md#5d-account-manager-design) lands, `SessionTokenRegistry` switches its key from `connection_id` to `account_guid` for cross-character consistency. Single-line change in the registry; no protocol change.
- **Hot-reload infrastructure.** When the [unified hot-reload mechanism](feature-backlog.md#5b-hot-reload-infrastructure) lands, `ContentService` subscribes to the reload event to bump version stamps and fire `WatchContent` updates. `IXmlReloadable` integration point is one method call.
- **More content domains.** Tooltips, Quest log, Spell/skill panel, Achievements catalog, Dreamweaver path/wallet catalog all join `ContentService` as new endpoints or extensions of existing message types. No new transport infrastructure.
- **Eventual cutover.** When `Content.LegacyMetafilesEnabled = false` in production, server stops generating metafiles. `World.GenerateMetafiles()` and the per-type generators can be removed in a follow-up scope. The `Metafile`/`CompiledMetafile`/`MetafileNode` classes can also be removed at that time.

## What we'd communicate to Chaos.Client

Per the same wire-data discussion documented in [achievements-plan.md](achievements-plan.md) and [dreamweavers-path-plan.md](dreamweavers-path-plan.md). For metafile modernization specifically:

**The replacement contract for current metafile types:**
- `NationDesc` → `GetNations` returning `NationBundle { repeated Nation nations; uint64 version; }` where `Nation` carries id, display name, flag color/identifier.
- `NPCIllust` → `GetNpcIllustrations` returning entries mapping NPC name + variant index → SPF filename.
- `ItemInfo*` → `GetItems` returning a single bundle (no need for the 16-file split — that was a legacy size concern). Per-item: id, name, description, level/ability/class requirements, weight, vendor category.
- `SClass*` → `GetCastables(class)` returning skills + spells learnable by that class with prereqs, descriptions, costs, icons.
- `SEvent*` → `GetQuests` returning quest summaries with title, description, reward text, prerequisite (cookie id today; structured prereq when quest log work lands).
- `Light` → `GetLighting` if server-sourced; otherwise stays client-local.

**The new push contract:**
- `WatchContent` server stream emits `ContentUpdate { domain, new_version, [invalidate | delta] }` whenever XML reloads. Client invalidates cache and re-fetches affected domains.

**Auth contract:**
- Extended `Auth` returns a session token in addition to existing fields. Client stores it and presents it on every `ContentService` call. Server invalidates on legacy socket disconnect.

**Client-side surfaces (out of scope for the server but flagged for coordination):**
- `Chaos.Client/Networking/ContentRepository.cs` — new wrapper around `Grpc.Net.Client` for `ContentService`.
- `MetaFileRepository.cs` — refactored to prefer `ContentRepository` for migrated domains, fall back to legacy metafile cache or `0x7B` request when unavailable.
- `ChaosGame.cs` (lines 110–120, 653, 662–681) — establish gRPC connection after world entry, gate metafile request on gRPC failure for migrated domains.
- `WorldScreen` and downstream UI controls — unchanged. They continue to query the repository abstraction.

**Coordination:** `ContentService` ships server-side without breaking the existing client. Chaos.Client adoption is a separate effort that the client team picks up when ready. The server-side scope can land independently.

## Verification

For the eventual implementation:

1. **Build.** Server compiles after `Content.proto` is added and code-generated; gRPC server starts at boot with both `PatronServer` and `ContentService` registered.
2. **gRPC reachability.** `grpcurl` connects to the gRPC port, lists the new service, calls `GetNations` with a valid session token, returns expected payload.
3. **Auth enforcement.** Same `grpcurl` call without a session token returns `Unauthenticated`. Call with an expired token returns `Unauthenticated`. Successful call refreshes the expiry.
4. **Legacy parity per migrated domain.** For each of the six (or five if Light stays client-local) domains, the gRPC bundle's content matches what the legacy metafile encodes, semantically. Tests assert this directly against `WorldData.Values<T>()`.
5. **Chaos.Client adoption (when client work lands).** Client connects gRPC after world entry, logs success. For the first migrated domain (`Nations`), client UI shows nation names without `0x7B` traffic for that metafile name. Block the gRPC port; client falls back to legacy metafile request and UI continues working.
6. **Hot-reload propagation.** Edit a Nation's display name in XML, trigger reload, server bumps version, `WatchContent` subscriber receives `ContentUpdate`, client re-fetches and UI updates without relog.
7. **Cutover dry-run.** Set `Content.LegacyMetafilesEnabled = false` in a test config. Server skips `GenerateMetafiles()` at boot. `0x7B` requests return empty. Client (with `ContentRepository` enabled for all migrated domains) functions normally. (This verifies the post-cutover state without committing to it.)
8. **Restart resilience.** Restart server during a `WatchContent` stream; client's stream errors, client re-establishes after reconnect, version stamps re-sync from scratch (server's reset to 1, client's known versions are stale → full re-fetch). UI does not break.
9. **Tests.** `dotnet test --filter Content` passes.

## Out of scope / follow-ups

- **Removing legacy metafile generation entirely.** Future scope after sustained gRPC stability with real players. Removes `Metafile.cs`, `CompiledMetafile.cs`, `MetafileNode.cs`, `World.GenerateMetafiles()` and per-type generators, `PacketHandler_0x7B_RequestMetafile`. Estimated medium scope; mostly deletion.
- **`Light` metafile migration.** Pending verification of whether it's server-sourced or DA-data-derived.
- **Chaos.Client team coordination.** Flagged here; the actual client-side adoption is a separate effort with its own scope.
- **Multi-server / cross-shard content distribution.** Single-server scope only.
- **Live authoring tooling** (XML edit → broadcast). Infrastructure supports it via `WatchContent`; building the editor UI is separate.
- **Per-entity ETag granularity.** Per-domain version stamp is sufficient for current content sizes. Revisit if a domain grows to thousands of entries.
- **Backlog content schemas (Tooltips, Quest log, Spell/skill panel, Achievements catalog, Dreamweaver paths).** Each has its own scope doc; this work is the foundation.
