# No Inline Python

**Never invoke `python3` directly in Bash.**

- For URL fetching: use the `WebFetch` tool
- For JSON parsing: use `jq` or read the output directly
- For local data processing: use bash scripts or dedicated `scripts/*.sh`
