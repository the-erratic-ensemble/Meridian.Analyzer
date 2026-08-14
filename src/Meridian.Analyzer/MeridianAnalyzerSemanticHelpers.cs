using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Meridian.Analyzer;

internal static class MeridianAnalyzerSemanticHelpers
{
    internal static bool IsTypeOrDerivedFrom(ITypeSymbol? type, string namespaceName, string typeName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.Name, typeName, StringComparison.Ordinal) &&
                string.Equals(current.ContainingNamespace?.ToDisplayString(), namespaceName,
                    StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    internal static bool Implements(ITypeSymbol? type, string namespaceName, string typeName)
    {
        return type?.AllInterfaces.Any(interfaceType =>
            string.Equals(interfaceType.Name, typeName, StringComparison.Ordinal) &&
            string.Equals(interfaceType.ContainingNamespace?.ToDisplayString(), namespaceName,
                StringComparison.Ordinal)) == true;
    }

    internal static ISymbol? GetReferencedSymbol(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        expression = Unwrap(expression);
        return semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol;
    }

    internal static bool IsSameReference(
        ExpressionSyntax left,
        ExpressionSyntax right,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var leftSymbol = GetReferencedSymbol(left, semanticModel, cancellationToken);
        var rightSymbol = GetReferencedSymbol(right, semanticModel, cancellationToken);

        return leftSymbol is not null &&
               rightSymbol is not null &&
               SymbolEqualityComparer.Default.Equals(leftSymbol, rightSymbol);
    }

    internal static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (true)
        {
            expression = expression switch
            {
                ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
                CastExpressionSyntax cast => cast.Expression,
                PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } nullable =>
                    nullable.Operand,
                _ => expression
            };

            if (expression is not (ParenthesizedExpressionSyntax or CastExpressionSyntax or
                PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression }))
                return expression;
        }
    }
}
