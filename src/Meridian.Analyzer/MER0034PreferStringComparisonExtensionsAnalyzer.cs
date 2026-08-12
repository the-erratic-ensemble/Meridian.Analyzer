using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0034PreferStringComparisonExtensionsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0034";

    private static readonly LocalizableString Title = "Prefer shared string comparison helpers";

    private static readonly LocalizableString MessageFormat =
        "Use {0} for this ordinal string comparison";

    private static readonly LocalizableString Description =
        "Direct ordinal string Equals and Contains calls with StringComparison duplicate the shared nullable string comparison helpers.";

    private static readonly string[] ExcludedPathSegments =
    {
        "/StringExtensions.cs",
        "/Meridian.Analyzer/"
    };

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Readability,
        DiagnosticSeverity.Warning,
        true,
        Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            IsExcludedLocation(invocation.SyntaxTree.FilePath) ||
            IsInsideQueryableOrExpressionQuery(context, invocation))
            return;

        if (!TryGetSuggestedHelper(context, invocation, out var helperName)) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), helperName));
    }

    private static bool TryGetSuggestedHelper(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        out string helperName)
    {
        helperName = string.Empty;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var comparisonKind = GetSupportedComparisonKind(context, invocation);
        if (comparisonKind is null) return false;

        if (IsStaticStringEquals(context, invocation, memberAccess))
        {
            helperName = comparisonKind == "OrdinalIgnoreCase"
                ? "EqualsOrdinalIgnoreCase"
                : "EqualsOrdinal";
            return true;
        }

        if (IsInstanceStringEquals(context, invocation, memberAccess))
        {
            helperName = comparisonKind == "OrdinalIgnoreCase"
                ? "EqualsOrdinalIgnoreCase"
                : "EqualsOrdinal";
            return true;
        }

        if (IsInstanceStringContains(context, invocation, memberAccess))
        {
            helperName = comparisonKind == "OrdinalIgnoreCase"
                ? "ContainsOrdinalIgnoreCase"
                : "ContainsOrdinal";
            return true;
        }

        return false;
    }

    private static string? GetSupportedComparisonKind(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is not MemberAccessExpressionSyntax memberAccess) continue;

            var symbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
            if (symbol is not IFieldSymbol
                {
                    Name: "Ordinal" or "OrdinalIgnoreCase",
                    ContainingType: { } containingType
                } ||
                !IsStringComparisonType(containingType))
                continue;

            return symbol.Name;
        }

        return null;
    }

    private static bool IsStaticStringEquals(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess)
    {
        if (memberAccess.Name.Identifier.ValueText != "Equals") return false;

        if (!IsStringTypeReference(memberAccess.Expression)) return false;

        if (invocation.ArgumentList.Arguments.Count < 3) return false;

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (symbol is null ||
            !symbol.IsStatic ||
            symbol.Name != "Equals" ||
            !IsStringType(symbol.ContainingType) ||
            !HasStringComparisonParameter(symbol))
            return false;

        return IsStringOrNullExpression(context, invocation.ArgumentList.Arguments[0].Expression) &&
               IsStringOrNullExpression(context, invocation.ArgumentList.Arguments[1].Expression);
    }

    private static bool IsInstanceStringEquals(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess)
    {
        if (memberAccess.Name.Identifier.ValueText != "Equals") return false;

        if (invocation.ArgumentList.Arguments.Count < 2) return false;

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (symbol is null ||
            symbol.IsStatic ||
            symbol.Name != "Equals" ||
            !IsStringType(symbol.ContainingType) ||
            !HasStringComparisonParameter(symbol))
            return false;

        return IsStringOrNullExpression(context, memberAccess.Expression) &&
               IsStringOrNullExpression(context, invocation.ArgumentList.Arguments[0].Expression);
    }

    private static bool IsInstanceStringContains(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess)
    {
        if (memberAccess.Name.Identifier.ValueText != "Contains") return false;

        if (invocation.ArgumentList.Arguments.Count < 2) return false;

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (symbol is null ||
            symbol.IsStatic ||
            symbol.Name != "Contains" ||
            !IsStringType(symbol.ContainingType) ||
            !HasStringComparisonParameter(symbol))
            return false;

        return IsStringOrNullExpression(context, memberAccess.Expression) &&
               IsStringOrNullExpression(context, invocation.ArgumentList.Arguments[0].Expression);
    }

    private static bool HasStringComparisonParameter(IMethodSymbol symbol)
    {
        return symbol.Parameters.Any(parameter => IsStringComparisonType(parameter.Type));
    }

    private static bool IsStringOrNullExpression(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression)
    {
        if (expression.IsKind(SyntaxKind.NullLiteralExpression)) return true;

        var typeInfo = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken);
        return IsStringType(typeInfo.Type) || IsStringType(typeInfo.ConvertedType);
    }

    private static bool IsStringTypeReference(ExpressionSyntax expression)
    {
        return expression.ToString() is "string" or "String" or "System.String";
    }

    private static bool IsStringType(ITypeSymbol? typeSymbol)
    {
        return typeSymbol?.SpecialType == SpecialType.System_String;
    }

    private static bool IsStringComparisonType(ITypeSymbol typeSymbol)
    {
        return string.Equals(typeSymbol.Name, "StringComparison", StringComparison.Ordinal) &&
               string.Equals(typeSymbol.ContainingNamespace?.ToDisplayString(), "System", StringComparison.Ordinal);
    }

    private static bool IsInsideQueryableOrExpressionQuery(
        SyntaxNodeAnalysisContext context,
        SyntaxNode node)
    {
        return IsInsideQueryableLambda(context, node) ||
               IsInsideQueryableQuerySyntax(context, node) ||
               IsInsideExpressionTreeLambda(context, node);
    }

    private static bool IsInsideQueryableLambda(
        SyntaxNodeAnalysisContext context,
        SyntaxNode node)
    {
        var lambdaExpression = node.AncestorsAndSelf().OfType<LambdaExpressionSyntax>().FirstOrDefault();
        if (lambdaExpression is null) return false;

        var argument = lambdaExpression.Parent as ArgumentSyntax;
        var invocation = argument?.Parent?.Parent as InvocationExpressionSyntax;
        if (invocation is null) return false;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol symbol)
            return IsSystemLinqQueryableMethod(symbol);

        var receiver = invocation.Expression is MemberAccessExpressionSyntax memberAccess
            ? memberAccess.Expression
            : null;
        if (receiver is null) return false;

        var receiverType = context.SemanticModel.GetTypeInfo(receiver, context.CancellationToken).Type;
        return receiverType is not null && IsIQueryableType(receiverType);
    }

    private static bool IsInsideQueryableQuerySyntax(
        SyntaxNodeAnalysisContext context,
        SyntaxNode node)
    {
        var queryExpression = node.AncestorsAndSelf().OfType<QueryExpressionSyntax>().FirstOrDefault();
        if (queryExpression is null) return false;

        var sourceType = context.SemanticModel
            .GetTypeInfo(queryExpression.FromClause.Expression, context.CancellationToken)
            .Type;
        return sourceType is not null && IsIQueryableType(sourceType);
    }

    private static bool IsInsideExpressionTreeLambda(
        SyntaxNodeAnalysisContext context,
        SyntaxNode node)
    {
        var lambdaExpression = node.AncestorsAndSelf().OfType<LambdaExpressionSyntax>().FirstOrDefault();
        if (lambdaExpression is null) return false;

        var convertedType = context.SemanticModel.GetTypeInfo(lambdaExpression, context.CancellationToken).ConvertedType;
        return convertedType is not null && IsExpressionTreeType(convertedType);
    }

    private static bool IsSystemLinqQueryableMethod(IMethodSymbol methodSymbol)
    {
        return string.Equals(methodSymbol.ContainingType?.Name, "Queryable", StringComparison.Ordinal) &&
               string.Equals(methodSymbol.ContainingNamespace?.ToDisplayString(), "System.Linq",
                   StringComparison.Ordinal);
    }

    private static bool IsIQueryableType(ITypeSymbol type)
    {
        return IsIQueryableNamedType(type) ||
               type.AllInterfaces.Any(IsIQueryableNamedType);
    }

    private static bool IsIQueryableNamedType(ITypeSymbol type)
    {
        return string.Equals(type.Name, "IQueryable", StringComparison.Ordinal) &&
               string.Equals(type.ContainingNamespace?.ToDisplayString(), "System.Linq", StringComparison.Ordinal);
    }

    private static bool IsExpressionTreeType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               string.Equals(namedType.Name, "Expression", StringComparison.Ordinal) &&
               string.Equals(namedType.ContainingNamespace?.ToDisplayString(), "System.Linq.Expressions",
                   StringComparison.Ordinal);
    }

    private static bool IsExcludedLocation(string filePath)
    {
        return MeridianAnalyzerSyntaxHelpers.PathContainsAny(filePath, ExcludedPathSegments);
    }
}
