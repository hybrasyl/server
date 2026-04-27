# Account Manager Scoping

Current state of accounts in Hybrasyl:

**What exists:**

- `User.AccountGuid` (Guid, always Guid.Empty) — `User.cs:94`
- `User.Account` property — commented out at `User.cs:100`
- `AuthInfo` — per-character password/login state — `Objects/AuthInfo.cs`
- `Vault` already checks AccountGuid for sharing — `Subsystems/Players/Vault.cs:129-130`
- `GuidReference` has AccountGuid field — `Internals/GuidReference.cs`
- gRPC Patron service with basic auth — `grpc/PatronServer.cs`, `protos/Patron.proto`

**What's missing:**

- Account entity class (owns multiple characters, stores clan name, email, etc.)
- Account-level authentication (login with account, then pick character)
- Character selection/listing after account login
- AccountGuid population during character creation
- Account management gRPC endpoints
- Vault/mailbox sharing activation (infrastructure exists, needs AccountGuid)

**Blocked decisions:**

- Where should the account service live? (Extend Patron gRPC vs separate service — needs more scoping)
- What account-level data beyond clan name? (email, 2FA, billing, ban status?)
- Character limits per account?
- Migration path for existing characters without accounts?

**How to apply:** This needs a dedicated design discussion before implementation. The clan name feature depends on this.
