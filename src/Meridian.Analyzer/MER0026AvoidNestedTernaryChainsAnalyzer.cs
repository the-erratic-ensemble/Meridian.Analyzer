using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0026AvoidNestedTernaryChainsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0026";

    private const int MinimumConditionalChainLength = 3;

    private static readonly LocalizableString Title = "Avoid deeply nested ternary chains";
    private static readonly LocalizableString MessageFormat =
        "Extract this {0}-branch conditional chain into named steps";
    private static readonly LocalizableString Description =
        "Long nested conditional-expression chains bury decision trees in one expression. " +
        "Stage the classification or ranking steps in named locals or helpers.";

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
        context.RegisterSyntaxNodeAction(AnalyzeConditionalExpression, SyntaxKind.ConditionalExpression);
    }

    private static void AnalyzeConditionalExpression(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ConditionalExpressionSyntax conditionalExpression)
        {
            return;
        }

        if (conditionalExpression.Parent is ConditionalExpressionSyntax)
        {
            return;
        }

        var conditionalCount = CountConditionalExpressions(conditionalExpression);
        if (conditionalCount < MinimumConditionalChainLength)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, conditionalExpression.GetLocation(), conditionalCount));
    }

    private static int CountConditionalExpressions(ExpressionSyntax expression)
    {
        if (expression is not ConditionalExpressionSyntax conditionalExpression)
        {
            return 0;
        }

        return 1
               + CountConditionalExpressions(conditionalExpression.WhenTrue)
               + CountConditionalExpressions(conditionalExpression.WhenFalse);
    }
}
