using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0027AvoidLongBooleanChainsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0027";

    private const int MinimumLogicalOperatorCount = 5;

    private static readonly LocalizableString Title = "Avoid overly long boolean condition chains";
    private static readonly LocalizableString MessageFormat =
        "Extract named predicates from this {0}-clause boolean chain";
    private static readonly LocalizableString Description =
        "Long `&&` and `||` chains are hard to review inline. " +
        "Extract named predicates or split the logic into clearer steps.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Readability,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeLogicalExpression, SyntaxKind.LogicalAndExpression, SyntaxKind.LogicalOrExpression);
    }

    private static void AnalyzeLogicalExpression(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not BinaryExpressionSyntax logicalExpression)
        {
            return;
        }

        if (logicalExpression.Parent is BinaryExpressionSyntax parent &&
            IsLogicalBinary(parent))
        {
            return;
        }

        var logicalOperatorCount = CountLogicalOperators(logicalExpression);
        if (logicalOperatorCount < MinimumLogicalOperatorCount)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, logicalExpression.GetLocation(), logicalOperatorCount + 1));
    }

    private static int CountLogicalOperators(SyntaxNode expression)
    {
        return expression
            .DescendantNodesAndSelf()
            .OfType<BinaryExpressionSyntax>()
            .Count(IsLogicalBinary);
    }

    private static bool IsLogicalBinary(BinaryExpressionSyntax expression)
    {
        return expression.IsKind(SyntaxKind.LogicalAndExpression) ||
               expression.IsKind(SyntaxKind.LogicalOrExpression);
    }
}
