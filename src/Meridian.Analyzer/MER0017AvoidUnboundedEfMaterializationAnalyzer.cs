using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0017AvoidUnboundedEfMaterializationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0017";

    private static readonly LocalizableString Title = "Avoid unbounded EF materialization";
    private static readonly LocalizableString MessageFormat = "Review this async materialization for an explicit Where/Take/Skip bound";
    private static readonly LocalizableString Description =
        "Unbounded EF materialization can load large tables or relationship graphs into memory. Runtime queries should make their bounds obvious at the query site.";

    private static readonly string[] MaterializerNames =
    {
        "ToArrayAsync",
        "ToDictionaryAsync",
        "ToHashSetAsync",
        "ToListAsync"
    };

    private static readonly string[] BoundingMethodNames =
    {
        "Skip",
        "Take",
        "Where"
    };

    private static readonly string[] AggregateBoundingMethodNames =
    {
        "GroupBy"
    };

    private static readonly string[] RawSqlBoundingTerms =
    {
        " limit ",
        " offset ",
        " top ",
        " where ",
        "p_limit"
    };

    private static readonly string[] IntentionalFullMaterializationBoundaryMethodNames =
    {
        "AddFacilitiesAsync",
        "GenerateMarketAnalysisReportAsync",
        "GetAllAsync",
        "GetAllBusinessTypesWithCountsAsync",
        "GetAllFacilitiesAsync",
        "GetAllWithDetailsAsync",
        "GetLocalitiesWithStopCountsAsync",
        "GetStatisticCatalogueAsync",
        "ValidateAndRepairHierarchyAsync",
        "ValidateHierarchyIntegrityAsync"
    };

    private static readonly string[] ApprovedPathSegments =
    {
        "/Migrations/",
        "/Cli/",
        "/tools/"
    };

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Performance,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation || IsApprovedLocation(invocation.SyntaxTree.FilePath))
        {
            return;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            !MaterializerNames.Any(name => string.Equals(memberAccess.Name.Identifier.ValueText, name, StringComparison.Ordinal)))
        {
            return;
        }

        if (ExpressionHasVisibleBound(context, memberAccess.Expression, invocation) ||
            IsIntentionalFullMaterializationBoundary(invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool ChainContainsInvocation(SyntaxNode node, string[] methodNames)
    {
        return node.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => methodNames.Any(name => string.Equals(MeridianAnalyzerRuleHelpers.GetSimpleInvocationName(invocation), name, StringComparison.Ordinal)));
    }

    private static bool ExpressionHasVisibleBound(SyntaxNodeAnalysisContext context, ExpressionSyntax receiver, SyntaxNode? currentNode = null)
    {
        return ChainContainsInvocation(receiver, BoundingMethodNames) ||
               ChainContainsInvocation(receiver, AggregateBoundingMethodNames) ||
               QuerySyntaxContainsWhere(receiver) ||
               RawSqlContainsBoundingTerm(receiver) ||
               ReceiverChainComesFromVisibleBound(context, receiver) ||
               ChainContainsBoundedLocalIdentifier(context, receiver) ||
               ReceiverComesFromBoundedLocal(context, receiver) ||
               ReceiverWasAssignedBoundedExpressionBeforeUse(context, receiver, currentNode) ||
               ReceiverComesFromBoundedMethod(context, receiver) ||
               ChainContainsBoundedMethodInvocation(context, receiver);
    }

    private static bool QuerySyntaxContainsWhere(SyntaxNode node)
    {
        return node.DescendantNodesAndSelf().OfType<WhereClauseSyntax>().Any();
    }

    private static bool RawSqlContainsBoundingTerm(SyntaxNode node)
    {
        return node.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => IsRawSqlInvocation(MeridianAnalyzerRuleHelpers.GetSimpleInvocationName(invocation)))
            .Any(invocation => invocation.ArgumentList.Arguments.Any(argument => ContainsRawSqlBoundingTerm(argument.Expression.ToString())));
    }

    private static bool ContainsRawSqlBoundingTerm(string value)
    {
        var normalized = " " + value.Replace("\\r", " ")
            .Replace("\\n", " ")
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .ToLowerInvariant() + " ";

        return RawSqlBoundingTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
    }

    private static bool IsRawSqlInvocation(string invocationName)
    {
        return string.Equals(invocationName, "FromSqlInterpolated", StringComparison.Ordinal) ||
               string.Equals(invocationName, "FromSqlRaw", StringComparison.Ordinal) ||
               string.Equals(invocationName, "SqlQueryRaw", StringComparison.Ordinal);
    }

    private static bool ReceiverComesFromBoundedLocal(SyntaxNodeAnalysisContext context, ExpressionSyntax receiver)
    {
        if (receiver is not IdentifierNameSyntax identifierName ||
            context.SemanticModel.GetSymbolInfo(identifierName, context.CancellationToken).Symbol is not ILocalSymbol localSymbol)
        {
            return false;
        }

        var declaration = localSymbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(context.CancellationToken))
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        return declaration?.Initializer?.Value is { } initializer &&
               ExpressionHasVisibleBound(context, initializer);
    }

    private static bool ReceiverWasAssignedBoundedExpressionBeforeUse(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax receiver,
        SyntaxNode? currentNode)
    {
        if (receiver is not IdentifierNameSyntax identifierName ||
            currentNode is null ||
            context.SemanticModel.GetSymbolInfo(identifierName, context.CancellationToken).Symbol is not ILocalSymbol localSymbol)
        {
            return false;
        }

        var containingMethod = MeridianAnalyzerRuleHelpers.GetContainingMethod(currentNode);
        if (containingMethod is null)
        {
            return false;
        }

        return containingMethod.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.SpanStart < currentNode.SpanStart)
            .Any(assignment =>
                AssignmentTargetsLocal(context, assignment.Left, localSymbol) &&
                ExpressionHasVisibleBound(context, assignment.Right, assignment));
    }

    private static bool AssignmentTargetsLocal(SyntaxNodeAnalysisContext context, ExpressionSyntax left, ILocalSymbol localSymbol)
    {
        return left is IdentifierNameSyntax identifierName &&
               SymbolEqualityComparer.Default.Equals(
                   context.SemanticModel.GetSymbolInfo(identifierName, context.CancellationToken).Symbol,
                   localSymbol);
    }

    private static bool ReceiverComesFromBoundedMethod(SyntaxNodeAnalysisContext context, ExpressionSyntax receiver)
    {
        if (receiver is not InvocationExpressionSyntax invocation ||
            context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol)
        {
            return false;
        }

        return methodSymbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(context.CancellationToken))
            .OfType<MethodDeclarationSyntax>()
            .Any(method => MethodReturnsBoundedQuery(context, method)) ||
               ReceiverComesFromSyntacticallyBoundedMethod(context, invocation);
    }

    private static bool ChainContainsBoundedMethodInvocation(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        return node.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => ReceiverComesFromBoundedMethod(context, invocation));
    }

    private static bool ChainContainsBoundedLocalIdentifier(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        return node.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => ReceiverComesFromBoundedLocal(context, identifier));
    }

    private static bool ReceiverComesFromSyntacticallyBoundedMethod(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var invocationName = MeridianAnalyzerRuleHelpers.GetSimpleInvocationName(invocation);
        var containingClass = MeridianAnalyzerRuleHelpers.GetContainingClass(invocation);
        if (containingClass is null || string.IsNullOrEmpty(invocationName))
        {
            return false;
        }

        return containingClass.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(method => string.Equals(method.Identifier.ValueText, invocationName, StringComparison.Ordinal))
            .Any(method => MethodReturnsBoundedQuery(context, method));
    }

    private static bool ReceiverChainComesFromVisibleBound(SyntaxNodeAnalysisContext context, ExpressionSyntax receiver)
    {
        return receiver switch
        {
            IdentifierNameSyntax => ReceiverComesFromBoundedLocal(context, receiver),
            InvocationExpressionSyntax invocation => InvocationReceiverComesFromVisibleBound(context, invocation),
            MemberAccessExpressionSyntax memberAccess => ReceiverChainComesFromVisibleBound(context, memberAccess.Expression),
            _ => false
        };
    }

    private static bool InvocationReceiverComesFromVisibleBound(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               ReceiverChainComesFromVisibleBound(context, memberAccess.Expression);
    }

    private static bool MethodReturnsBoundedQuery(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method)
    {
        if (method.ExpressionBody?.Expression is { } expressionBody)
        {
            return ExpressionHasVisibleBound(context, expressionBody);
        }

        return method.Body?.Statements
            .OfType<ReturnStatementSyntax>()
            .Any(statement => statement.Expression is { } expression && ExpressionHasVisibleBound(context, expression)) == true;
    }

    private static bool IsIntentionalFullMaterializationBoundary(SyntaxNode node)
    {
        var method = MeridianAnalyzerRuleHelpers.GetContainingMethod(node);
        if (method is null)
        {
            return false;
        }

        return IntentionalFullMaterializationBoundaryMethodNames.Any(methodName =>
            string.Equals(method.Identifier.ValueText, methodName, StringComparison.Ordinal));
    }

    private static bool IsApprovedLocation(string filePath)
    {
        return MeridianAnalyzerRuleHelpers.IsTestPath(filePath) ||
               MeridianAnalyzerSyntaxHelpers.PathContainsAny(filePath, ApprovedPathSegments);
    }
}
