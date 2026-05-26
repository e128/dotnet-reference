### New Rules

Rule ID  | Category    | Severity | Notes
---------|-------------|----------|-------
E128076  | Reliability | Warning  | Materialize QuerySelectorAll result before iterating
E128077  | Reliability | Warning  | TextContent string match requires a preceding length guard
E128078  | Reliability | Warning  | GetAttribute("href") on element that does not carry href
E128079  | Reliability | Warning  | CompositeDetection with single generic ID selector lacks specificity
E128080  | Design      | Error    | Use ByteSize for data-size values to avoid unit ambiguity
E128081  | Performance | Info     | Use StringBuilderPool instead of new StringBuilder()
E128082  | Design      | Warning  | Do not unwrap ByteSize via cast
E128083  | Performance | Warning  | Use ImmutableCollectionsMarshal.AsImmutableArray instead of ImmutableArray.Create(x.ToArray())
E128084  | Performance | Warning  | Use CollectionsMarshal.AsSpan with Slice instead of List.GetRange
E128085  | Performance | Warning  | Use foreach+AddRange instead of SelectMany.ToList
E128086  | Reliability | Warning  | ArrayPool buffer used as SqliteParameter value without Size
E128087  | Design      | Warning  | Static numeric field should not be incremented with ++/--
E128089  | Reliability | Warning  | Bare .Parse() call without TryParse
E128090  | Maintainability | Info     | Reflection in test methods — use public API instead — use TryParse to avoid FormatException
