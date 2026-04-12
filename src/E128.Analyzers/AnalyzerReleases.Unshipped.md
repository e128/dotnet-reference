### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
E128006 | Style | Warning | Use Encoding.UTF8 instead of Encoding.Default
E128007 | Design | Warning | Avoid async void methods (non-event-handler)
E128008 | Design | Warning | Avoid sync-over-async (.Result / .GetAwaiter().GetResult())
E128009 | Performance | Warning | Use MinBy/MaxBy instead of OrderBy().First()
E128010 | Performance | Warning | HttpClient call missing HttpCompletionOption.ResponseHeadersRead
