using System.Collections.Immutable;
using System.Threading;
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

    private readonly struct AnalysisScope
    {
        internal AnalysisScope(Compilation compilation, SemanticModel semanticModel, CancellationToken cancellationToken)
            : this(
                compilation,
                semanticModel,
                cancellationToken,
                ImmutableHashSet.Create<ISymbol>(SymbolEqualityComparer.Default))
        {
        }

        private AnalysisScope(
            Compilation compilation,
            SemanticModel semanticModel,
            CancellationToken cancellationToken,
            ImmutableHashSet<ISymbol> activeSymbols)
        {
            Compilation = compilation;
            SemanticModel = semanticModel;
            CancellationToken = cancellationToken;
            ActiveSymbols = activeSymbols;
        }

        internal Compilation Compilation { get; }

        internal SemanticModel SemanticModel { get; }

        internal CancellationToken CancellationToken { get; }

        private ImmutableHashSet<ISymbol> ActiveSymbols { get; }

        internal AnalysisScope ForSyntaxTree(SyntaxTree syntaxTree)
        {
            return SemanticModel.SyntaxTree == syntaxTree
                ? this
                : new AnalysisScope(Compilation, Compilation.GetSemanticModel(syntaxTree), CancellationToken, ActiveSymbols);
        }

        internal bool TryEnterSymbol(ISymbol symbol, out AnalysisScope nestedScope)
        {
            if (ActiveSymbols.Contains(symbol))
            {
                nestedScope = default;
                return false;
            }

            nestedScope = new AnalysisScope(Compilation, SemanticModel, CancellationToken, ActiveSymbols.Add(symbol));
            return true;
        }
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

        var scope = new AnalysisScope(context.Compilation, context.SemanticModel, context.CancellationToken);

        if (ExpressionHasVisibleBound(scope, memberAccess.Expression, invocation) ||
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

    private static bool ExpressionHasVisibleBound(AnalysisScope scope, ExpressionSyntax receiver, SyntaxNode? currentNode = null)
    {
        return ChainContainsInvocation(receiver, BoundingMethodNames) ||
               ChainContainsInvocation(receiver, AggregateBoundingMethodNames) ||
               QuerySyntaxContainsWhere(receiver) ||
               RawSqlContainsBoundingTerm(receiver) ||
               ReceiverChainComesFromVisibleBound(scope, receiver) ||
               ChainContainsBoundedLocalIdentifier(scope, receiver) ||
               ReceiverComesFromBoundedLocal(scope, receiver) ||
               ReceiverWasAssignedBoundedExpressionBeforeUse(scope, receiver, currentNode) ||
               ReceiverComesFromBoundedMethod(scope, receiver) ||
               ChainContainsBoundedMethodInvocation(scope, receiver);
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

    private static bool ReceiverComesFromBoundedLocal(AnalysisScope scope, ExpressionSyntax receiver)
    {
        if (receiver is not IdentifierNameSyntax identifierName ||
            scope.SemanticModel.GetSymbolInfo(identifierName, scope.CancellationToken).Symbol is not ILocalSymbol localSymbol)
        {
            return false;
        }

        if (!scope.TryEnterSymbol(localSymbol, out var localScope))
        {
            return false;
        }

        var declaration = localSymbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(localScope.CancellationToken))
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        return declaration?.Initializer?.Value is { } initializer &&
               ExpressionHasVisibleBound(localScope.ForSyntaxTree(initializer.SyntaxTree), initializer);
    }

    private static bool ReceiverWasAssignedBoundedExpressionBeforeUse(
        AnalysisScope scope,
        ExpressionSyntax receiver,
        SyntaxNode? currentNode)
    {
        if (receiver is not IdentifierNameSyntax identifierName ||
            currentNode is null ||
            scope.SemanticModel.GetSymbolInfo(identifierName, scope.CancellationToken).Symbol is not ILocalSymbol localSymbol)
        {
            return false;
        }

        if (!scope.TryEnterSymbol(localSymbol, out var localScope))
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
                AssignmentTargetsLocal(localScope, assignment.Left, localSymbol) &&
                ExpressionHasVisibleBound(localScope, assignment.Right, assignment));
    }

    private static bool AssignmentTargetsLocal(AnalysisScope scope, ExpressionSyntax left, ILocalSymbol localSymbol)
    {
        return left is IdentifierNameSyntax identifierName &&
               SymbolEqualityComparer.Default.Equals(
                   scope.SemanticModel.GetSymbolInfo(identifierName, scope.CancellationToken).Symbol,
                   localSymbol);
    }

    private static bool ReceiverComesFromBoundedMethod(AnalysisScope scope, ExpressionSyntax receiver)
    {
        if (receiver is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        if (scope.SemanticModel.GetSymbolInfo(invocation, scope.CancellationToken).Symbol is IMethodSymbol methodSymbol)
        {
            return methodSymbol.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(scope.CancellationToken))
                .OfType<MethodDeclarationSyntax>()
                .Any(method => MethodReturnsBoundedQuery(scope.ForSyntaxTree(method.SyntaxTree), method));
        }

        return ReceiverComesFromSyntacticallyBoundedMethod(scope, invocation);
    }

    private static bool ChainContainsBoundedMethodInvocation(AnalysisScope scope, SyntaxNode node)
    {
        return node.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => ReceiverComesFromBoundedMethod(scope, invocation));
    }

    private static bool ChainContainsBoundedLocalIdentifier(AnalysisScope scope, SyntaxNode node)
    {
        return node.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => ReceiverComesFromBoundedLocal(scope, identifier));
    }

    private static bool ReceiverComesFromSyntacticallyBoundedMethod(AnalysisScope scope, InvocationExpressionSyntax invocation)
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
            .Any(method => MethodReturnsBoundedQuery(scope.ForSyntaxTree(method.SyntaxTree), method));
    }

    private static bool ReceiverChainComesFromVisibleBound(AnalysisScope scope, ExpressionSyntax receiver)
    {
        return receiver switch
        {
            IdentifierNameSyntax => ReceiverComesFromBoundedLocal(scope, receiver),
            InvocationExpressionSyntax invocation => InvocationReceiverComesFromVisibleBound(scope, invocation),
            MemberAccessExpressionSyntax memberAccess => ReceiverChainComesFromVisibleBound(scope, memberAccess.Expression),
            _ => false
        };
    }

    private static bool InvocationReceiverComesFromVisibleBound(AnalysisScope scope, InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               ReceiverChainComesFromVisibleBound(scope, memberAccess.Expression);
    }

    private static bool MethodReturnsBoundedQuery(AnalysisScope scope, MethodDeclarationSyntax method)
    {
        var methodScope = scope.ForSyntaxTree(method.SyntaxTree);
        if (methodScope.SemanticModel.GetDeclaredSymbol(method, methodScope.CancellationToken) is IMethodSymbol methodSymbol &&
            !methodScope.TryEnterSymbol(methodSymbol, out methodScope))
        {
            return false;
        }

        if (method.ExpressionBody?.Expression is { } expressionBody)
        {
            return ExpressionHasVisibleBound(methodScope, expressionBody);
        }

        return method.Body?.Statements
            .OfType<ReturnStatementSyntax>()
            .Any(statement => statement.Expression is { } expression && ExpressionHasVisibleBound(methodScope, expression)) == true;
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
