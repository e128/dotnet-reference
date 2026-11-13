# Roslyn Navigator MCP
*Updated: 2026-11-13T12:52:46Z*

The `cwm-roslyn-navigator` .NET tool serves a Model Context Protocol server.
It loads the solution with Roslyn and answers structural questions about the
code. It is a local stdio server. It is configured for both harnesses.

## Install

The tool is a repository-local .NET tool. The manifest is `dotnet-tools.json`
at the repository root.

```sh
dotnet tool restore
```

To install the tool after a clone, run `dotnet tool restore`. That command
reads the manifest and installs the pinned version.

To change the pinned version, run:

```sh
dotnet tool update cwm.roslynnavigator
```

`dotnet tool install` fails once the manifest already names the tool.

The package targets `net10.0` and its runtime config sets no `rollForward`
policy. The repository pins a .NET 11 SDK in `global.json`, so the tool needs
major-version roll forward to start. The manifest entry sets
`"rollForward": true`, and `dotnet tool run` turns that into
`--roll-forward Major` for the host. Without the setting the process exits
before the stdio handshake, and the client reports `CONNECTION_CLOSED`.

Prove the setting reaches the host with a host trace:

```sh
COREHOST_TRACE=1 COREHOST_TRACEFILE=trace.txt dotnet tool run cwm-roslyn-navigator -- --help
```

The trace reports `version_compatibility_range=major`. A `minor` value means
the manifest setting is missing.

## Configuration

Both configuration files name the same command. The command starts the server
on standard input and output.

```text
dotnet tool run cwm-roslyn-navigator -- --solution .
```

The `--solution .` argument makes the server discover the solution file in the
working directory. The repository has `E128.Reference.slnx`.

| Harness     | File            | Key           |
| ----------- | --------------- | ------------- |
| opencode v2 | `opencode.json` | `mcp.servers` |
| Claude Code | `.mcp.json`     | `mcpServers`  |

opencode v2 entry:

```jsonc
{
  "mcp": {
    "servers": {
      "cwm-roslyn-navigator": {
        "type": "local",
        "command": ["dotnet", "tool", "run", "cwm-roslyn-navigator", "--", "--solution", "."]
      }
    }
  }
}
```

The working directory defaults to the workspace root. Keep it unset. A fixed
working directory breaks solution discovery for a worktree.

Claude Code asks for approval before it starts a server from `.mcp.json`.
The approval is stored per machine, so the prompt appears once. Add a server
name to `enabledMcpjsonServers` in `.claude/settings.json` to start it without
a prompt.

## Tools

The server exposes tools for project graph reads, symbol lookup, caller and
reference search, type hierarchy, public API, dead code, circular
dependencies, diagnostics, anti-pattern detection, and a heuristic test
coverage map. Read `get_project_graph` first. It confirms that the workspace
loaded the expected solution.

The server returns `TotalFound` when a result set is capped. Re-query with a
higher `maxResults` when the count exceeds the returned rows.

## Verification

```sh
dotnet tool list
opencode mcp list
```

A healthy server reports `connected`. Call `get_project_graph` and confirm the
project list matches `E128.Reference.slnx`.

## Failure Modes

- A missing tool manifest fails `dotnet tool run`. Run `dotnet tool restore`.
- A missing solution file fails workspace load. Pass the solution path instead
  of `.`.
- A stale manifest loads an old tool. Reinstall the tool in the repository.
- The client reports `CONNECTION_CLOSED` when no .NET 10 runtime is installed.
  Set `"rollForward": true` on the manifest entry, restore the tool, then
  reconnect.
- The tool writes log messages to standard error. The MCP client keeps them out
  of the model context.

## See Also

- [claude-code-maintenance.md](claude-code-maintenance.md): harness capability map
- [opencode-guardrails.md](opencode-guardrails.md): opencode plugin and hook contracts
