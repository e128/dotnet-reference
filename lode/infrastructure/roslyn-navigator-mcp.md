# Roslyn Navigator MCP
*Updated: 2026-11-08T13:43:23Z*

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

The package requires `net10.0`. It matches the SDK that `global.json` pins.

## Configuration

Both configuration files name the same command. The command starts the server
on standard input and output.

```text
dotnet cwm-roslyn-navigator --solution .
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
        "command": ["dotnet", "cwm-roslyn-navigator", "--solution", "."]
      }
    }
  }
}
```

The working directory defaults to the workspace root. Keep it unset. A fixed
working directory breaks solution discovery for a worktree.

Claude Code asks for approval before it starts a server from `.mcp.json`.
`.claude/settings.json` lists the server in `enabledMcpjsonServers`, so the
prompt appears once per machine. Add a server name to that list when you add
a server to `.mcp.json`.

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
- The tool writes log messages to standard error. The MCP client keeps them out
  of the model context.

## See Also

- [claude-code-maintenance.md](claude-code-maintenance.md): harness capability map
- [opencode-guardrails.md](opencode-guardrails.md): opencode plugin and hook contracts
