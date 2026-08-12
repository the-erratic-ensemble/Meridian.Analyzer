using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0032AvoidInlineConditionalRecordRewritesAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0032";

    private const int MinimumAssignmentCount = 2;
    private const int MinimumWithBranchCount = 2;
    private const int MinimumLineSpan = 4;

    private static readonly LocalizableString Title = "Avoid inline conditional record rewrites";

    private static readonly LocalizableString MessageFormat =
        "Extract this conditional record rewrite into a named helper";

    private static readonly LocalizableString Description =
        "Conditional LINQ projections that clone a record across multiple lines hide update rules inside one expression. " +
        "Move the rewrite into a named helper or stage the member changes first.";

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
        context.RegisterSyntaxNodeAction(AnalyzeConditionalExpression, SyntaxKind.ConditionalExpression);
    }

    private static void AnalyzeConditionalExpression(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ConditionalExpressionSyntax conditionalExpression) return;

        if (!IsSelectLambdaBody(conditionalExpression)) return;

        if (!IsReportableConditionalRecordRewrite(conditionalExpression)) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, conditionalExpression.GetLocation()));
    }

    private static bool IsReportableConditionalRecordRewrite(ConditionalExpressionSyntax conditionalExpression)
    {
        if (TryGetReportedWithExpression(conditionalExpression, out var withExpression) &&
            IsReportableWithExpression(withExpression))
            return true;

        return CountWithBranches(conditionalExpression) >= MinimumWithBranchCount;
    }

    private static bool IsSelectLambdaBody(ConditionalExpressionSyntax conditionalExpression)
    {
        if (conditionalExpression.Parent is not SimpleLambdaExpressionSyntax
            and not ParenthesizedLambdaExpressionSyntax) return false;

        return conditionalExpression
                   .Ancestors()
                   .OfType<InvocationExpressionSyntax>()
                   .FirstOrDefault() is { } invocation &&
               IsNamedInvocation(invocation, "Select");
    }

    private static bool TryGetReportedWithExpression(
        ConditionalExpressionSyntax conditionalExpression,
        out WithExpressionSyntax withExpression)
    {
        withExpression = null!;

        if (conditionalExpression.WhenTrue is WithExpressionSyntax trueWithExpression &&
            IsMultiLineWithExpression(trueWithExpression))
        {
            withExpression = trueWithExpression;
            return true;
        }

        if (conditionalExpression.WhenFalse is WithExpressionSyntax falseWithExpression &&
            IsMultiLineWithExpression(falseWithExpression))
        {
            withExpression = falseWithExpression;
            return true;
        }

        return false;
    }

    private static bool IsMultiLineWithExpression(WithExpressionSyntax withExpression)
    {
        var lineSpan = withExpression.SyntaxTree.GetLineSpan(withExpression.Span);
        return lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1 >= MinimumLineSpan;
    }

    private static bool IsReportableWithExpression(WithExpressionSyntax withExpression)
    {
        return CountAssignments(withExpression) >= MinimumAssignmentCount ||
               HasNestedWithExpression(withExpression);
    }

    private static int CountAssignments(WithExpressionSyntax withExpression)
    {
        return withExpression.Initializer?.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .Count(assignment => assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)) ?? 0;
    }

    private static bool HasNestedWithExpression(WithExpressionSyntax withExpression)
    {
        return withExpression
            .DescendantNodes()
            .OfType<WithExpressionSyntax>()
            .Any(IsMultiLineWithExpression);
    }

    private static int CountWithBranches(ConditionalExpressionSyntax conditionalExpression)
    {
        return CountWithBranches(conditionalExpression.WhenTrue) +
               CountWithBranches(conditionalExpression.WhenFalse);
    }

    private static int CountWithBranches(ExpressionSyntax expression)
    {
        return expression switch
        {
            WithExpressionSyntax withExpression when IsMultiLineWithExpression(withExpression) => 1,
            ConditionalExpressionSyntax conditionalExpression => CountWithBranches(conditionalExpression),
            _ => 0
        };
    }

    private static bool IsNamedInvocation(InvocationExpressionSyntax invocation, string name)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               string.Equals(memberAccess.Name.Identifier.ValueText, name, StringComparison.Ordinal);
    }
}