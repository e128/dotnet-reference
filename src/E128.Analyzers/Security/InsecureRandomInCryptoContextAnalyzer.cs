using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128075: Reports <c lang="csharp">System.Random</c> and any class derived from it in two cases. First, a value from
///     Random (a new instance, <c lang="csharp">Random.Shared</c> or a stored instance) flows into a variable, field,
///     property or buffer with a security-sensitive name such as <c lang="csharp">token</c> or
///     <c lang="csharp">salt</c>. Second, a new instance or <c lang="csharp">Random.Shared</c> appears in a file that
///     references a type from <c lang="csharp">System.Security.Cryptography</c> or
///     <c lang="csharp">Microsoft.AspNetCore.Cryptography</c>.
/// </summary>
/// <remarks>
///     Both checks are heuristics. The naming check judges only the name that receives the value, so it can miss
///     a sensitive value with an ordinary name and report an ordinary value with a sensitive name. The file check
///     reports every Random creation in a cryptography file, including a jitter or a shuffle. It does not report a
///     stored Random that the file did not create, and it does not look at other files. Each site produces one
///     diagnostic, and the naming message wins over the file message.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InsecureRandomInCryptoContextAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128075";

    private const string CryptoFileMessage =
        "System.Random is not cryptographically secure and this file uses cryptography APIs; use RandomNumberGenerator for any security-sensitive value";

    // Distinctive fragments matched as case-insensitive substrings, so an all-lowercase compound such as
    // "authtoken" still matches. Each is long enough that it rarely sits inside an unrelated word.
    private static readonly ImmutableArray<string> SensitiveSubstrings = ImmutableArray.Create(
        "token",
        "nonce",
        "password",
        "passwd",
        "credential",
        "apikey",
        "privatekey");

    // Fragments that collide with ordinary words (salt in basalt, secret in secretary, signature in a design
    // name, key in monkey, session in possession) match only as whole words in camelCase or snake_case names. A
    // bare "session" is absent on purpose: sessionCount and sessionTimeout are not secrets, while sessionId and
    // session_id are.
    private static readonly ImmutableArray<string> SensitiveWords = ImmutableArray.Create(
        "secret",
        "salt",
        "challenge",
        "signature",
        "sessionid",
        "session_id",
        "cookie",
        "authcode",
        "verifier",
        "entropy",
        "iv",
        "key",
        "seed",
        "mac",
        "hmac",
        "auth",
        "sig",
        "pin",
        "pwd",
        "otp");

    // .NET 8 members that draw random values without a Next prefix.
    private static readonly ImmutableHashSet<string> StoredRandomMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "GetItems",
        "GetHexString",
        "GetString");

    // The namespace trees whose types mark a file as one that uses cryptography.
    private static readonly string[][] CryptographyNamespaces =
    [
        ["System", "Security", "Cryptography"],
        ["Microsoft", "AspNetCore", "Cryptography"]
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use RandomNumberGenerator instead of Random in security-adjacent code",
        "{0}",
        "E128-Security",
        DiagnosticSeverity.Error,
        true,
        "System.Random is not a cryptographically secure generator. Generate keys, tokens, nonces, IVs and " +
        "salts with RandomNumberGenerator. Both checks are heuristics, so the rule can report an ordinary " +
        "value and can miss a sensitive value.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // A generated file can create Random for a security-sensitive value, and organization policy covers
        // that file, so the rule reads and reports generated code.
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var cryptography = new CryptographyUse(start.Compilation);
            start.RegisterOperationAction(operation => AnalyzeObjectCreation(operation, cryptography), OperationKind.ObjectCreation);
            start.RegisterOperationAction(operation => AnalyzePropertyReference(operation, cryptography), OperationKind.PropertyReference);
            start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        });
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context, CryptographyUse cryptography)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (IsRandomType(creation.Type))
        {
            ReportSite(context, creation, cryptography);
        }
    }

    /// <summary>Returns true when the type is <c lang="csharp">System.Random</c> or derives from it.</summary>
    private static bool IsRandomType(ITypeSymbol? type) =>
        FipsSymbolHelpers.DerivesFromType(type, "Random", "System");

    private static void AnalyzePropertyReference(OperationAnalysisContext context, CryptographyUse cryptography)
    {
        var reference = (IPropertyReferenceOperation)context.Operation;
        var property = reference.Property;

        if (property.IsStatic &&
            string.Equals(property.Name, "Shared", StringComparison.Ordinal) &&
            IsRandomType(property.ContainingType))
        {
            ReportSite(context, reference, cryptography);
        }
    }

    /// <summary>
    ///     Reports a call on a stored <c lang="csharp">Random</c> (a field or local that the file created elsewhere) when the
    ///     value or buffer has a sensitive name. A call on a new instance or <c lang="csharp">Random.Shared</c> belongs to the
    ///     creation site, which this analyzer reports for every Random type it recognizes.
    /// </summary>
    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (method.IsStatic ||
            !(method.Name.StartsWith("Next", StringComparison.Ordinal) || StoredRandomMembers.Contains(method.Name)) ||
            !IsRandomType(method.ContainingType) ||
            IsCreationSite(invocation.Instance))
        {
            return;
        }

        var name = GetSensitiveName(invocation);
        if (name is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                invocation.Syntax.GetLocation(),
                SensitiveMessage(name)));
        }
    }

    /// <summary>
    ///     Returns true when the receiver is a site that <see cref="AnalyzeObjectCreation" /> or
    ///     <see cref="AnalyzePropertyReference" /> reports itself. Any other receiver, such as a creation of a type
    ///     parameter, is not a site, so the call must report on its own.
    /// </summary>
    private static bool IsCreationSite(IOperation? instance)
    {
        return FipsSymbolHelpers.Unwrap(instance) switch
        {
            IObjectCreationOperation creation => IsRandomType(creation.Type),
            IPropertyReferenceOperation { Property: { IsStatic: true, Name: "Shared" } property } => IsRandomType(property.ContainingType),
            _ => false
        };
    }

    private static void ReportSite(
        OperationAnalysisContext context,
        IOperation site,
        CryptographyUse cryptography)
    {
        string? message = null;
        var name = GetSensitiveName(site);
        if (name is not null)
        {
            message = SensitiveMessage(name);
        }
        else if (context.Operation.SemanticModel is { } model &&
                 cryptography.IsReferencedBy(model, site.Syntax.SyntaxTree, context.CancellationToken))
        {
            message = CryptoFileMessage;
        }

        if (message is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, site.Syntax.GetLocation(), message));
    }

    private static string SensitiveMessage(string name)
    {
        return $"'{name}' appears to be security-sensitive but is sourced from System.Random; use RandomNumberGenerator instead";
    }

    /// <summary>
    ///     Returns the sensitive name that receives the value of <paramref name="site" /> (a random call, a new
    ///     instance or <c lang="csharp">Random.Shared</c>), or null. A <c lang="csharp">NextBytes</c> call is judged by its
    ///     buffer, and any other use by the destination of its result.
    /// </summary>
    private static string? GetSensitiveName(IOperation site)
    {
        var name = FindNextBytesCall(site) is { } nextBytes
            ? nextBytes.Arguments.Length > 0 ? GetBufferName(nextBytes.Arguments[0].Value) : null
            : GetDestinationName(site);

        return name is not null && IsSensitive(name) ? name : null;
    }

    /// <summary>
    ///     Returns the <c lang="csharp">NextBytes</c> call that is <paramref name="site" /> or that
    ///     <paramref name="site" /> receives.
    /// </summary>
    private static IInvocationOperation? FindNextBytesCall(IOperation site)
    {
        if (site is IInvocationOperation { TargetMethod.Name: "NextBytes" } call)
        {
            return call;
        }

        var parent = site.Parent;
        while (parent is IConversionOperation)
        {
            parent = parent.Parent;
        }

        return parent is IInvocationOperation { TargetMethod.Name: "NextBytes" } receiver &&
               FipsSymbolHelpers.Unwrap(receiver.Instance) == site
            ? receiver
            : null;
    }

    private static string? GetBufferName(IOperation? buffer)
    {
        while (true)
        {
            switch (FipsSymbolHelpers.Unwrap(buffer))
            {
                case IInvocationOperation { Instance: { } instance }:
                    buffer = instance;
                    break;
                case IInvocationOperation { TargetMethod.IsExtensionMethod: true, Arguments.Length: > 0 } extension:
                    buffer = extension.Arguments[0].Value;
                    break;
                case { } operation:
                    return TargetSymbolName(operation);
                default:
                    return null;
            }
        }
    }

    /// <summary>
    ///     Walks up from the random value to the variable, field or property that receives it, so the naming
    ///     heuristic can judge it. The walk passes through conversions, operators, string interpolation, member
    ///     access on the value (<c lang="csharp">new Random().Next().ToString()</c>) and arguments of a call that returns a
    ///     plain value (<c lang="csharp">BitConverter.GetBytes(random.Next())</c>). It stops at a lambda, a local
    ///     function, a call that returns another type and any statement, so the search stays inside the expression.
    /// </summary>
    private static string? GetDestinationName(IOperation operation)
    {
        var child = operation;
        for (var current = operation.Parent; current is not null; child = current, current = current.Parent)
        {
            switch (current)
            {
                case IVariableDeclaratorOperation declarator:
                    return declarator.Symbol.Name;
                case IFieldInitializerOperation { InitializedFields.Length: > 0 } fieldInitializer:
                    return fieldInitializer.InitializedFields[0].Name;
                case IPropertyInitializerOperation { InitializedProperties.Length: > 0 } propertyInitializer:
                    return propertyInitializer.InitializedProperties[0].Name;
                case IAssignmentOperation assignment:
                    return assignment.Value == child ? TargetSymbolName(assignment.Target) : null;
                case IInvocationOperation invocation:
                    if (invocation.Instance == child || (child is IArgumentOperation && IsValueLike(invocation.Type)))
                    {
                        continue;
                    }

                    return null;
                case IPropertyReferenceOperation property when property.Instance == child:
                case IFieldReferenceOperation field when field.Instance == child:
                case IArgumentOperation:
                case IVariableInitializerOperation:
                case IConversionOperation:
                case IParenthesizedOperation:
                case IBinaryOperation:
                case IUnaryOperation:
                case IConditionalOperation:
                case ICoalesceOperation:
                case IInterpolatedStringOperation:
                case IInterpolatedStringContentOperation:
                    continue;
                default:
                    return null;
            }
        }

        return null;
    }

    /// <summary>A plain value: a primitive, a string, or an array of them.</summary>
    private static bool IsValueLike(ITypeSymbol? type)
    {
        return type switch
        {
            null => false,
            IArrayTypeSymbol array => IsValueLike(array.ElementType),
            _ => type.SpecialType == SpecialType.System_String ||
                 (type.IsValueType && type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Void)
        };
    }

    private static string? TargetSymbolName(IOperation target)
    {
        return target switch
        {
            ILocalReferenceOperation local => local.Local.Name,
            IFieldReferenceOperation field => field.Field.Name,
            IPropertyReferenceOperation property => property.Property.Name,
            IParameterReferenceOperation parameter => parameter.Parameter.Name,
            _ => null
        };
    }

    private static bool IsSensitive(string name)
    {
        foreach (var fragment in SensitiveSubstrings)
        {
            if (name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        foreach (var word in SensitiveWords)
        {
            if (FipsSymbolHelpers.ContainsWord(name, word))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Answers, for one compilation, whether a file references a type or member of a cryptography namespace. The
    ///     answer is cached per syntax tree and computed only when the file creates a Random.
    /// </summary>
    private sealed class CryptographyUse
    {
        private readonly ConcurrentDictionary<SyntaxTree, bool> _files = new();
        private readonly Lazy<ImmutableHashSet<string>> _candidateNames;

        public CryptographyUse(Compilation compilation)
        {
            _candidateNames = new Lazy<ImmutableHashSet<string>>(() => CollectCandidateNames(compilation));
        }

        /// <summary>
        ///     Returns true when an identifier in the file resolves to a type or member of a cryptography namespace.
        ///     Only a known type, namespace, or member name, plus <c lang="csharp">var</c>, is bound, because every other
        ///     identifier cannot resolve to one. A concurrent scan of the same file computes the same answer and the
        ///     first stored answer wins.
        /// </summary>
        public bool IsReferencedBy(SemanticModel model, SyntaxTree tree, CancellationToken cancellationToken)
        {
            if (_files.TryGetValue(tree, out var cached))
            {
                return cached;
            }

            // A canceled scan throws before it stores a result, so a partial answer is never cached.
            var found = Scan(model, tree, cancellationToken);
            _files.TryAdd(tree, found);

            return found;
        }

        private bool Scan(SemanticModel model, SyntaxTree tree, CancellationToken cancellationToken)
        {
            var names = _candidateNames.Value;
            if (names.IsEmpty)
            {
                return false;
            }

            foreach (var node in tree.GetRoot(cancellationToken).DescendantNodes())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (node is not SimpleNameSyntax name)
                {
                    continue;
                }

                // A 'var' declaration takes the type of its initializer, so it can name a cryptography type
                // that the file never spells out.
                var text = name.Identifier.ValueText;
                if (!string.Equals(text, "var", StringComparison.Ordinal) && !names.Contains(text))
                {
                    continue;
                }

                var symbol = model.GetSymbolInfo(name, cancellationToken).Symbol;
                var type = symbol as ITypeSymbol ?? symbol?.ContainingType;
                if (type is not null && IsCryptographyNamespace(type))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Collects the namespace, type, and member names of the cryptography namespace trees that the compilation
        ///     can see, from its references and its own source. Names include referenced and source symbols. A name for
        ///     an attribute class also appears without the <c lang="csharp">Attribute</c> suffix, because source can omit
        ///     the suffix.
        /// </summary>
        private static ImmutableHashSet<string> CollectCandidateNames(Compilation compilation)
        {
            var names = ImmutableHashSet.CreateBuilder(StringComparer.Ordinal);
            AddAssemblyNames(compilation.Assembly.GlobalNamespace, names);
            foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                AddAssemblyNames(assembly.GlobalNamespace, names);
            }

            return names.ToImmutable();
        }

        private static void AddAssemblyNames(INamespaceSymbol root, ImmutableHashSet<string>.Builder names)
        {
            foreach (var path in CryptographyNamespaces)
            {
                var ns = root;
                foreach (var part in path)
                {
                    ns = FindChild(ns, part);
                    if (ns is null)
                    {
                        break;
                    }
                }

                if (ns is not null)
                {
                    AddNamespaceNames(ns, names);
                }
            }
        }

        private static INamespaceSymbol? FindChild(INamespaceSymbol parent, string name)
        {
            foreach (var child in parent.GetNamespaceMembers())
            {
                if (string.Equals(child.Name, name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static void AddNamespaceNames(INamespaceSymbol ns, ImmutableHashSet<string>.Builder names)
        {
            names.Add(ns.Name);
            foreach (var type in ns.GetTypeMembers())
            {
                AddTypeNames(type, names);
            }

            foreach (var child in ns.GetNamespaceMembers())
            {
                AddNamespaceNames(child, names);
            }
        }

        private static void AddTypeNames(INamedTypeSymbol type, ImmutableHashSet<string>.Builder names)
        {
            names.Add(type.Name);
            if (type.Name.EndsWith("Attribute", StringComparison.Ordinal) && type.Name.Length > "Attribute".Length)
            {
                names.Add(type.Name.Substring(0, type.Name.Length - "Attribute".Length));
            }

            // A member reached through a value from another file can mark this file without spelling its type.
            foreach (var member in type.GetMembers())
            {
                names.Add(member.Name);
            }

            foreach (var nested in type.GetTypeMembers())
            {
                AddTypeNames(nested, names);
            }
        }

        private static bool IsCryptographyNamespace(ITypeSymbol type)
        {
            if (FipsSymbolHelpers.IsInCryptographyNamespaceTree(type))
            {
                return true;
            }

            for (var ns = type.ContainingNamespace; ns is not null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
            {
                if (Namespaces.Equals(ns, FipsSymbolHelpers.AspNetCryptography))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
