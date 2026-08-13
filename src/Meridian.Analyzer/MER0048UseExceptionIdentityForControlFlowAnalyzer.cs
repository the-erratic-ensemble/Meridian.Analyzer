using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0048UseExceptionIdentityForControlFlowAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0048";

    private static readonly LocalizableString Title = "Use exception identity for control flow";

    private static readonly LocalizableString MessageFormat =
        "Use exception type or structured identity instead of exception text for control flow";

    private static readonly LocalizableString Description =
        "Exception messages and formatted exception text are for reporting; branch decisions should use exception identity.";

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
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MemberAccessExpressionSyntax memberAccess ||
            MeridianAnalyzerRuleHelpers.IsTestPath(memberAccess.SyntaxTree.FilePath) ||
            memberAccess.Name.Identifier.ValueText != "Message" ||
            !IsExceptionReceiver(context, memberAccess.Expression) ||
            !ContributesToControlFlow(memberAccess))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation()));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(invocation.SyntaxTree.FilePath) ||
            invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Name.Identifier.ValueText != "ToString" ||
            !IsExceptionReceiver(context, memberAccess.Expression) ||
            !ContributesToControlFlow(invocation))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsExceptionReceiver(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax receiver)
    {
        var type = context.SemanticModel.GetTypeInfo(receiver, context.CancellationToken).Type;
        return MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(type, "System", "Exception");
    }

    private static bool ContributesToControlFlow(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is IfStatementSyntax ifStatement &&
                ifStatement.Condition.Span.Contains(node.Span))
                return true;

            if (ancestor is WhileStatementSyntax whileStatement &&
                whileStatement.Condition.Span.Contains(node.Span))
                return true;

            if (ancestor is DoStatementSyntax doStatement &&
                doStatement.Condition.Span.Contains(node.Span))
                return true;

            if (ancestor is ForStatementSyntax forStatement &&
                forStatement.Condition?.Span.Contains(node.Span) == true)
                return true;

            if (ancestor is ConditionalExpressionSyntax conditionalExpression &&
                conditionalExpression.Condition.Span.Contains(node.Span))
                return true;

            if (ancestor is SwitchStatementSyntax switchStatement &&
                switchStatement.Expression.Span.Contains(node.Span))
                return true;

            if (ancestor is SwitchExpressionSyntax switchExpression &&
                switchExpression.GoverningExpression.Span.Contains(node.Span))
                return true;

            if (ancestor is CatchFilterClauseSyntax catchFilter &&
                catchFilter.FilterExpression.Span.Contains(node.Span))
                return true;
        }

        return false;
    }
}
