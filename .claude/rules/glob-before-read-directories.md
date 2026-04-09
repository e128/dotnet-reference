# Glob Before Read on Uncertain Paths

If a path might be a directory, use Glob or `ls` to verify it's a file before calling Read. The Read tool errors with EISDIR on directories.

**Also apply to any dynamically-constructed path.** If the path is assembled from variables, verify it exists before reading.
