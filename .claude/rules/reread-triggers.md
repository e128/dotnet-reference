# Re-Read Triggers

The canonical re-read rule lives in [read-before-edit.md](read-before-edit.md). After `format.sh`/`dotnet format`, a `check.sh` run that applied fixes, a sub-agent writing to files, or context compaction, **re-Read files before editing** — cached contents may be stale.
