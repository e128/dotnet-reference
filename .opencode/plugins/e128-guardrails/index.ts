// e128-guardrails: mirror the Claude Code guardrail hooks on opencode v2.
//
// The shell scripts under .claude/hooks/ stay the single source of truth for
// every guardrail decision. This plugin only translates an opencode event into
// the Claude Code hook JSON contract and reports the verdict back.
//
// PreToolUse guard -> ctx.permission.hook("evaluate")
// PostToolUse hook -> ctx.tool.hook("execute.after")
// SessionStart hook -> ctx.session.hook("context"), once per session

import { spawnSync } from "node:child_process"
import { join } from "node:path"

// Claude Code PreToolUse hooks that can block. Each exits 2 on a block.
const SHELL_BLOCKERS = ["block-raw-git", "block-raw-commands"]
const EDIT_BLOCKERS = ["pre-tool-use-guard"]

const TIMEOUT_MS = 5000

function runHook(root: string, name: string, payload: unknown) {
  const file = join(root, ".claude", "hooks", name)
  const result = spawnSync("bash", [file], {
    input: JSON.stringify(payload),
    encoding: "utf8",
    cwd: root,
    timeout: TIMEOUT_MS,
  })
  // status null means a signal or a timeout. Treat a crash as no verdict.
  return {
    blocked: result.status === 2,
    stdout: result.stdout ?? "",
    stderr: result.stderr ?? "",
  }
}

// opencode exposes the command or the path under a lowercase tool id.
function shellPayload(resource: string) {
  return { tool_name: "Bash", tool_input: { command: resource } }
}

function pathPayload(resource: string, tool: string) {
  return { tool_name: tool, tool_input: { file_path: resource } }
}

export default {
  id: "e128-guardrails",
  async setup(ctx: any) {
    const root = ctx.location.directory
    let pendingReminder = ""
    const seenSessions = new Set<string>()

    // Blocking guardrails. opencode evaluates every shell and edit action
    // because the base policy is allow. A deny here is final for the tool.
    await ctx.permission.hook("evaluate", async (event: any) => {
      if (event.action === "shell") {
        for (const command of event.resources) {
          for (const name of SHELL_BLOCKERS) {
            const verdict = runHook(root, name, shellPayload(command))
            if (verdict.blocked) {
              event.effect = "deny"
              event.message = verdict.stderr.trim()
              return
            }
          }
        }
      }

      if (event.action === "edit") {
        for (const path of event.resources) {
          for (const name of EDIT_BLOCKERS) {
            const verdict = runHook(root, name, pathPayload(path, "Edit"))
            if (verdict.blocked) {
              event.effect = "deny"
              event.message = verdict.stderr.trim()
              return
            }
          }
        }
      }
    })

    // Logging and reminders. These hooks never block.
    await ctx.tool.hook("execute.after", async (event: any) => {
      if (event.status !== "completed") return

      if (event.tool === "write") {
        const path = event.input?.filePath ?? event.input?.file_path
        if (path) runHook(root, "post-tool-use-memory-sync", pathPayload(path, "Write"))
        return
      }

      if (event.tool === "bash") {
        const command = event.input?.command
        if (!command) return
        const verdict = runHook(root, "post-format-reread-reminder", shellPayload(command))
        if (verdict.stdout.trim()) pendingReminder = verdict.stdout.trim()
      }
    })

    // Session context. The reminder is one-shot. The session block runs once
    // per session so a long session does not re-inject it on every request.
    await ctx.session.hook("context", async (event: any) => {
      if (pendingReminder) {
        event.system.push({ type: "text", text: pendingReminder })
        pendingReminder = ""
      }

      if (seenSessions.has(event.sessionID)) return
      seenSessions.add(event.sessionID)

      const verdict = runHook(root, "init-session", { cwd: root })
      if (verdict.stdout.trim()) {
        event.system.push({ type: "text", text: verdict.stdout.trim() })
      }
    })
  },
}