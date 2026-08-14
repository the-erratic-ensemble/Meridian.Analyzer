using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0041ModelLiteralNullSuppressionAsNullableStateAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0041";

    private static readonly LocalizableString Title = "Model literal null suppression as nullable state";

    private static readonly LocalizableString MessageFormat =
        "Model this {0} value as nullable state instead of using null suppression";

    private static readonly LocalizableString Description =
        "Null-forgiving literal and default values hide a deliberate nullable state that should be represented in the type or result shape.";

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
        context.RegisterSyntaxNodeAction(AnalyzeSuppression, SyntaxKind.SuppressNullableWarningExpression);
    }

    private static void AnalyzeSuppression(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not PostfixUnaryExpressionSyntax suppression ||
            MeridianAnalyzerRuleHelpers.IsTestPath(suppression.SyntaxTree.FilePath) ||
            !IsLiteralOrDefault(context, suppression.Operand))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            suppression.GetLocation(),
            GetSuppressedValueDescription(context, suppression.Operand)));
    }

    private static bool IsLiteralOrDefault(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression)
    {
        expression = Unwrap(expression);
        if (expression is DefaultExpressionSyntax)
            return context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type is not null;

        return expression is LiteralExpressionSyntax
            {
                RawKind: (int)SyntaxKind.NullLiteralExpression or (int)SyntaxKind.DefaultLiteralExpression
            };
    }

    private static string GetSuppressedValueDescription(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression)
    {
        expression = Unwrap(expression);
        if (expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.NullLiteralExpression))
            return "null";

        if (expression is DefaultExpressionSyntax defaultExpression &&
            context.SemanticModel.GetTypeInfo(defaultExpression, context.CancellationToken).Type is { } type)
            return $"default({type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)})";

        return "default";
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
            expression = parenthesized.Expression;

        return expression;
    }
}
