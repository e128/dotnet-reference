# Bash Full Reference

Extended reference material for Bash scripting. For core patterns and gotchas, see the main SKILL.md.

## Data Types

Bash is untyped — everything is a string. Numeric operations use arithmetic expansion.

| Concept       | Syntax                             | Example                        |
|---------------|------------------------------------|---------------------------------|
| String        | `"hello"`, `'raw'`                | `local name="world"`           |
| Integer math  | `$(( expr ))`                      | `result=$(( 5 + 3 ))`         |
| Array         | `( items )`                        | `local -a arr=(1 2 3)`        |
| Assoc. array  | `declare -A`                       | `declare -A map=([k]=v)`      |
| Boolean       | convention: `true`/`false` strings | `local flag=true`             |

## Control Flow

### if/elif/else

```bash
if [[ "$x" -gt 0 ]]; then
  echo "positive"
elif [[ "$x" -eq 0 ]]; then
  echo "zero"
else
  echo "negative"
fi
```

### case (pattern matching)

```bash
case "$val" in
  start)  echo "starting" ;;
  stop)   echo "stopping" ;;
  *)      echo "unknown: $val" ;;
esac
```

### Loops

```bash
# For loop over items
for item in a b c; do
  echo "$item"
done

# For loop over range
for i in $(seq 1 10); do
  echo "$i"
done

# C-style for loop
for (( i=0; i<10; i++ )); do
  echo "$i"
done

# While loop
while read -r line; do
  echo "$line"
done < file.txt

# Until loop
until [[ "$status" == "ready" ]]; do
  sleep 1
  status=$(check_status)
done
```

`break` and `continue` work as expected.

### Prefer pipelines and built-ins over loops

```bash
# Preferred: pipeline
grep -r "pattern" src/ | wc -l

# Preferred: built-in iteration
find src/ -name "*.cs" -exec grep -l "pattern" {} +

# Avoid: manual loop when a pipeline works
```

### Error handling

```bash
# Trap for cleanup
trap 'echo "Error on line $LINENO" >&2' ERR

# Try/catch equivalent
if ! some_command; then
  echo "Command failed" >&2
  exit 1
fi

# Or with set -e, use || for recovery
risky_command || { echo "Failed" >&2; exit 1; }
```

## Pipelines

Bash pipelines connect stdout of one command to stdin of the next.

```bash
# Chain commands
ls -la | sort -k5 -n | head -5

# Process substitution (avoid temp files)
diff <(sort file1) <(sort file2)

# Capture output
output=$(command 2>&1)

# Discard stderr
command 2>/dev/null

# Redirect both stdout and stderr
command &> output.txt
```

## File Operations

```bash
# Read file contents
content=$(<file.txt)

# Read line by line
while IFS= read -r line; do
  echo "$line"
done < file.txt

# Write to file
echo "content" > file.txt

# Append to file
echo "more" >> file.txt

# Check file exists
[[ -f "path/to/file" ]] && echo "exists"

# Check directory exists
[[ -d "path/to/dir" ]] && echo "exists"

# Find files by pattern
find src/ -name "*.cs" -type f
```

## String Operations

```bash
# Length
echo "${#string}"

# Substring
echo "${string:0:5}"      # first 5 chars
echo "${string:3}"         # from position 3

# Replace first occurrence
echo "${string/old/new}"

# Replace all occurrences
echo "${string//old/new}"

# Remove prefix
echo "${string#prefix}"    # shortest match
echo "${string##prefix}"   # longest match

# Remove suffix
echo "${string%suffix}"    # shortest match
echo "${string%%suffix}"   # longest match

# Upper/lowercase (Bash 4+)
echo "${string^^}"         # uppercase
echo "${string,,}"         # lowercase
```

## Arrays

```bash
# Indexed array
local -a arr=("one" "two" "three")

# Access
echo "${arr[0]}"           # first element
echo "${arr[@]}"           # all elements
echo "${#arr[@]}"          # length
echo "${!arr[@]}"          # all indices

# Append
arr+=("four")

# Slice
echo "${arr[@]:1:2}"       # elements 1-2

# Iterate
for item in "${arr[@]}"; do
  echo "$item"
done

# Associative array (Bash 4+)
declare -A map
map[key]="value"
echo "${map[key]}"
```

## Functions

```bash
# Function definition
my_function() {
  local arg1="$1"
  local arg2="${2:-default}"

  # Return value via stdout
  echo "result"
}

# Call and capture
result=$(my_function "hello" "world")

# Return status code
check_status() {
  if [[ -f "$1" ]]; then
    return 0
  else
    return 1
  fi
}

# Use return status
if check_status "file.txt"; then
  echo "exists"
fi
```

## Subcommands in Scripts

```bash
#!/usr/bin/env bash
set -euo pipefail

cmd_run() { echo "running"; }
cmd_build() { echo "building"; }
cmd_help() { echo "Usage: script.sh <run|build>"; }

case "${1:-help}" in
  run)   cmd_run ;;
  build) cmd_build ;;
  *)     cmd_help ;;
esac
```

## Useful Built-ins

| Command | Purpose |
|---------|---------|
| `local` | Declare function-scoped variable |
| `readonly` | Declare constant |
| `declare` | Declare variable with attributes |
| `shift` | Remove first positional parameter |
| `set` | Set shell options or positional params |
| `trap` | Set signal/exit handlers |
| `getopts` | Parse short options |
| `printf` | Formatted output (prefer over echo for portability) |
| `read` | Read input (use `-r` to prevent backslash interpretation) |
| `mapfile` | Read lines into array (Bash 4+) |
