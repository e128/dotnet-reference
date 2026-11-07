using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Design;

/// <summary>
///     E128218: Flags a type that uses several mutable bool fields as one state.
///     The rule reports a co-write or a co-read, and it ignores one-way flags,
///     constructor writes, and flags that never take both values.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ImplicitStateMachineAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128218";
    internal const string OptionKey = "e128_min_state_flags";

    private const int DefaultMinFlags = 3;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Boolean flags encode one state",
        "'{0}' uses {1} boolean flags as one state: {2}. Use one enum and one transition method.",
        "E128-Design",
        DiagnosticSeverity.Info,
        true,
        "Several mutable bool fields change together, so they encode one state with scattered transitions. " +
        "Replace them with one enum and one transition method. Set e128_min_state_flags to change the minimum cluster size.",
        null,
        WellKnownDiagnosticTags.CompilationEnd);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var minFlags = ReadMinFlags(context.Options.AnalyzerConfigOptionsProvider);
        var obsoleteAttribute = context.Compilation.GetTypeByMetadataName("System.ObsoleteAttribute");
        var interlockedType = context.Compilation.GetTypeByMetadataName("System.Threading.Interlocked");
        var volatileType = context.Compilation.GetTypeByMetadataName("System.Threading.Volatile");
        var evidenceMap = new ConcurrentDictionary<ISymbol, TypeEvidence>(SymbolEqualityComparer.Default);

        context.RegisterOperationAction(
            ctx => RecordWrite(ctx, evidenceMap, obsoleteAttribute),
            OperationKind.SimpleAssignment,
            OperationKind.CompoundAssignment);

        context.RegisterOperationAction(
            ctx => RecordConditionRead(ctx, evidenceMap, obsoleteAttribute),
            OperationKind.Conditional,
            OperationKind.Loop);

        context.RegisterOperationAction(
            ctx => RecordInvocation(ctx, evidenceMap, obsoleteAttribute, interlockedType, volatileType),
            OperationKind.Invocation);

        context.RegisterCompilationEndAction(ctx => Report(ctx, evidenceMap, minFlags));
    }

    private static void RecordWrite(
        OperationAnalysisContext context,
        ConcurrentDictionary<ISymbol, TypeEvidence> evidenceMap,
        INamedTypeSymbol? obsoleteAttribute)
    {
        if (context.Operation is not IAssignmentOperation assignment
            || assignment.Target is not IMemberReferenceOperation memberReference
            || memberReference.Instance is not IInstanceReferenceOperation
            {
                ReferenceKind: InstanceReferenceKind.ContainingTypeInstance
            })
        {
            return;
        }

        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType is null)
        {
            return;
        }

        var evidence = GetEvidence(evidenceMap, containingType, obsoleteAttribute);
        if (evidence is null || !evidence.IsCandidate(memberReference.Member))
        {
            return;
        }

        var body = context.ContainingSymbol;
        if (body is null)
        {
            return;
        }

        var value = assignment is ISimpleAssignmentOperation
            ? GetConstantBool(assignment.Value)
            : null;

        evidence.AddWrite((
            memberReference.Member,
            value,
            GetBodyKey(assignment, body),
            assignment.Syntax.GetLocation()));
    }

    private static void RecordConditionRead(
        OperationAnalysisContext context,
        ConcurrentDictionary<ISymbol, TypeEvidence> evidenceMap,
        INamedTypeSymbol? obsoleteAttribute)
    {
        var condition = context.Operation switch
        {
            IConditionalOperation conditional => conditional.Condition,
            IWhileLoopOperation whileLoop => whileLoop.Condition,
            IForLoopOperation forLoop => forLoop.Condition,
            _ => null
        };

        if (condition is null)
        {
            return;
        }

        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType is null)
        {
            return;
        }

        var evidence = GetEvidence(evidenceMap, containingType, obsoleteAttribute);
        if (evidence is null || !evidence.HasCandidates)
        {
            return;
        }

        var members = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var negated = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var descendant in condition.DescendantsAndSelf())
        {
            if (descendant is IMemberReferenceOperation memberReference
                && memberReference.Instance is IInstanceReferenceOperation
                {
                    ReferenceKind: InstanceReferenceKind.ContainingTypeInstance
                }
                && evidence.IsCandidate(memberReference.Member))
            {
                members.Add(memberReference.Member);
                if (IsNegated(memberReference, condition))
                {
                    negated.Add(memberReference.Member);
                }
            }
        }

        if (members.Count >= 2)
        {
            evidence.AddRead((
                members.ToImmutableArray(),
                negated.ToImmutableArray(),
                condition.Syntax.GetLocation()));
        }
    }

    private static void RecordInvocation(
        OperationAnalysisContext context,
        ConcurrentDictionary<ISymbol, TypeEvidence> evidenceMap,
        INamedTypeSymbol? obsoleteAttribute,
        INamedTypeSymbol? interlockedType,
        INamedTypeSymbol? volatileType)
    {
        if (context.Operation is not IInvocationOperation invocation)
        {
            return;
        }

        var method = invocation.TargetMethod;
        var isInterlocked = interlockedType is not null
                            && SymbolEqualityComparer.Default.Equals(method.ContainingType, interlockedType)
                            && string.Equals(method.Name, "Exchange", StringComparison.Ordinal);
        var isVolatile = volatileType is not null
                         && SymbolEqualityComparer.Default.Equals(method.ContainingType, volatileType)
                         && string.Equals(method.Name, "Write", StringComparison.Ordinal);

        if ((!isInterlocked && !isVolatile)
            || invocation.Arguments.Length < 2
            || invocation.Arguments[0].Value is not IFieldReferenceOperation fieldReference
            || fieldReference.Instance is not IInstanceReferenceOperation
            {
                ReferenceKind: InstanceReferenceKind.ContainingTypeInstance
            })
        {
            return;
        }

        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType is null)
        {
            return;
        }

        var evidence = GetEvidence(evidenceMap, containingType, obsoleteAttribute);
        if (evidence is null || !evidence.IsCandidate(fieldReference.Field))
        {
            return;
        }

        var body = context.ContainingSymbol;
        if (body is null)
        {
            return;
        }

        var value = GetConstantBool(invocation.Arguments[1].Value);

        evidence.AddWrite((
            fieldReference.Field,
            value,
            GetBodyKey(invocation, body),
            invocation.Syntax.GetLocation()));
    }

    private static bool IsNegated(IOperation reference, IOperation condition)
    {
        for (var current = reference.Parent; current is not null; current = current.Parent)
        {
            if (current is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not })
            {
                return true;
            }

            if (current is IBinaryOperation binary && IsNegatingComparison(binary))
            {
                return true;
            }

            if (current is IIsPatternOperation pattern && IsNegatingPattern(pattern))
            {
                return true;
            }

            if (current == condition)
            {
                break;
            }
        }

        return false;
    }

    private static bool IsNegatingComparison(IBinaryOperation binary)
    {
        return binary.OperatorKind == BinaryOperatorKind.NotEquals
            ? IsBooleanConstant(binary.RightOperand, true)
              || IsBooleanConstant(binary.LeftOperand, true)
            : binary.OperatorKind == BinaryOperatorKind.Equals
              && (IsBooleanConstant(binary.RightOperand, false)
                  || IsBooleanConstant(binary.LeftOperand, false));
    }

    private static bool IsNegatingPattern(IIsPatternOperation pattern)
    {
        return pattern.Pattern is IConstantPatternOperation constant
            ? IsBooleanConstant(constant.Value, false)
            : pattern.Pattern is INegatedPatternOperation { Pattern: IConstantPatternOperation inner }
              && IsBooleanConstant(inner.Value, true);
    }

    private static bool IsBooleanConstant(IOperation operation, bool expected)
    {
        return operation.ConstantValue is { HasValue: true, Value: bool value } && value == expected;
    }

    private static void Report(
        CompilationAnalysisContext context,
        ConcurrentDictionary<ISymbol, TypeEvidence> evidenceMap,
        int minFlags)
    {
        foreach (var pair in evidenceMap)
        {
            var evidence = pair.Value;
            var type = evidence.Type;

            if (!evidence.HasCandidates || type.Locations.Length == 0)
            {
                continue;
            }

            var transitionWrites = CollectTransitionWrites(evidence.Writes, out var bodyWrites, out var bodyLocations);
            var cluster = BuildCluster(evidence.Candidates, transitionWrites);

            if (cluster.Count < minFlags)
            {
                continue;
            }

            var clusterSet = new HashSet<ISymbol>(cluster, SymbolEqualityComparer.Default);
            var coWrite = FindCoWrite(bodyWrites, bodyLocations, clusterSet);
            var coRead = coWrite is null && cluster.Count >= minFlags + 1
                ? FindCoRead(evidence.Reads, clusterSet)
                : null;

            if (coWrite is null && coRead is null)
            {
                continue;
            }

            ReportType(context, type, cluster, coWrite, coRead);
        }
    }

    private static void ReportType(
        CompilationAnalysisContext context,
        INamedTypeSymbol type,
        List<ISymbol> cluster,
        Location? coWrite,
        Location? coRead)
    {
        var additional = new List<Location>(2);
        if (coWrite is not null)
        {
            additional.Add(coWrite);
        }

        if (coRead is not null)
        {
            additional.Add(coRead);
        }

        var names = string.Join(
            ", ",
            cluster.Select(member => member.Name).OrderBy(name => name, StringComparer.Ordinal));

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            type.Locations[0],
            additional,
            null,
            type.Name,
            cluster.Count,
            names));
    }

    private static Dictionary<ISymbol, WriteStats> CollectTransitionWrites(
        (ISymbol Member, bool? Value, ISymbol Body, Location Location)[] writes,
        out Dictionary<ISymbol, Dictionary<ISymbol, bool?>> bodyWrites,
        out Dictionary<ISymbol, Location> bodyLocations)
    {
        var transitionWrites = new Dictionary<ISymbol, WriteStats>(SymbolEqualityComparer.Default);
        bodyWrites = new Dictionary<ISymbol, Dictionary<ISymbol, bool?>>(SymbolEqualityComparer.Default);
        bodyLocations = new Dictionary<ISymbol, Location>(SymbolEqualityComparer.Default);

        foreach (var (member, value, body, location) in writes)
        {
            if (IsLifecycleBody(body))
            {
                continue;
            }

            UpdateWriteStats(transitionWrites, member, value);
            AddBodyWrite(bodyWrites, bodyLocations, body, member, value, location);
        }

        return transitionWrites;
    }

    private static void UpdateWriteStats(
        Dictionary<ISymbol, WriteStats> writes,
        ISymbol member,
        bool? value)
    {
        if (!writes.TryGetValue(member, out var stats))
        {
            stats = new WriteStats();
            writes.Add(member, stats);
        }

        stats.Add(value);
    }

    private static void AddBodyWrite(
        Dictionary<ISymbol, Dictionary<ISymbol, bool?>> bodyWrites,
        Dictionary<ISymbol, Location> bodyLocations,
        ISymbol body,
        ISymbol member,
        bool? value,
        Location location)
    {
        if (!bodyWrites.TryGetValue(body, out var members))
        {
            members = new Dictionary<ISymbol, bool?>(SymbolEqualityComparer.Default);
            bodyWrites.Add(body, members);
            bodyLocations.Add(body, location);
        }

        members[member] = value;
    }

    private static List<ISymbol> BuildCluster(
        ImmutableArray<ISymbol> candidates,
        Dictionary<ISymbol, WriteStats> transitionWrites)
    {
        var cluster = new List<ISymbol>();
        foreach (var candidate in candidates)
        {
            if (transitionWrites.TryGetValue(candidate, out var stats) && stats.IsTwoValued)
            {
                cluster.Add(candidate);
            }
        }

        return cluster;
    }

    private static Location? FindCoWrite(
        Dictionary<ISymbol, Dictionary<ISymbol, bool?>> bodyWrites,
        Dictionary<ISymbol, Location> bodyLocations,
        HashSet<ISymbol> cluster)
    {
        Location? earliest = null;
        foreach (var pair in bodyWrites)
        {
            if (!HasMixedTransition(pair.Value, cluster)
                || !bodyLocations.TryGetValue(pair.Key, out var location))
            {
                continue;
            }

            if (earliest is null || location.SourceSpan.Start < earliest.SourceSpan.Start)
            {
                earliest = location;
            }
        }

        return earliest;
    }

    private static bool HasMixedTransition(
        Dictionary<ISymbol, bool?> writes,
        HashSet<ISymbol> cluster)
    {
        var count = 0;
        var sawTrue = false;
        var sawFalse = false;
        var sawUnknown = false;

        foreach (var pair in writes)
        {
            if (!cluster.Contains(pair.Key))
            {
                continue;
            }

            count++;
            if (pair.Value is true)
            {
                sawTrue = true;
            }
            else if (pair.Value is false)
            {
                sawFalse = true;
            }
            else
            {
                sawUnknown = true;
            }
        }

        return count >= 2 && (sawUnknown || (sawTrue && sawFalse));
    }

    private static Location? FindCoRead(
        (ImmutableArray<ISymbol> Members, ImmutableArray<ISymbol> NegatedMembers, Location Location)[] reads,
        HashSet<ISymbol> cluster)
    {
        Location? earliest = null;
        foreach (var (members, negated, location) in reads)
        {
            if (CountClusterMembers(members, cluster) < 2
                || !ContainsClusterMember(negated, cluster))
            {
                continue;
            }

            if (earliest is null || location.SourceSpan.Start < earliest.SourceSpan.Start)
            {
                earliest = location;
            }
        }

        return earliest;
    }

    private static bool ContainsClusterMember(IEnumerable<ISymbol> members, HashSet<ISymbol> cluster)
    {
        foreach (var member in members)
        {
            if (cluster.Contains(member))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountClusterMembers(IEnumerable<ISymbol> members, HashSet<ISymbol> cluster)
    {
        var count = 0;
        foreach (var member in members)
        {
            if (cluster.Contains(member))
            {
                count++;
            }
        }

        return count;
    }

    private static TypeEvidence? GetEvidence(
        ConcurrentDictionary<ISymbol, TypeEvidence> evidenceMap,
        INamedTypeSymbol type,
        INamedTypeSymbol? obsoleteAttribute)
    {
        return IsAnalyzedType(type)
            ? evidenceMap.GetOrAdd(
                type.OriginalDefinition,
                symbol => CreateEvidence((INamedTypeSymbol)symbol, obsoleteAttribute))
            : null;
    }

    private static TypeEvidence CreateEvidence(
        INamedTypeSymbol type,
        INamedTypeSymbol? obsoleteAttribute)
    {
        var evidence = new TypeEvidence(type);
        foreach (var member in type.GetMembers())
        {
            if (IsCandidateMember(member, obsoleteAttribute))
            {
                evidence.AddCandidate(member);
            }
        }

        return evidence;
    }

    private static ISymbol GetBodyKey(IOperation operation, ISymbol containingSymbol)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation anonymous)
            {
                return anonymous.Symbol;
            }

            if (current is ILocalFunctionOperation local)
            {
                return local.Symbol;
            }
        }

        return containingSymbol;
    }

    private static bool IsAnalyzedType(INamedTypeSymbol type)
    {
        return type.TypeKind == TypeKind.Class && !type.IsRecord && !type.IsStatic;
    }

    private static bool IsCandidateMember(ISymbol member, INamedTypeSymbol? obsoleteAttribute)
    {
        return !member.IsStatic
               && !HasAttribute(member, obsoleteAttribute)
               && (member switch
               {
                   IFieldSymbol field => !field.IsConst
                                         && !field.IsReadOnly
                                         && !field.IsImplicitlyDeclared
                                         && field.Type.SpecialType == SpecialType.System_Boolean,
                   IPropertySymbol property => property.Type.SpecialType == SpecialType.System_Boolean
                                               && !property.IsIndexer
                                               && !property.IsAbstract
                                               && !property.IsOverride
                                               && property.ExplicitInterfaceImplementations.IsEmpty
                                               && property.SetMethod is { IsInitOnly: false }
                                               && IsAutoProperty(property),
                   _ => false
               });
    }

    private static bool IsAutoProperty(IPropertySymbol property)
    {
        if (property.DeclaringSyntaxReferences.Length == 0)
        {
            return false;
        }

        foreach (var reference in property.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not PropertyDeclarationSyntax declaration
                || declaration.AccessorList is not { } accessors)
            {
                return false;
            }

            foreach (var accessor in accessors.Accessors)
            {
                if (accessor.Body is not null || accessor.ExpressionBody is not null)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsLifecycleBody(ISymbol body)
    {
        return body is IMethodSymbol
        {
            MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor or MethodKind.Destructor
        };
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
        {
            return false;
        }

        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool? GetConstantBool(IOperation operation)
    {
        return operation.ConstantValue is { HasValue: true, Value: bool value } ? value : null;
    }

    private static int ReadMinFlags(AnalyzerConfigOptionsProvider optionsProvider)
    {
        return optionsProvider.GlobalOptions.TryGetValue(OptionKey, out var text)
               && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
               && value >= 2
            ? value
            : DefaultMinFlags;
    }

    private sealed class WriteStats
    {
        private bool _sawTrue;
        private bool _sawFalse;
        private bool _sawUnknown;

        internal bool IsTwoValued => _sawUnknown || (_sawTrue && _sawFalse);

        internal void Add(bool? value)
        {
            if (value is true)
            {
                _sawTrue = true;
            }
            else if (value is false)
            {
                _sawFalse = true;
            }
            else
            {
                _sawUnknown = true;
            }
        }
    }

    private sealed class TypeEvidence
    {
        private readonly ConcurrentDictionary<ISymbol, byte> _candidates =
            new(SymbolEqualityComparer.Default);

        private readonly ConcurrentQueue<(ISymbol Member, bool? Value, ISymbol Body, Location Location)> _writes = new();

        private readonly ConcurrentQueue<(
            ImmutableArray<ISymbol> Members,
            ImmutableArray<ISymbol> NegatedMembers,
            Location Location)> _reads = new();

        internal TypeEvidence(INamedTypeSymbol type)
        {
            Type = type;
        }

        internal INamedTypeSymbol Type { get; }

        internal bool HasCandidates => !_candidates.IsEmpty;

        internal ImmutableArray<ISymbol> Candidates => [.. _candidates.Keys];

        internal (ISymbol Member, bool? Value, ISymbol Body, Location Location)[] Writes => [.. _writes];

        internal (ImmutableArray<ISymbol> Members, ImmutableArray<ISymbol> NegatedMembers, Location Location)[] Reads =>
            [.. _reads];

        internal void AddCandidate(ISymbol candidate)
        {
            _candidates.TryAdd(candidate, 0);
        }

        internal bool IsCandidate(ISymbol candidate)
        {
            return _candidates.ContainsKey(candidate);
        }

        internal void AddWrite((ISymbol Member, bool? Value, ISymbol Body, Location Location) record)
        {
            _writes.Enqueue(record);
        }

        internal void AddRead((
            ImmutableArray<ISymbol> Members,
            ImmutableArray<ISymbol> NegatedMembers,
            Location Location) record)
        {
            _reads.Enqueue(record);
        }
    }
}
