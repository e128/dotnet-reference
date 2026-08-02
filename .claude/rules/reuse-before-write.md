# Reuse Before Write

**Search for an existing implementation before you write new utility code.**
Duplicate code is a maintenance burden.

**Mandatory pre-write search.** Search first before you implement any of these:

| Writing...                  | Search first with                        |
| --------------------------- | ----------------------------------------- |
| String helpers or extensions| `rg "static.*string" src/ -g "*.cs"`     |
| Path manipulation utilities | `rg "Path\.\|FileInfo" src/`             |
| Collection helpers          | `rg "static.*IEnumerable" src/`          |
| HTTP or fetching            | `rg "HttpClient\|IHttpClientFactory" src/`|

Use `rg` or `fd` for these lookups. Both are fast and cheap.
