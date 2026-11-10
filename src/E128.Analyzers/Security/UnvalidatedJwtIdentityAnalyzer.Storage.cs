using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

public sealed partial class UnvalidatedJwtIdentityAnalyzer
{
    private sealed partial class BodyAnalysis
    {
        private void VisitNested(IOperation? body, SyntaxNode syntax)
        {
            var nested = new BodyAnalysis(types, localFunctions, this, syntax);
            nested.Visit(body);
            _nestedBodies.Add(nested);
            MergePositions(_opaque, nested._opaque);
            _added.AddRange(nested._added);
            MergePositions(_writes, nested._writes);
        }

        private void AddDefinition(ILocalSymbol local, IOperation value, SyntaxNode syntax)
        {
            if (!_definitions.TryGetValue(local, out var definitions))
            {
                definitions = [];
                _definitions[local] = definitions;
            }

            definitions.Add((value, syntax));
        }

        private void RecordWrite(IOperation target, int position)
        {
            if (StorageSymbol(target) is not { } symbol)
            {
                return;
            }

            AddWrite(symbol, position);
        }

        private void AddWrite(ISymbol symbol, int position)
        {
            AddPosition(_writes, symbol, position);
        }

        private static void AddPosition(Dictionary<ISymbol, List<int>> writes, ISymbol symbol, int position)
        {
            if (!writes.TryGetValue(symbol, out var positions))
            {
                positions = [];
                writes[symbol] = positions;
            }

            positions.Add(position);
        }

        private static void MergePositions(Dictionary<ISymbol, List<int>> target, Dictionary<ISymbol, List<int>> source)
        {
            foreach (var pair in source)
            {
                foreach (var position in pair.Value)
                {
                    AddPosition(target, pair.Key, position);
                }
            }
        }

        private bool OpaqueBefore(ISymbol symbol, int position)
        {
            return _opaque.TryGetValue(symbol, out var positions) && positions.Exists(write => write < position);
        }

        private void MarkOpaque(IOperation target, int position)
        {
            if (StorageSymbol(target) is not { } symbol)
            {
                return;
            }

            AddPosition(_opaque, symbol, position);
        }

        private void MarkOpaqueWrite(IOperation target, SyntaxNode syntax)
        {
            RecordWrite(target, syntax.SpanStart);
            MarkOpaque(target, syntax.SpanStart);
        }

        private static IOperation? ReceiverOf(IOperation? instance)
        {
            if (instance is not IConditionalAccessInstanceOperation)
            {
                return instance;
            }

            var current = instance.Parent;
            while (current is not null and not IConditionalAccessOperation)
            {
                current = current.Parent;
            }

            return (current as IConditionalAccessOperation)?.Operation;
        }

        private static bool IsAddMethod(string name)
        {
            return AddMethodNames.Contains(name);
        }

        private static IEnumerable<ILocalSymbol> PassedResults(IOperation condition)
        {
            condition = Unwrap(condition);
            if (condition is IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd } both)
            {
                return PassedResults(both.LeftOperand).Concat(PassedResults(both.RightOperand));
            }

            var passed = condition is IBinaryOperation
            {
                OperatorKind: BinaryOperatorKind.Equals,
                RightOperand.ConstantValue: { HasValue: true, Value: true }
            } equals
                ? equals.LeftOperand
                : condition;
            return ValidResult(passed) is { } result ? [result] : [];
        }

        private static ILocalSymbol? ValidResult(IOperation operation)
        {
            return operation is IPropertyReferenceOperation { Instance: ILocalReferenceOperation local } property
                   && string.Equals(property.Property.Name, "IsValid", StringComparison.Ordinal)
                ? local.Local
                : null;
        }

        private static ILocalSymbol? FailedResult(IOperation condition)
        {
            var failed = Unwrap(condition) switch
            {
                IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } not => not.Operand,
                IBinaryOperation
                {
                    OperatorKind: BinaryOperatorKind.Equals,
                    RightOperand.ConstantValue: { HasValue: true, Value: false }
                } equals => equals.LeftOperand,
                _ => null
            };
            return failed is null ? null : ValidResult(failed);
        }

        private static bool Exits(IOperation operation)
        {
            return operation switch
            {
                IReturnOperation { Kind: OperationKind.Return or OperationKind.YieldBreak } => true,
                IThrowOperation or IExpressionStatementOperation { Operation: IThrowOperation } => true,
                IBlockOperation { Operations.Length: > 0 } block => Exits(block.Operations[block.Operations.Length - 1]),
                _ => false
            };
        }

        private static IOperation? Argument(ImmutableArray<IArgumentOperation> arguments, string name)
        {
            foreach (var argument in arguments)
            {
                if (string.Equals(argument.Parameter?.Name, name, StringComparison.Ordinal))
                {
                    return argument.Value;
                }
            }

            return null;
        }

        private static ISymbol? StorageSymbol(IOperation? operation)
        {
            return operation is null ? null : Unwrap(operation) switch
            {
                ILocalReferenceOperation local => local.Local,
                IParameterReferenceOperation parameter => parameter.Parameter,
                IFieldReferenceOperation field => field.Field,
                IPropertyReferenceOperation property when IsStableProperty(property.Property) => property.Property,
                _ => null
            };
        }

        private static bool IsStableProperty(IPropertySymbol property)
        {
            return !property.IsIndexer && property.DeclaringSyntaxReferences.Any(static reference =>
                reference.GetSyntax(CancellationToken.None) is PropertyDeclarationSyntax { ExpressionBody: null } declaration
                && declaration.AccessorList?.Accessors.Any(static accessor =>
                    accessor.RawKind == (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.GetAccessorDeclaration
                    && accessor.Body is null && accessor.ExpressionBody is null) is true);
        }

        private static bool SameStorage(IOperation? left, IOperation? right)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }

            left = Unwrap(left);
            right = Unwrap(right);
            return left is IInstanceReferenceOperation leftThis && right is IInstanceReferenceOperation rightThis
                ? leftThis.ReferenceKind == rightThis.ReferenceKind
                  && SymbolEqualityComparer.Default.Equals(leftThis.Type, rightThis.Type)
                : StorageSymbol(left) is { } symbol
                  && SymbolEqualityComparer.Default.Equals(symbol, StorageSymbol(right))
                  && SameStorage(StorageReceiver(left), StorageReceiver(right));
        }

        private static IOperation? StorageReceiver(IOperation operation)
        {
            return operation switch
            {
                IFieldReferenceOperation field => field.Instance,
                IPropertyReferenceOperation property => property.Instance,
                _ => null
            };
        }

        private static IOperation Unwrap(IOperation operation)
        {
            while (operation is IConversionOperation { OperatorMethod: null } conversion)
            {
                operation = conversion.Operand;
            }

            return operation;
        }
    }
}
