using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0043OrderUnorderedCollectionsBeforePositionalSelectionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0043";

    private static readonly LocalizableString Title = "Order unordered collections before positional selection";

    private static readonly LocalizableString MessageFormat =
        "Order this dictionary or set before using positional selection";

    private static readonly LocalizableString Description =
        "Position-based selection from dictionary and set data should state the order that determines the selected item.";

    private static readonly string[] PositionalMethodNames =
    {
        "First",
        "FirstOrDefault",
        "Last",
        "LastOrDefault",
        "ElementAt",
        "ElementAtOrDefault",
        "Take",
        "Skip"
    };

    private static readonly string[] OrderingMethodNames =
    {
        "OrderBy",
        "OrderByDescending",
        "ThenBy",
        "ThenByDescending"
    };

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Reliability,
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
            MeridianAnalyzerRuleHelpers.IsTestPath(invocation.SyntaxTree.FilePath) ||
            context.SemanticModel.GetSymbolInfo(
                invocation,
                context.CancellationToken).Symbol is not IMethodSymbol method ||
            !IsEnumerableMethod(method) ||
            !PositionalMethodNames.Contains(method.Name, StringComparer.Ordinal) ||
            GetSourceExpression(context, invocation) is not { } source ||
            GetEnumerationState(context, source) != EnumerationState.Unordered)
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static ExpressionSyntax? GetSourceExpression(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            !IsStaticEnumerableTarget(context, memberAccess.Expression))
            return memberAccess.Expression;

        if (invocation.ArgumentList.Arguments.Count > 0)
            return invocation.ArgumentList.Arguments[0].Expression;

        return null;
    }

    private static bool IsStaticEnumerableTarget(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression)
    {
        return context.SemanticModel.GetSymbolInfo(
                expression,
                context.CancellationToken).Symbol is INamedTypeSymbol type &&
               IsEnumerableType(type);
    }

    private static bool IsEnumerableType(INamedTypeSymbol type)
    {
        return string.Equals(type.Name, "Enumerable", StringComparison.Ordinal) &&
               string.Equals(type.ContainingNamespace?.ToDisplayString(), "System.Linq",
                   StringComparison.Ordinal);
    }

    private static EnumerationState GetEnumerationState(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression)
    {
        expression = Unwrap(expression);

        if (expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText is "Keys" or "Values" &&
            IsUnorderedCollection(context.SemanticModel.GetTypeInfo(
                memberAccess.Expression,
                context.CancellationToken).Type))
            return EnumerationState.Unordered;

        if (IsUnorderedCollection(context.SemanticModel.GetTypeInfo(
                expression,
                context.CancellationToken).Type))
            return EnumerationState.Unordered;

        if (expression is not InvocationExpressionSyntax invocation)
            return EnumerationState.None;

        var method = context.SemanticModel.GetSymbolInfo(
            invocation,
            context.CancellationToken).Symbol as IMethodSymbol;
        if (method is null || !IsEnumerableMethod(method))
            return EnumerationState.None;

        if (OrderingMethodNames.Contains(method.Name, StringComparer.Ordinal))
            return EnumerationState.Ordered;

        if (IsUnorderedCollection(context.SemanticModel.GetTypeInfo(
                invocation,
                context.CancellationToken).Type))
            return EnumerationState.Unordered;

        var source = GetSourceExpression(context, invocation);
        return source is null
            ? EnumerationState.None
            : GetEnumerationState(context, source);
    }

    private static bool IsEnumerableMethod(IMethodSymbol method)
    {
        return string.Equals(method.ContainingType?.Name, "Enumerable", StringComparison.Ordinal) &&
               string.Equals(method.ContainingNamespace?.ToDisplayString(), "System.Linq",
                   StringComparison.Ordinal);
    }

    private static bool IsUnorderedCollection(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol namedType &&
               (IsUnorderedCollectionDefinition(namedType) ||
                namedType.AllInterfaces.Any(IsUnorderedCollectionDefinition));
    }

    private static bool IsUnorderedCollectionDefinition(ITypeSymbol type)
    {
        return string.Equals(type.ContainingNamespace?.ToDisplayString(), "System.Collections.Generic",
                   StringComparison.Ordinal) &&
               type.Name is "Dictionary" or "IDictionary" or "IReadOnlyDictionary" or
                   "HashSet" or "ISet" or "IReadOnlySet";
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
            expression = parenthesized.Expression;

        return expression;
    }

    private enum EnumerationState
    {
        None,
        Unordered,
        Ordered
    }
}
