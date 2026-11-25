using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

/// <summary>
///     E128254: A reflection write sets a static readonly field of a type outside the framework.
///     The runtime inlines a static readonly field, so the new value reaches no inlined read.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StaticReadonlyReflectionWriteAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128254";

    private const string FieldInfoMetadataName = "System.Reflection.FieldInfo";
    private const string SystemTypeMetadataName = "System.Type";
    private const string GetFieldName = "GetField";
    private const string SetValueName = "SetValue";
    private const string SetValueDirectName = "SetValueDirect";
    private const string StaticFlag = "Static";
    private const string PublicFlag = "Public";
    private const string NonPublicFlag = "NonPublic";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not write a static readonly field through reflection",
        "Reflection sets '{0}.{1}'. The runtime inlines a static readonly field, so the new value reaches no inlined read.",
        "E128-Unsafe",
        DiagnosticSeverity.Warning,
        true,
        "A static readonly field is a constant to the runtime. A reflective write can miss every inlined " +
        "read of the field. Make the field a mutable static with a lock, or pass the value to each reader.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static start =>
        {
            var fieldInfo = start.Compilation.GetTypeByMetadataName(FieldInfoMetadataName);
            var systemType = start.Compilation.GetTypeByMetadataName(SystemTypeMetadataName);
            var bindingFlags = start.Compilation.GetTypeByMetadataName("System.Reflection.BindingFlags");

            if (fieldInfo is null || systemType is null || bindingFlags is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, fieldInfo, systemType, bindingFlags),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol fieldInfo,
        INamedTypeSymbol systemType,
        INamedTypeSymbol bindingFlags)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var model = context.SemanticModel;

        if (model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, fieldInfo)
            || !IsWriteMethod(method.Name))
        {
            return;
        }

        if (ReceiverOf(invocation) is not { } receiver
            || ResolveField(receiver, model, systemType, context.CancellationToken) is not { } target)
        {
            return;
        }

        if (!IsStaticReadonlyWrite(target, bindingFlags, model, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.Parent is ConditionalAccessExpressionSyntax conditional
                ? conditional.GetLocation()
                : invocation.GetLocation(),
            target.Field.ContainingType.Name,
            target.Field.Name));
    }

    private static ExpressionSyntax? ReceiverOf(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Expression,
            MemberBindingExpressionSyntax when invocation.Parent is ConditionalAccessExpressionSyntax conditional
                => conditional.Expression,
            _ => null,
        };
    }

    private static bool IsWriteMethod(string name)
    {
        return string.Equals(name, SetValueName, StringComparison.Ordinal)
               || string.Equals(name, SetValueDirectName, StringComparison.Ordinal);
    }

    private static bool IsStaticReadonlyWrite(
        FieldTarget target,
        INamedTypeSymbol bindingFlags,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return target.Field.IsStatic
               && target.Field.IsReadOnly
               && !FrameworkAssemblyIdentity.IsFrameworkAssembly(target.Field.ContainingAssembly)
               && FlagsCanReturn(target.Lookup, target.Field, bindingFlags, model, cancellationToken);
    }

    private static FieldTarget? ResolveField(
        ExpressionSyntax receiver,
        SemanticModel model,
        INamedTypeSymbol systemType,
        CancellationToken cancellationToken)
    {
        switch (receiver)
        {
            case InvocationExpressionSyntax invocation:
                return ResolveGetField(invocation, model, systemType, cancellationToken);

            case IdentifierNameSyntax identifier
                when model.GetSymbolInfo(identifier, cancellationToken).Symbol is ILocalSymbol local:
                return FindInitializer(local, identifier.SpanStart, model, cancellationToken) is { } initializer
                    ? ResolveGetField(initializer, model, systemType, cancellationToken)
                    : null;

            default:
                return null;
        }
    }

    private static InvocationExpressionSyntax? FindInitializer(
        ILocalSymbol local,
        int usePosition,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var declarator = local.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        return declarator?.Initializer?.Value as InvocationExpressionSyntax
               ?? FindAssignedInitializer(declarator, usePosition, local, model, cancellationToken);
    }

    private static InvocationExpressionSyntax? FindAssignedInitializer(
        VariableDeclaratorSyntax? declarator,
        int usePosition,
        ILocalSymbol local,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (declarator?.Parent?.Parent is not LocalDeclarationStatementSyntax { Parent: BlockSyntax block })
        {
            return null;
        }

        var assignments = block.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => IsAssignmentTo(assignment, usePosition, local, model, cancellationToken))
            .ToList();

        return assignments.Count is 1 && assignments[0].Right is InvocationExpressionSyntax assigned
            ? assigned
            : null;
    }

    private static bool IsAssignmentTo(
        AssignmentExpressionSyntax assignment,
        int usePosition,
        ILocalSymbol local,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return assignment.Span.End <= usePosition
               && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
               && SymbolEqualityComparer.Default.Equals(
                   model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol,
                   local);
    }

    private static FieldTarget? ResolveGetField(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        INamedTypeSymbol systemType,
        CancellationToken cancellationToken)
    {
        if (!TryGetLookup(invocation, model, systemType, cancellationToken, out var memberAccess, out var name))
        {
            return null;
        }

        if (!ReflectionTargets.TryGet(memberAccess.Expression, model, cancellationToken, out var target)
            || target is null)
        {
            return null;
        }

        var field = target.GetMembers(name)
            .OfType<IFieldSymbol>()
            .FirstOrDefault(static candidate => !candidate.IsImplicitlyDeclared);

        return field is null ? null : new FieldTarget(field, invocation);
    }

    private static bool TryGetLookup(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        INamedTypeSymbol systemType,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out MemberAccessExpressionSyntax? memberAccess,
        [NotNullWhen(true)] out string? name)
    {
        memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        name = null;

        if (memberAccess is null
            || !string.Equals(memberAccess.Name.Identifier.ValueText, GetFieldName, StringComparison.Ordinal)
            || invocation.ArgumentList.Arguments.Count is 0
            || model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || !TypeSymbols.DerivesFrom(method.ContainingType, systemType))
        {
            return false;
        }

        var constant = model.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression, cancellationToken);
        name = constant.HasValue ? constant.Value as string : null;

        return name is not null;
    }

    private static bool FlagsCanReturn(
        InvocationExpressionSyntax invocation,
        IFieldSymbol field,
        INamedTypeSymbol bindingFlags,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetFlagArgument(invocation, bindingFlags, model, cancellationToken, out var flagArgument))
        {
            return false;
        }

        var isPublic = field.DeclaredAccessibility is Accessibility.Public;

        if (flagArgument is null)
        {
            return isPublic;
        }

        return TryGetFlagValue(flagArgument, model, cancellationToken, out var value)
               && HasFlag(value, bindingFlags, StaticFlag)
               && HasFlag(value, bindingFlags, isPublic ? PublicFlag : NonPublicFlag);
    }

    private static bool TryGetFlagArgument(
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol bindingFlags,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ExpressionSyntax? argument)
    {
        argument = null;

        var matches = invocation.ArgumentList.Arguments
            .Where(candidate => SymbolEqualityComparer.Default.Equals(
                model.GetTypeInfo(candidate.Expression, cancellationToken).Type,
                bindingFlags))
            .ToList();

        if (matches.Count is 0)
        {
            return true;
        }

        if (matches.Count is not 1)
        {
            return false;
        }

        argument = matches[0].Expression;
        return true;
    }

    private static bool TryGetFlagValue(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken,
        out int value)
    {
        value = 0;

        var constant = model.GetConstantValue(expression, cancellationToken);
        if (!constant.HasValue || constant.Value is not int flags)
        {
            return false;
        }

        value = flags;
        return true;
    }

    private static bool HasFlag(int value, INamedTypeSymbol bindingFlags, string name)
    {
        return bindingFlags.GetMembers(name)
            .OfType<IFieldSymbol>()
            .Any(member => member.HasConstantValue
                           && member.ConstantValue is int flag
                           && (value & flag) is not 0);
    }

    private sealed class FieldTarget
    {
        public FieldTarget(IFieldSymbol field, InvocationExpressionSyntax lookup)
        {
            Field = field;
            Lookup = lookup;
        }

        public IFieldSymbol Field { get; }

        public InvocationExpressionSyntax Lookup { get; }
    }
}
