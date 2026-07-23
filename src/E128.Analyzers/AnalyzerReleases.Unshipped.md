### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|------
E128093 | Reliability | Warning  | Use async DbConnection/DbCommand *Async overload instead of sync method (outside already-async methods)
E128096 | Reliability | Warning  | Detect synchronous call to async local function via .Result or .Wait()
