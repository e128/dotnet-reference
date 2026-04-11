## Release 1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
E128001 | Design | Warning | Use FileInfo or DirectoryInfo instead of string for file system paths
E128002 | Style | Warning | Use string.Empty instead of ""
E128003 | Reliability | Warning | Use TimeProvider instead of DateTime/DateTimeOffset direct access
E128004 | Reliability | Warning | Use IHttpClientFactory instead of new HttpClient()
E128005 | Design | Warning | Seal classes that have no subclasses
