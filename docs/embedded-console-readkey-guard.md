# Skip ReadKey When Stdin Is Redirected

The fatal-exit `Console.ReadKey()` pause in `Game.cs` now only runs when stdin is a real console. When Epona (or any wrapper) launches the server with piped stdio, the server exits immediately on `World.Init()` failure instead of throwing `InvalidOperationException`.

**Why:** Epona's embedded-console mode pipes the server's stdout/stderr into the launcher's log pane instead of a separate Win32 console window. `Console.ReadKey()` throws on a redirected stdin, which would crash the server on any `World.Init()` failure when launched embedded. The fix is a two-line guard; no API change, no new dependencies.

**Behavior:**
- **Standalone console launch** (operator running `dotnet Hybrasyl.dll` in a terminal): unchanged — still prints "Press any key to exit." and waits for input
- **Wrapped launch** (Epona, CI, any stdin-redirecting parent): skips the pause, exits with code 1; the wrapper surfaces the fatal log line in its own UI

**Change:** `hybrasyl/Game.cs:614-622` — wrap the `Log.Fatal("Press any key...")` + `Console.ReadKey()` lines in `if (!Console.IsInputRedirected)`. The `Environment.Exit(1)` stays unconditional.

**How to apply:** This is the sole upstream dependency for Epona's embedded-console mode (see `epona/docs/embedded-server-console-plan.md`). It's the only `ReadKey` call in the `hybrasyl/` tree and has no test coverage today; verified manually by running the server with a bad `--datadir` both in a terminal (pauses) and piped through `echo | dotnet …` (exits cleanly).
