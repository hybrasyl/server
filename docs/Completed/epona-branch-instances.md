# Epona Branch-Aware Server Instances

Epona (the Hybrasyl launcher) manages server instances that can build and run from specific git branches of both the server repo and the Hybrasyl.Xml repo. This enables running multiple server branches simultaneously without manual worktree juggling or csproj editing.

**Why:** Developing features that span both the server and XML repos (e.g., new element types, schema changes) requires building the server against a local XML branch instead of the published NuGet package. Doing this manually means editing the csproj, managing git worktrees, and remembering to undo it all — error-prone and tedious.

## Server-side support

The server's only responsibility is accepting a local Hybrasyl.Xml project reference when asked. This is done via a conditional in `hybrasyl/Hybrasyl.csproj`:

```xml
<PackageReference Include="Hybrasyl.Xml" Version="0.9.4.11"
                  Condition="'$(UseLocalXml)' != 'true'" />
<ProjectReference Include="$(LocalXmlProjectPath)"
                  Condition="'$(UseLocalXml)' == 'true'" />
```

When `UseLocalXml` is unset (the default), the NuGet package is used — zero behavior change for CI, production, or developers not using Epona.

When Epona creates a server instance with a local XML branch, it generates a gitignored `Directory.Build.props` in the server worktree root:

```xml
<Project>
  <PropertyGroup>
    <UseLocalXml>true</UseLocalXml>
    <LocalXmlProjectPath>E:\Dark Ages Dev\Repos\xml\.worktrees\some-branch\src\Hybrasyl.Xml.csproj</LocalXmlProjectPath>
  </PropertyGroup>
</Project>
```

MSBuild auto-imports this from the project's ancestor directories.

## Worktree strategy

Epona uses `git worktree` to isolate branches:

- Server worktrees: `server/.worktrees/<sanitized-branch-name>/`
- XML worktrees: `xml/.worktrees/<sanitized-branch-name>/`

Both `.worktrees/` directories are gitignored. Two instances on the same branch share one worktree. Worktrees are cleaned up when no instance references them.

## Instance config (in Epona settings)

Each instance specifies: server repo path, server branch (null = current checkout), XML repo path (null = use NuGet), XML branch, world data dir, Redis host:port, port triplet. Mode can be "repo" (branch-aware) or "binary" (prebuilt, no branch fields).

## How to apply

Server repo changes are minimal — the conditional csproj and two `.gitignore` entries. All worktree management, build override generation, and UI live in the Epona repo (see `epona/docs/multi-target-expansion-plan.md` Stage 3).
