using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
    internal const string MinTransitionBodiesOptionKey = "e128_state_min_transition_bodies";

    private const int DefaultMinFlags = 3;
    private const int DefaultMinTransitionBodies = 1;
    private const int MinClusterMembersPerSite = 2;
    private const int MinWriteArguments = 2;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Boolean flags encode one state",
        "'{0}' uses {1} boolean flags as one state: {2}. Use one enum and one transition method.",
        "E128-Design",
        DiagnosticSeverity.Info,
        true,
        "Several mutable bool fields change together, so they encode one state with scattered transitions. " +
        "Replace them with one enum and one transition method. Set e128_min_state_flags to change the minimum cluster size. " +
        "Set e128_state_min_transition_bodies to require more than one transition body.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

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

        context.RegisterCompilationEndAction(ctx => Report(ctx, evidenceMap));
    }

    private static void RecordWrite(
        OperationAnalysisContext context,
        ConcurrentDictionary<ISymbol, TypeEvidence> evidenceMap,
        INamedTypeSymbol? obsoleteAttribute)
    {
        if (context.Operation is not IAssignmentOperation assignment
            || assignment.Target is not IMemberReferenceOperation memberReference
            || context.ContainingSymbol is not { } body
            || TryResolveWriteTarget(context, memberReference, evidenceMap, obsoleteAttribute) is not { } target)
        {
            return;
        }

        var value = assignment is ISimpleAssignmentOperation
            ? GetConstantBool(assignment.Value)
            : null;

        target.Evidence.AddWrite((
            target.Member,
            value,
            GetBodyKey(assignment, body),
            assignment.Syntax.GetLocation(),
            target.IsInternal));
    }

    private static (TypeEvidence Evidence, ISymbol Member, bool IsInternal)? TryResolveWriteTarget(
        OperationAnalysisContext context,
        IMemberReferenceOperation memberReference,
        ConcurrentDictionary<ISymbol, TypeEvidence> evidenceMap,
        INamedTypeSymbol? obsoleteAttribute)
    {
        if (memberReference.Member.ContainingType is not { } owner
            || context.ContainingSymbol?.ContainingType is not { } containingType
            || !IsWriteInstanceValid(memberReference, owner, containingType))
        {
            return null;
        }

        var evidence = GetEvidence(evidenceMap, owner, obsoleteAttribute, context.CancellationToken);
        if (evidence is null)
        {
            return null;
        }

        var member = evidence.Resolve(memberReference.Member);
        if (!evidence.IsCandidate(member))
        {
            return null;
        }

        var isInternal = SymbolEqualityComparer.Default.Equals(owner, containingType);
        return (evidence, member, isInternal);
    }

    private static bool IsWriteInstanceValid(
        IMemberReferenceOperation memberReference,
        INamedTypeSymbol owner,
        INamedTypeSymbol containingType)
    {
        if (SymbolEqualityComparer.Default.Equals(owner, containingType))
        {
            return memberReference.Instance is IInstanceReferenceOperation
            {
                ReferenceKind: InstanceReferenceKind.ContainingTypeInstance
            };
        }

        return memberReference.Instance is not null && !owner.DeclaringSyntaxReferences.IsEmpty;
    }

    private static IOperation? GetCondition(IOperation operation)
    {
        return operation switch
        {
            IConditionalOperation conditional => conditional.Condition,
            IWhileLoopOperation whileLoop => whileLoop.Condition,
            IForLoopOperation forLoop => forLoop.Condition,
            _ => null
        };
    }

    private static void RecordConditionRead(
        OperationAnalysisContext context,
        ConcurrentDictionary<ISymbol, TypeEvidence> evidenceMap,
        INamedTypeSymbol? obsoleteAttribute)
    {
        var condition = GetCondition(context.Operation);

        if (condition is null)
        {
            return;
        }

        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType is null)
        {
            return;
        }

        var evidence = GetEvidence(evidenceMap, containingType, obsoleteAttribute, context.CancellationToken);
        if (evidence is null || !evidence.HasCandidates)
        {
            return;
        }

        var (members, negated) = CollectConditionMembers(condition, evidence);
        if (members.Count < MinClusterMembersPerSite)
        {
            return;
        }

        evidence.AddRead((
            members.ToImmutableArray(),
            negated.ToImmutableArray(),
            condition.Syntax.GetLocation()));
    }

    private static (HashSet<ISymbol> Members, HashSet<ISymbol> Negated) CollectConditionMembers(
        IOperation condition,
        TypeEvidence evidence)
    {
        var members = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var negated = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var descendant in condition.DescendantsAndSelf())
        {
            if (descendant is IMemberReferenceOperation memberReference
                && memberReference.Instance is IInstanceReferenceOperation
                {
                    ReferenceKind: InstanceReferenceKind.ContainingTypeInstance
                })
            {
                var member = evidence.Resolve(memberReference.Member);
                if (!evidence.IsCandidate(member))
                {
                    continue;
                }

                members.Add(member);
                if (IsNegated(memberReference, condition))
                {
                    negated.Add(member);
                }
            }
        }

        return (members, negated);
    }

    private static void RecordInvocation(
        OperationAnalysisContext context,
        ConcurrentDictionary<ISymbol, TypeEvidence> evidenceMap,
        INamedTypeSymbol? obsoleteAttribute,
        INamedTypeSymbol? interlockedType,
        INamedTypeSymbol? volatileType)
    {
        if (context.Operation is not IInvocationOperation invocation
            || GetWrittenField(invocation, interlockedType, volatileType) is not { } fieldReference)
        {
            return;
        }

        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType is null)
        {
            return;
        }

        var evidence = GetEvidence(evidenceMap, containingType, obsoleteAttribute, context.CancellationToken);
        if (evidence is null)
        {
            return;
        }

        var member = evidence.Resolve(fieldReference.Field);
        if (!evidence.IsCandidate(member))
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
            member,
            value,
            GetBodyKey(invocation, body),
            invocation.Syntax.GetLocation(),
            IsInternal: true));
    }

    // Matches Interlocked.Exchange or Volatile.Write on a field of the containing instance.
    private static IFieldReferenceOperation? GetWrittenField(
        IInvocationOperation invocation,
        INamedTypeSymbol? interlockedType,
        INamedTypeSymbol? volatileType)
    {
        var method = invocation.TargetMethod;
        if (!(IsMethodOf(method, interlockedType, "Exchange") || IsMethodOf(method, volatileType, "Write"))
            || invocation.Arguments.Length < MinWriteArguments
            || invocation.Arguments[0].Value is not IFieldReferenceOperation fieldReference
            || fieldReference.Instance is not IInstanceReferenceOperation
            {
                ReferenceKind: InstanceReferenceKind.ContainingTypeInstance
            })
        {
            return null;
        }

        return fieldReference;
    }

    private static bool IsMethodOf(IMethodSymbol method, INamedTypeSymbol? owner, string name)
    {
        return owner is not null
               && SymbolEqualityComparer.Default.Equals(method.ContainingType, owner)
               && string.Equals(method.Name, name, StringComparison.Ordinal);
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
        return binary.OperatorKind is BinaryOperatorKind.NotEquals
            ? IsBooleanConstant(binary.RightOperand, true)
              || IsBooleanConstant(binary.LeftOperand, true)
            : binary.OperatorKind is BinaryOperatorKind.Equals
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
        ConcurrentDictionary<ISymbol, TypeEvidence> evidenceMap)
    {
        foreach (var pair in evidenceMap)
        {
            TryReportType(context, pair.Value);
        }
    }

    private static void TryReportType(CompilationAnalysisContext context, TypeEvidence evidence)
    {
        var type = evidence.Type;
        if (!evidence.HasCandidates || !evidence.HasInternalWrite || type.Locations.Length is 0)
        {
            return;
        }

        var tree = type.Locations[0].SourceTree;
        if (tree is null)
        {
            return;
        }

        // Read per type so a project .editorconfig applies, not only a global config.
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree);
        if (!TryFindStateEvidence(evidence, options, out var cluster, out var coWrite, out var coRead))
        {
            return;
        }

        ReportType(context, type, cluster, coWrite, coRead);
    }

    private static bool TryFindStateEvidence(
        TypeEvidence evidence,
        AnalyzerConfigOptions options,
        out List<ISymbol> cluster,
        out Location? coWrite,
        out Location? coRead)
    {
        cluster = [];
        coWrite = null;
        coRead = null;

        var minFlags = ReadMinFlags(options);
        var transitionWrites = CollectTransitionWrites(
            evidence.Writes,
            out var bodyWrites,
            out var bodyLocations);
        cluster = BuildCluster(evidence.Candidates, transitionWrites);

        if (cluster.Count < minFlags)
        {
            return false;
        }

        var clusterSet = new HashSet<ISymbol>(cluster, SymbolEqualityComparer.Default);
        if (CountTransitionBodies(bodyWrites, clusterSet) < ReadMinTransitionBodies(options))
        {
            return false;
        }

        coWrite = FindCoWrite(bodyWrites, bodyLocations, clusterSet);
        coRead = coWrite is null && cluster.Count >= minFlags + 1
            ? FindCoRead(evidence.Reads, clusterSet)
            : null;

        return coWrite is not null || coRead is not null;
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
            cluster.Select(static member => member.Name).OrderBy(static name => name, StringComparer.Ordinal));

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
        (ISymbol Member, bool? Value, ISymbol Body, Location Location, bool IsInternal)[] writes,
        out Dictionary<ISymbol, Dictionary<ISymbol, bool?>> bodyWrites,
        out Dictionary<ISymbol, Location> bodyLocations)
    {
        var transitionWrites = new Dictionary<ISymbol, WriteStats>(SymbolEqualityComparer.Default);
        bodyWrites = new Dictionary<ISymbol, Dictionary<ISymbol, bool?>>(SymbolEqualityComparer.Default);
        bodyLocations = new Dictionary<ISymbol, Location>(SymbolEqualityComparer.Default);

        foreach (var (member, value, body, location, _) in writes)
        {
            if (IsLifecycleBody(body) || IsAccessorBody(body))
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

        return count >= MinClusterMembersPerSite && (sawUnknown || (sawTrue && sawFalse));
    }

    private static Location? FindCoRead(
        (ImmutableArray<ISymbol> Members, ImmutableArray<ISymbol> NegatedMembers, Location Location)[] reads,
        HashSet<ISymbol> cluster)
    {
        Location? earliest = null;
        foreach (var (members, negated, location) in reads)
        {
            if (CountClusterMembers(members, cluster) < MinClusterMembersPerSite
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

    private static int CountTransitionBodies(
        Dictionary<ISymbol, Dictionary<ISymbol, bool?>> bodyWrites,
        HashSet<ISymbol> cluster)
    {
        var count = 0;
        foreach (var pair in bodyWrites)
        {
            foreach (var member in pair.Value.Keys)
            {
                if (cluster.Contains(member))
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    private static TypeEvidence? GetEvidence(
        ConcurrentDictionary<ISymbol, TypeEvidence> evidenceMap,
        INamedTypeSymbol type,
        INamedTypeSymbol? obsoleteAttribute,
        CancellationToken cancellationToken)
    {
        return IsAnalyzedType(type)
            ? evidenceMap.GetOrAdd(
                type.OriginalDefinition,
                symbol => CreateEvidence((INamedTypeSymbol)symbol, obsoleteAttribute, cancellationToken))
            : null;
    }

    private static TypeEvidence CreateEvidence(
        INamedTypeSymbol type,
        INamedTypeSymbol? obsoleteAttribute,
        CancellationToken cancellationToken)
    {
        var evidence = new TypeEvidence(type);
        foreach (var member in type.GetMembers())
        {
            if (IsCandidateMember(member, obsoleteAttribute, cancellationToken))
            {
                evidence.AddCandidate(member);
            }
        }

        foreach (var member in type.GetMembers())
        {
            if (member is IPropertySymbol property
                && TryGetForwardedFieldName(property, cancellationToken) is { } name
                && FindField(type, name) is { } field
                && evidence.IsCandidate(field))
            {
                evidence.AddCanonical(property, field);
            }
        }

        return evidence;
    }

    private static IFieldSymbol? FindField(INamedTypeSymbol type, string name)
    {
        foreach (var member in type.GetMembers(name))
        {
            if (member is IFieldSymbol field)
            {
                return field;
            }
        }

        return null;
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
        return (type.TypeKind is TypeKind.Class && !type.IsRecord && !type.IsStatic)
               || (type.TypeKind is TypeKind.Struct && !type.IsRecord && !type.IsReadOnly);
    }

    private static bool IsCandidateMember(
        ISymbol member,
        INamedTypeSymbol? obsoleteAttribute,
        CancellationToken cancellationToken)
    {
        return !member.IsStatic
               && !HasAttribute(member, obsoleteAttribute)
               && (member switch
               {
                   IFieldSymbol field => IsMutableBoolField(field),
                   IPropertySymbol property => IsMutableBoolAutoProperty(property, cancellationToken),
                   _ => false
               });
    }

    private static bool IsMutableBoolField(IFieldSymbol field)
    {
        return !field.IsConst
               && !field.IsReadOnly
               && !field.IsImplicitlyDeclared
               && field.Type.SpecialType is SpecialType.System_Boolean;
    }

    private static bool IsMutableBoolAutoProperty(IPropertySymbol property, CancellationToken cancellationToken)
    {
        return property.Type.SpecialType is SpecialType.System_Boolean
               && !property.IsIndexer
               && !property.IsAbstract
               && !property.IsOverride
               && property.ExplicitInterfaceImplementations.IsEmpty
               && property.SetMethod is { IsInitOnly: false }
               && IsAutoProperty(property, cancellationToken);
    }

    private static bool IsAutoProperty(IPropertySymbol property, CancellationToken cancellationToken)
    {
        if (property.DeclaringSyntaxReferences.Length is 0)
        {
            return false;
        }

        foreach (var reference in property.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is not PropertyDeclarationSyntax declaration
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

    // A property with a getter and a setter that only read and write one field
    // names that field. The field is the canonical member, so the pair counts once.
    private static string? TryGetForwardedFieldName(
        IPropertySymbol property,
        CancellationToken cancellationToken)
    {
        if (!IsForwardableProperty(property))
        {
            return null;
        }

        string? forwarded = null;
        foreach (var reference in property.DeclaringSyntaxReferences)
        {
            if (!TryGetAccessorFieldName(reference, cancellationToken, out var name))
            {
                return null;
            }

            if (forwarded is not null && !string.Equals(forwarded, name, StringComparison.Ordinal))
            {
                return null;
            }

            forwarded = name;
        }

        return forwarded;
    }

    private static bool IsForwardableProperty(IPropertySymbol property)
    {
        return !property.IsStatic
               && !property.IsIndexer
               && !property.IsAbstract
               && !property.IsOverride
               && property.ExplicitInterfaceImplementations.IsEmpty
               && property.Type.SpecialType is SpecialType.System_Boolean
               && property.SetMethod is { IsInitOnly: false };
    }

    private static bool TryGetAccessorFieldName(
        SyntaxReference reference,
        CancellationToken cancellationToken,
        out string name)
    {
        name = string.Empty;
        if (reference.GetSyntax(cancellationToken) is not PropertyDeclarationSyntax { AccessorList: { } accessors })
        {
            return false;
        }

        string? getter = null;
        string? setter = null;
        foreach (var accessor in accessors.Accessors)
        {
            if (accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
            {
                getter = GetGetterFieldName(accessor);
                if (getter is null)
                {
                    return false;
                }
            }
            else if (accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
            {
                setter = GetSetterFieldName(accessor);
                if (setter is null)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        if (getter is null || setter is null || !string.Equals(getter, setter, StringComparison.Ordinal))
        {
            return false;
        }

        name = getter;
        return true;
    }

    private static string? GetGetterFieldName(AccessorDeclarationSyntax accessor)
    {
        var expression = accessor.ExpressionBody is { } arrow
            ? arrow.Expression
            : FirstReturnedExpression(accessor.Body);

        return GetSimpleMemberName(expression);
    }

    private static string? GetSetterFieldName(AccessorDeclarationSyntax accessor)
    {
        var expression = accessor.ExpressionBody is { } arrow
            ? arrow.Expression
            : FirstStatementExpression(accessor.Body);

        return expression is AssignmentExpressionSyntax assignment
               && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
               && assignment.Right is IdentifierNameSyntax valueName
               && string.Equals(valueName.Identifier.ValueText, "value", StringComparison.Ordinal)
            ? GetSimpleMemberName(assignment.Left)
            : null;
    }

    private static ExpressionSyntax? FirstReturnedExpression(BlockSyntax? body)
    {
        return body is { Statements.Count: 1 }
               && body.Statements[0] is ReturnStatementSyntax { Expression: { } returned }
            ? returned
            : null;
    }

    private static ExpressionSyntax? FirstStatementExpression(BlockSyntax? body)
    {
        return body is { Statements.Count: 1 }
               && body.Statements[0] is ExpressionStatementSyntax { Expression: { } statement }
            ? statement
            : null;
    }

    private static string? GetSimpleMemberName(ExpressionSyntax? expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name } =>
                name.Identifier.ValueText,
            _ => null
        };
    }

    private static bool IsLifecycleBody(ISymbol body)
    {
        return body is IMethodSymbol
        {
            MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor or MethodKind.Destructor
        };
    }

    // An accessor body is the storage for a property, not a transition.
    private static bool IsAccessorBody(ISymbol body)
    {
        return body is IMethodSymbol
        {
            MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet
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

    private static int ReadMinFlags(AnalyzerConfigOptions options)
    {
        return OptionReader.ReadInt(
            options,
            OptionKey,
            DefaultMinFlags,
            minimum: 2);
    }

    private static int ReadMinTransitionBodies(AnalyzerConfigOptions options)
    {
        return OptionReader.ReadInt(
            options,
            MinTransitionBodiesOptionKey,
            DefaultMinTransitionBodies,
            minimum: 1);
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

        private readonly ConcurrentQueue<(
            ISymbol Member,
            bool? Value,
            ISymbol Body,
            Location Location,
            bool IsInternal)> _writes = new();

        private readonly ConcurrentQueue<(
            ImmutableArray<ISymbol> Members,
            ImmutableArray<ISymbol> NegatedMembers,
            Location Location)> _reads = new();

        private readonly Dictionary<ISymbol, ISymbol> _canonical = new(SymbolEqualityComparer.Default);

        private int _hasInternalWrite;

        internal TypeEvidence(INamedTypeSymbol type)
        {
            Type = type;
        }

        internal INamedTypeSymbol Type { get; }

        internal bool HasCandidates => !_candidates.IsEmpty;

        internal bool HasInternalWrite => Volatile.Read(ref _hasInternalWrite) is not 0;

        internal ImmutableArray<ISymbol> Candidates => [.. _candidates.Keys];

        internal (ISymbol Member, bool? Value, ISymbol Body, Location Location, bool IsInternal)[] Writes =>
            [.. _writes];

        internal (ImmutableArray<ISymbol> Members, ImmutableArray<ISymbol> NegatedMembers, Location Location)[] Reads =>
            [.. _reads];

        internal void AddCandidate(ISymbol candidate)
        {
            _ = _candidates.TryAdd(candidate, 0);
        }

        internal void AddCanonical(ISymbol property, ISymbol field)
        {
            _canonical[property] = field;
        }

        internal ISymbol Resolve(ISymbol member)
        {
            return _canonical.TryGetValue(member, out var canonical) ? canonical : member;
        }

        internal bool IsCandidate(ISymbol candidate)
        {
            return _candidates.ContainsKey(candidate);
        }

        internal void AddWrite((
            ISymbol Member,
            bool? Value,
            ISymbol Body,
            Location Location,
            bool IsInternal) record)
        {
            if (record.IsInternal)
            {
                Volatile.Write(ref _hasInternalWrite, 1);
            }

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
