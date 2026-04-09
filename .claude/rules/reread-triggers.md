# Re-Read Triggers

After any of these events, **re-read files before editing** — contents may have changed:

| Trigger                      | Reason                                         |
| ---------------------------- | ---------------------------------------------- |
| `format.sh --changed` ran   | Reformats in-place, invalidates cached content |
| `check.sh` ran              | May apply format fixes                         |
| Context compaction           | Drops file contents from memory                |
| Sub-agent wrote to files     | External modification                          |
