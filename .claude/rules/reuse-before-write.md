# Reuse Before Write

**Before writing new utility code, search for existing implementations.** Duplicate code is a maintenance burden.

**Mandatory pre-write search** — before implementing any of these, search first:

| Writing...                    | Search first with                              |
| ----------------------------- | ---------------------------------------------- |
| String helpers / extensions   | `rg "static.*string" src/ -g "*.cs"`           |
| Path manipulation utilities   | `rg "Path\.\|FileInfo" src/`                   |
| Collection helpers            | `rg "static.*IEnumerable" src/`                |
| HTTP / fetching               | `rg "HttpClient\|IHttpClientFactory" src/`     |

**Use `rg` or `fd` for these lookups** — they're fast and cheap.
