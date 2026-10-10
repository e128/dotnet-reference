# opencode v2 Guardrail Parity
*Updated: 2026-10-10T14:53:02Z*

Claude Code enforces guardrails through hook scripts under `.claude/hooks/`.
opencode v2 has no hook file. Its plugin API carries the same interception.
This file records the mapping and the current coverage.

The capability map lives in
[claude-code-maintenance.md](claude-code-maintenance.md). The approval policy
lives in `.claude/rules/auto-approvals.md`. The deterministic script routing
that these guardrails protect lives in `.claude/rules/deterministic-scripts.md`.

## Plugin Location

`.opencode/plugins/e128-guardrails/index.ts` loads with no config entry.
opencode discovers the directory automatically.

The plugin exports a plain object with `id` and `setup`. It needs no import
from `@opencode/plugin`, so the repo carries no plugin package manifest and no
`node_modules`.

## Single Source of Truth

The plugin never restates a guardrail rule. It builds the Claude Code hook JSON
payload and runs the existing script:

```ts
spawnSync("bash", [join(root, ".claude/hooks", name)], {
  input: JSON.stringify({ tool_name: "Bash", tool_input: { command } }),
  encoding: "utf8",
  cwd: root,
  timeout: 5000,
})
```

A hook blocks by exiting with status 2 and writing the reason to stderr. A
status of null means a signal or a timeout. The plugin treats that as no
verdict rather than as a block.

The CI job named `harness` fails when the plugin names a hook script that does
not exist. A renamed script cannot silently disable a guardrail.

## Blocking Guardrails

| Hook                 | opencode permission action | Tool name passed | Rules it carries                        |
| -------------------- | ------------------------- | ---------------- | --------------------------------------- |
| `block-raw-git`      | `shell`                   | `Bash`           | Raw git commands with a script          |
| `block-raw-commands` | `shell`                   | `Bash`           | Raw dotnet commands and bare `python3`  |
| `pre-tool-use-guard` | `edit`                    | `Edit`           | Writing `settings.json`                 |

## Nonblocking Guardrails

| Hook                          | opencode hook                   | Effect                                 |
| ----------------------------- | ------------------------------- | -------------------------------------- |
| `post-tool-use-memory-sync`   | `tool.hook` after a write       | The script writes the event log itself |
| `post-format-reread-reminder` | `tool.hook` after a shell call  | Stores a one-shot reminder string      |
| `init-session`                | `session.hook` on context       | Pushes stdout into the system prompt   |

`init-session` runs once per session ID. A later context call in the same
session skips it, so a long session does not re-inject the block on every
request.

## Unmapped Hooks

| Hook                        | Reason                                                                              |
| --------------------------- | ----------------------------------------------------------------------------------- |
| `validate-agent-frontmatter` | It never blocks, only warns. Running `git diff --cached` on every shell call costs more than the warning returns |
| `user-prompt-submit`         | The script reads raw prompt text and JSON from the same stdin, so its context hint never fires |
| `stop-failure`               | opencode v2 has no stop failure event                                              |

## Permission Hook Contract

- The hook runs for `allow` and `ask` decisions.
- An explicit configured `deny` is final and skips the hook.
- The hook may change `effect` to `allow`, `ask`, or `deny`.
- `message` becomes the denial reason.
- Hooks run in plugin registration order, so a later hook sees earlier changes.

The base policy of opencode allows every action, so the plugin hook runs on
every shell and edit call. Each blocker spawns one short bash process. The cost
stays far below one model round trip.

## Shell Hook Contract

`ctx.shell.hook("create.before")` edits the command, the working directory, the
timeout, the executable, and the environment before the shell runs. It cannot
stop the command. Use a permission hook to block.

## Approval Parity

`.claude/rules/auto-approvals.md` owns the policy. Both harnesses enforce the
same three tiers in their own syntax:

| Tier | Claude Code        | opencode                           |
| ---- | ------------------ | ---------------------------------- |
| auto | `permissions.allow` | base policy of allow               |
| ask  | `permissions.ask`  | `permissions` with `effect` of ask |
| deny | `permissions.deny` | `experimental.policies`            |

Claude Code evaluates deny, then ask, then allow. opencode evaluates the array
in order and the last match wins. A `git push --force` command matches the ask
rule and the deny rule on both harnesses. The deny rule wins on both.

The base posture differs by design. Claude Code denies an unlisted Bash command
by default. opencode allows every action by default. Closing that gap on
opencode means enumerating every shell pattern, which costs more than it
returns.

## Model Selection

The repo `opencode.json` pins no model. A model default is a personal choice,
and a committed default forces that choice on every clone. The global config
file `~/.config/opencode/opencode.json` holds the `model` key instead. The
`lode-opencode.nu` wrapper reads it from there.

## Invariants

- `.claude/hooks/*.sh` stays the only copy of a guardrail decision.
- A plugin reads the decision from the hook script or from the rule file. It
  never restates the rule in TypeScript.
- A guardrail never edits files. It denies, asks, or injects text.
- Every new Claude Code hook needs a line in a table above.

## Lessons

- A missing harness feature is not a missing capability. Check the plugin API
  before a lode file records a gap.
- A lode claim that names a harness limitation needs a documentation
  citation. This file replaced an uncited claim that opencode has no hook
  system.
- Verify a guardrail by tripping it. A guardrail that blocks the test that
  exercises it is the strongest proof available.
- A drift check needs a test that uses the same character set as the real
  input. An injected uppercase letter can pass a lowercase pattern.
- Read a guardrail script in full before documenting it. A partial read hid
  the `python3` rule until the plugin blocked a command during verification.