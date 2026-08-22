# opencode -- Ollama Provider Setup
*Updated: 2026-08-22T17:35:13Z*

One-time local setup that lets `scripts/lode-opencode.nu` run this repo's
composed lode prompt against a local Ollama model through opencode. See
[claude-code-maintenance.md](claude-code-maintenance.md) for the full
Harness Portability Capability Map; this file covers only the local
provider config that map does not.

## Config File

opencode reads provider configuration from `~/.config/opencode/opencode.json`.
Add a custom `@ai-sdk/openai-compatible` provider pointed at the local Ollama
server:

```json
{
  "$schema": "https://opencode.ai/config.json",
  "provider": {
    "ollama": {
      "npm": "@ai-sdk/openai-compatible",
      "name": "Ollama (local)",
      "options": {
        "baseURL": "http://localhost:11434/v1"
      },
      "models": {
        "qwen3.8:27b-mlx": {
          "name": "Qwen3.8 27B (MLX, local)"
        }
      }
    }
  }
}
```

Add one entry under `models` per pulled Ollama model the launcher targets.
The default model in `scripts/lode-opencode.nu` must have a matching key
here.

## Why This Shape

- opencode has no first-party Ollama provider. The official integration path
  is a generic OpenAI-compatible provider pointed at Ollama's OpenAI-compatible
  endpoint (`/v1`), confirmed against opencode's own Ollama documentation.
- `opencode run` has no `--append-system-prompt`-equivalent flag. The launcher
  works around this by composing the lode prompt into the run message body
  instead of a dedicated system-prompt flag. See
  [lode-opencode.nu](../../scripts/lode-opencode.nu) and
  [lode-opencode-lib.nu](../../scripts/lode-opencode-lib.nu).

## Related

- [claude-code-maintenance.md](claude-code-maintenance.md) -- capability map and rule-ownership table
- [lode-opencode.nu](../../scripts/lode-opencode.nu) / [lode-opencode-lib.nu](../../scripts/lode-opencode-lib.nu) -- the launcher pair
- [lode-lib.nu](../../scripts/lode-lib.nu) -- shared prompt composition, reused across the `lode-*` launchers
- `AGENTS.md` -- the portable prompt content opencode receives (no Claude-Code-only mechanics)
