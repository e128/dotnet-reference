# Shared argument parsing for lode wrapper scripts.
# Usage: use lode-lib.nu [parse-lode-args load-system-prompt]

# Load SystemPrompt.txt from prompts/ (sibling of scripts/).
export def load-system-prompt []: nothing -> string {
    let path = $env.CURRENT_FILE | path dirname | path dirname | path join "prompts" "SystemPrompt.txt"
    if not ($path | path exists) {
        print $"Error: SystemPrompt.txt not found at ($path)"
        exit 1
    }
    open $path | str trim
}

# Parse lode wrapper arguments. Extracts --model and --append-system-prompt flags.
# Returns { prompt: string, model: string, claude_args: list<string> }.
export def parse-lode-args [
    base_prompt: string     # Base system prompt text
    default_model: string   # Default model (empty string = no --model flag)
    ...args: string         # Raw arguments to parse
]: nothing -> record {
    mut model = $default_model
    mut combined_prompt = $base_prompt
    mut claude_args = []
    mut i = 0

    while $i < ($args | length) {
        let arg = $args | get $i

        if $arg == "--model" {
            if ($i + 1) < ($args | length) {
                $model = $args | get ($i + 1)
                $i = $i + 2
            } else {
                print "Error: --model requires a value"
                exit 1
            }
        } else if ($arg | str starts-with "--model=") {
            $model = $arg | str replace "--model=" ""
            $i = $i + 1
        } else if $arg == "--append-system-prompt" {
            if ($i + 1) < ($args | length) {
                let append_text = $args | get ($i + 1)
                $combined_prompt = $combined_prompt + "\n\n" + $append_text
                $i = $i + 2
            } else {
                print "Error: --append-system-prompt requires a value"
                exit 1
            }
        } else if ($arg | str starts-with "--append-system-prompt=") {
            let append_text = $arg | str replace "--append-system-prompt=" ""
            $combined_prompt = $combined_prompt + "\n\n" + $append_text
            $i = $i + 1
        } else {
            $claude_args = $claude_args ++ [$arg]
            $i = $i + 1
        }
    }

    { prompt: $combined_prompt, model: $model, claude_args: $claude_args }
}
