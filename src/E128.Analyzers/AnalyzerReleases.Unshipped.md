### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
E128006 | Style | Warning | Use Encoding.UTF8 instead of Encoding.Default
E128007 | Design | Warning | Avoid async void methods (non-event-handler)
E128008 | Design | Warning | Avoid sync-over-async (.Result / .GetAwaiter().GetResult())
E128009 | Performance | Warning | Use MinBy/MaxBy instead of OrderBy().First()
E128010 | Performance | Warning | HttpClient call missing HttpCompletionOption.ResponseHeadersRead
E128011 | Reliability | Warning | [GeneratedRegex] attribute is missing matchTimeoutMilliseconds
E128012 | Reliability | Warning | RegexOptions.Compiled is redundant in [GeneratedRegex]
E128013 | Reliability | Warning | [GeneratedRegex] pattern has overlapping quantifiers
E128014 | Reliability | Warning | [GeneratedRegex] pattern has nested quantifiers
