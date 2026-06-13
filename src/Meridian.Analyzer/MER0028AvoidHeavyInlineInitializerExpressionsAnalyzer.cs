using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0028AvoidHeavyInlineInitializerExpressionsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0028";

    private const int MinimumLineSpan = 8;
    private const int MinimumComplexityScore = 8;

    private static readonly LocalizableString Title = "Avoid heavy inline expressions in initializer members";
    private static readonly LocalizableString MessageFormat =
        "Move this {0}-line initializer member expression into named locals or a helper";
    private static readonly LocalizableString Description =
        "Large multi-line expressions inside object and anonymous-object initializer members hide business logic in construction code. " +
        "Stage the logic before the initializer or move it into a helper.";

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
        context.RegisterSyntaxNodeAction(AnalyzeAnonymousObjectMember, SyntaxKind.AnonymousObjectMemberDeclarator);
        context.RegisterSyntaxNodeAction(AnalyzeAssignmentExpression, SyntaxKind.SimpleAssignmentExpression);
    }

    private static void AnalyzeAnonymousObjectMember(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not AnonymousObjectMemberDeclaratorSyntax member ||
            member.Expression is null)
        {
            return;
        }

        AnalyzeInitializerExpression(context, member.Expression);
    }

    private static void AnalyzeAssignmentExpression(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not AssignmentExpressionSyntax assignment ||
            assignment.Parent is not InitializerExpressionSyntax initializer ||
            !initializer.IsKind(SyntaxKind.ObjectInitializerExpression))
        {
            return;
        }

        AnalyzeInitializerExpression(context, assignment.Right);
    }

    private static void AnalyzeInitializerExpression(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        if (IsNestedObjectConstruction(expression))
        {
            return;
        }

        var lineCount = GetLineCount(expression);
        if (lineCount < MinimumLineSpan)
        {
            return;
        }

        var complexityScore = GetComplexityScore(expression);
        if (complexityScore < MinimumComplexityScore)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, expression.GetLocation(), lineCount));
    }

    private static bool IsNestedObjectConstruction(ExpressionSyntax expression)
    {
        return expression is AnonymousObjectCreationExpressionSyntax
            or ObjectCreationExpressionSyntax
            or ImplicitObjectCreationExpressionSyntax;
    }

    private static int GetLineCount(SyntaxNode node)
    {
        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        return lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
    }

    private static int GetComplexityScore(ExpressionSyntax expression)
    {
        var conditionalCount = expression.DescendantNodesAndSelf().OfType<ConditionalExpressionSyntax>().Count();
        var invocationCount = expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Count();
        var logicalOperatorCount = expression.DescendantNodesAndSelf()
            .OfType<BinaryExpressionSyntax>()
            .Count(binaryExpression =>
                binaryExpression.IsKind(SyntaxKind.LogicalAndExpression) ||
                binaryExpression.IsKind(SyntaxKind.LogicalOrExpression));

        return (conditionalCount * 3) + invocationCount + logicalOperatorCount;
    }
}
