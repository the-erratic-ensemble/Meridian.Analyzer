using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0033AvoidHeavyInlineQueryLambdasAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0033";

    private const int MinimumLineSpan = 7;
    private const int MinimumNestedQueryCallCount = 2;

    private static readonly LocalizableString Title = "Avoid heavy inline query lambdas";
    private static readonly LocalizableString MessageFormat =
        "Move this {0}-line query lambda into a named query step or helper";
    private static readonly LocalizableString Description =
        "Multi-line LINQ lambdas that contain nested query pipelines or statement-body value construction hide branching, filtering, and shaping inside a fluent call. " +
        "Move the nested query work into a named step or helper.";

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
        context.RegisterSyntaxNodeAction(AnalyzeLambda, SyntaxKind.SimpleLambdaExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLambda, SyntaxKind.ParenthesizedLambdaExpression);
    }

    private static void AnalyzeLambda(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not LambdaExpressionSyntax lambda)
        {
            return;
        }

        if (!TryGetContainingQueryInvocation(context, lambda, out _))
        {
            return;
        }

        var lineCount = GetLineCount(lambda);
        if (lineCount < MinimumLineSpan)
        {
            return;
        }

        if (HasReportedQueryLambdaAncestor(context, lambda))
        {
            return;
        }

        if (!IsHeavyQueryLambda(context, lambda))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, lambda.GetLocation(), lineCount));
    }

    private static bool HasReportedQueryLambdaAncestor(SyntaxNodeAnalysisContext context, LambdaExpressionSyntax lambda)
    {
        foreach (var ancestorLambda in lambda.Ancestors().OfType<LambdaExpressionSyntax>())
        {
            if (!TryGetContainingQueryInvocation(context, ancestorLambda, out _))
            {
                continue;
            }

            if (GetLineCount(ancestorLambda) >= MinimumLineSpan &&
                IsHeavyQueryLambda(context, ancestorLambda))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHeavyQueryLambda(SyntaxNodeAnalysisContext context, LambdaExpressionSyntax lambda)
    {
        return lambda.Body switch
        {
            BlockSyntax block => IsHeavyStatementBody(block),
            ExpressionSyntax expression => ContainsNestedQueryPipeline(context, expression),
            _ => false
        };
    }

    private static bool IsHeavyStatementBody(BlockSyntax block)
    {
        var hasLocalDeclaration = block.Statements.Any(statement => statement is LocalDeclarationStatementSyntax);
        var hasValueReturn = block.Statements.Any(statement =>
            statement is ReturnStatementSyntax {
                Expression: ObjectCreationExpressionSyntax
                    or ImplicitObjectCreationExpressionSyntax
                    or AnonymousObjectCreationExpressionSyntax
                    or InvocationExpressionSyntax
                    or TupleExpressionSyntax
                    or WithExpressionSyntax
            });

        return hasLocalDeclaration && hasValueReturn;
    }

    private static bool ContainsNestedQueryPipeline(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        foreach (var invocation in expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (!IsQueryOperatorInvocation(context, invocation))
            {
                continue;
            }

            if (TryGetParentChainedInvocation(invocation, out var parentInvocation) &&
                IsQueryOperatorInvocation(context, parentInvocation))
            {
                continue;
            }

            if (CountQueryOperatorChain(context, invocation) >= MinimumNestedQueryCallCount)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountQueryOperatorChain(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var count = 0;
        InvocationExpressionSyntax? current = invocation;

        while (current is not null && IsQueryOperatorInvocation(context, current))
        {
            count++;
            current = GetReceiverInvocation(current);
        }

        return count;
    }

    private static bool TryGetContainingQueryInvocation(
        SyntaxNodeAnalysisContext context,
        LambdaExpressionSyntax lambda,
        out InvocationExpressionSyntax invocation)
    {
        invocation = null!;

        if (lambda.Parent is not ArgumentSyntax argument ||
            argument.Parent is not ArgumentListSyntax argumentList ||
            argumentList.Parent is not InvocationExpressionSyntax candidateInvocation ||
            !IsQueryOperatorInvocation(context, candidateInvocation))
        {
            return false;
        }

        invocation = candidateInvocation;
        return true;
    }

    private static bool IsQueryOperatorInvocation(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            !IsQueryOperatorName(memberAccess.Name.Identifier.ValueText))
        {
            return false;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
        var invocationTypeInfo = context.SemanticModel.GetTypeInfo(invocation, context.CancellationToken);
        var invocationType = invocationTypeInfo.Type ?? invocationTypeInfo.ConvertedType;
        return IsQueryLikeType(receiverType) || IsQueryLikeType(invocationType);
    }

    private static bool IsQueryOperatorName(string name)
    {
        return name is
            "Where" or
            "Select" or
            "SelectMany" or
            "OrderBy" or
            "OrderByDescending" or
            "ThenBy" or
            "ThenByDescending" or
            "GroupBy" or
            "Join" or
            "GroupJoin";
    }

    private static bool TryGetParentChainedInvocation(
        InvocationExpressionSyntax invocation,
        out InvocationExpressionSyntax parentInvocation)
    {
        parentInvocation = null!;

        if (invocation.Parent is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Parent is not InvocationExpressionSyntax parent)
        {
            return false;
        }

        parentInvocation = parent;
        return true;
    }

    private static InvocationExpressionSyntax? GetReceiverInvocation(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax receiverInvocation }
            ? receiverInvocation
            : null;
    }

    private static int GetLineCount(SyntaxNode node)
    {
        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        return lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
    }

    private static bool IsQueryLikeType(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol is null || typeSymbol.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (MatchesQueryLikeType(typeSymbol))
        {
            return true;
        }

        return typeSymbol.AllInterfaces.Any(MatchesQueryLikeType);
    }

    private static bool MatchesQueryLikeType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var namespaceName = namedType.ContainingNamespace?.ToDisplayString();
        return (namespaceName, namedType.MetadataName) switch
        {
            ("System.Linq", "IQueryable`1") => true,
            ("System.Linq", "IOrderedQueryable`1") => true,
            ("System.Collections.Generic", "IEnumerable`1") => true,
            ("System.Collections.Generic", "IAsyncEnumerable`1") => true,
            _ => false
        };
    }
}
