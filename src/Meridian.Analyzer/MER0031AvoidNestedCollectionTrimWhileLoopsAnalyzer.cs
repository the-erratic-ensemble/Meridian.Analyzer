using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0031AvoidNestedCollectionTrimWhileLoopsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0031";

    private static readonly LocalizableString Title = "Avoid nested collection-trimming while loops";
    private static readonly LocalizableString MessageFormat =
        "Extract this nested collection-trimming while loop into a helper or bounded collection abstraction";
    private static readonly LocalizableString Description =
        "A nested while loop that exists only to trim a collection's count or length blends housekeeping logic into the enclosing loop's main work. " +
        "Extract the trimming loop into a named helper or use a bounded collection abstraction.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Readability,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    private static readonly HashSet<string> TrimMethodNames =
    [
        "Dequeue",
        "Remove",
        "RemoveAt",
        "RemoveFirst",
        "RemoveLast",
        "TryDequeue",
        "TryPop",
        "TryTake"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeWhileStatement, SyntaxKind.WhileStatement);
    }

    private static void AnalyzeWhileStatement(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not WhileStatementSyntax whileStatement)
        {
            return;
        }

        if (!IsNestedInsideAnotherWhileStatement(whileStatement))
        {
            return;
        }

        if (!TryGetTrackedCollectionExpression(whileStatement.Condition, out var collectionExpression))
        {
            return;
        }

        if (!BodyOnlyTrimsTrackedCollection(whileStatement.Statement, collectionExpression))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, whileStatement.WhileKeyword.GetLocation()));
    }

    private static bool IsNestedInsideAnotherWhileStatement(WhileStatementSyntax whileStatement)
    {
        foreach (var ancestor in whileStatement.Ancestors())
        {
            if (ancestor is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)
            {
                return false;
            }

            if (ancestor is WhileStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetTrackedCollectionExpression(
        ExpressionSyntax condition,
        out ExpressionSyntax collectionExpression)
    {
        condition = Unwrap(condition);

        if (condition is not BinaryExpressionSyntax binaryExpression)
        {
            collectionExpression = null!;
            return false;
        }

        switch (binaryExpression.Kind())
        {
            case SyntaxKind.GreaterThanExpression:
            case SyntaxKind.GreaterThanOrEqualExpression:
                return TryGetCountLikeCollectionExpression(binaryExpression.Left, out collectionExpression);
            case SyntaxKind.LessThanExpression:
            case SyntaxKind.LessThanOrEqualExpression:
                return TryGetCountLikeCollectionExpression(binaryExpression.Right, out collectionExpression);
            default:
                collectionExpression = null!;
                return false;
        }
    }

    private static bool TryGetCountLikeCollectionExpression(
        ExpressionSyntax expression,
        out ExpressionSyntax collectionExpression)
    {
        expression = Unwrap(expression);

        if (expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText is "Count" or "Length")
        {
            collectionExpression = Unwrap(memberAccess.Expression);
            return true;
        }

        if (expression is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax invocationMemberAccess,
                ArgumentList.Arguments.Count: 0
            } &&
            invocationMemberAccess.Name.Identifier.ValueText == "Count")
        {
            collectionExpression = Unwrap(invocationMemberAccess.Expression);
            return true;
        }

        collectionExpression = null!;
        return false;
    }

    private static bool BodyOnlyTrimsTrackedCollection(
        StatementSyntax statement,
        ExpressionSyntax collectionExpression)
    {
        var statements = statement is BlockSyntax block
            ? block.Statements
            : [statement];

        return statements.Count > 0 &&
               statements.All(candidate => IsTrackedCollectionTrimStatement(candidate, collectionExpression));
    }

    private static bool IsTrackedCollectionTrimStatement(
        StatementSyntax statement,
        ExpressionSyntax collectionExpression)
    {
        return statement is ExpressionStatementSyntax
            {
                Expression: InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax memberAccess
                } invocation
            } &&
            SyntaxFactory.AreEquivalent(Unwrap(memberAccess.Expression), collectionExpression) &&
            TrimMethodNames.Contains(memberAccess.Name.Identifier.ValueText) &&
            InvocationLooksLikeTrim(invocation);
    }

    private static bool InvocationLooksLikeTrim(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        return memberAccess.Name.Identifier.ValueText switch
        {
            "RemoveAt" => invocation.ArgumentList.Arguments.Count == 1,
            "Remove" => invocation.ArgumentList.Arguments.Count == 1,
            _ => true
        };
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesizedExpression)
        {
            expression = parenthesizedExpression.Expression;
        }

        return expression;
    }
}
