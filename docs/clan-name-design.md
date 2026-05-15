# Clan Name Design

Clan name is an **account-level** property, not related to the guild system. All characters on the same account share the same clan name.

**Why:** Clans are a meta/social concept separate from in-game guilds. A player's clan identity persists across all their characters.

**Dependencies:**
- Requires Account entity to exist (currently AccountGuid on User is always Guid.Empty)
- Clan name lives on the Account, not on User
- Characters inherit clan name from their linked account

**Display:**
- Add a new `ClanName` field to DisplayUser packet (0x33) — separate from `GroupName`
- `GroupName` stays for party recruitment (both displayed, separate concerns)
- Since we own the client (Chaos.Client), extending the packet format is fine
- SelfProfile (0x39) can also show clan name — separate from GuildName/GuildRank

**How to apply:** Blocked on Account entity creation. Once accounts exist with a ClanName property, the server populates it in DisplayUser from the character's linked account. Client reads the new field and renders it.
