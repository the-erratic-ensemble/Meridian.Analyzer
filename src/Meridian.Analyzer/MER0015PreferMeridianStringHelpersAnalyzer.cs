using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0015PreferMeridianStringHelpersAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0015";

    private static readonly LocalizableString Title = "Prefer shared string helpers";
    private static readonly LocalizableString MessageFormat = "Prefer Shared string helpers for in-memory string normalization";
    private static readonly LocalizableString Description =
        "StringExtensions centralizes common string handling. In-memory runtime code should prefer helpers such as HasText, TrimToNull, OrEmpty, TrimUpper, TrimLower, NormalizePostcode, and RedactPostcode.";

    private static readonly string[] QueryMethodNames =
    {
        "All",
        "Any",
        "Count",
        "First",
        "FirstOrDefault",
        "GroupBy",
        "GroupJoin",
        "Join",
        "Last",
        "LastOrDefault",
        "LongCount",
        "OrderBy",
        "OrderByDescending",
        "Select",
        "SelectMany",
        "Single",
        "SingleOrDefault",
        "ThenBy",
        "ThenByDescending",
        "Where"
    };

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
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeCoalesceExpression, SyntaxKind.CoalesceExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            IsExcluded(invocation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (IsNullOrWhitespaceCall(invocation) || IsTrimInvariantCaseCall(invocation))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        }
    }

    private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not BinaryExpressionSyntax binaryExpression ||
            IsExcluded(binaryExpression, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if ((ContainsTrimCall(binaryExpression.Left) && IsEmptyStringExpression(binaryExpression.Right)) ||
            (ContainsTrimCall(binaryExpression.Right) && IsEmptyStringExpression(binaryExpression.Left)))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, binaryExpression.GetLocation()));
        }
    }

    private static void AnalyzeCoalesceExpression(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not BinaryExpressionSyntax coalesceExpression ||
            IsExcluded(coalesceExpression, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (IsEmptyStringExpression(coalesceExpression.Right))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, coalesceExpression.GetLocation()));
        }
    }

    private static bool IsNullOrWhitespaceCall(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Expression.ToString() is "string" or "String" or "System.String" &&
               memberAccess.Name.Identifier.ValueText is "IsNullOrWhiteSpace" or "IsNullOrEmpty";
    }

    private static bool IsTrimInvariantCaseCall(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax
               {
                   Name.Identifier.ValueText: "ToUpperInvariant" or "ToLowerInvariant",
                   Expression: var memberExpression
               }
               && ContainsTrimCall(memberExpression);
    }

    private static bool ContainsTrimCall(ExpressionSyntax expression)
    {
        return expression.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => MeridianAnalyzerRuleHelpers.GetSimpleInvocationName(invocation) == "Trim");
    }

    private static bool IsEmptyStringExpression(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax { Token.Value: string { Length: 0 } } ||
               expression is MemberAccessExpressionSyntax
               {
                   Name.Identifier.ValueText: "Empty",
                   Expression: var memberExpression
               } &&
               memberExpression.ToString() is "string" or "String" or "System.String";
    }

    private static bool IsExcluded(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var filePath = node.SyntaxTree.FilePath;
        return MeridianAnalyzerRuleHelpers.IsTestPath(filePath) ||
               MeridianAnalyzerSyntaxHelpers.PathContains(filePath, "/StringExtensions.cs") ||
               IsInsideQueryableOrExpressionQuery(node, semanticModel, cancellationToken);
    }

    private static bool IsInsideQueryableOrExpressionQuery(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return IsInsideQueryableLambdaSyntax(node) ||
               IsInsideQueryableQuerySyntax(node, semanticModel, cancellationToken) ||
               IsInsideQueryableLambda(node, semanticModel, cancellationToken) ||
               IsInsideExpressionTreeLambda(node, semanticModel, cancellationToken);
    }

    private static bool IsInsideQueryableLambdaSyntax(SyntaxNode node)
    {
        var lambdaExpression = node.AncestorsAndSelf().OfType<LambdaExpressionSyntax>().FirstOrDefault();
        if (lambdaExpression?.Parent is not ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } })
        {
            return false;
        }

        return IsKnownQueryMethodName(invocation) &&
               ReceiverIdentifierHasQueryableParameterType(GetInvocationReceiver(invocation));
    }

    private static bool IsInsideQueryableQuerySyntax(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var queryExpression = node.AncestorsAndSelf().OfType<QueryExpressionSyntax>().FirstOrDefault();
        if (queryExpression is null)
        {
            return false;
        }

        var sourceType = semanticModel.GetTypeInfo(queryExpression.FromClause.Expression, cancellationToken).Type;
        if (sourceType is not null)
        {
            return IsIQueryableType(sourceType);
        }

        var queryType = semanticModel.GetTypeInfo(queryExpression, cancellationToken).Type;
        if (queryType is not null)
        {
            return IsIQueryableType(queryType);
        }

        return true;
    }

    private static bool IsInsideQueryableLambda(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var lambdaExpression = node.AncestorsAndSelf().OfType<LambdaExpressionSyntax>().FirstOrDefault();
        if (lambdaExpression is null)
        {
            return false;
        }

        var argument = lambdaExpression.Parent as ArgumentSyntax;
        var invocation = argument?.Parent?.Parent as InvocationExpressionSyntax;
        if (invocation is null || !IsKnownQueryMethodName(invocation))
        {
            return false;
        }

        if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol symbol)
        {
            return IsSystemLinqQueryableMethod(symbol);
        }

        var receiver = GetInvocationReceiver(invocation);
        var receiverType = receiver is null
            ? null
            : semanticModel.GetTypeInfo(receiver, cancellationToken).Type;

        return receiverType is null ||
               IsIQueryableType(receiverType) ||
               ReceiverIsSyntacticallyQueryable(receiver, semanticModel, cancellationToken) ||
               ReceiverIdentifierHasQueryableParameterType(receiver);
    }

    private static bool ReceiverIsSyntacticallyQueryable(
        ExpressionSyntax? receiver,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (receiver is null)
        {
            return false;
        }

        var symbol = semanticModel.GetSymbolInfo(receiver, cancellationToken).Symbol;
        var typeSyntax = symbol switch
        {
            IParameterSymbol parameter => parameter.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(cancellationToken))
                .OfType<ParameterSyntax>()
                .Select(parameterSyntax => parameterSyntax.Type?.ToString())
                .FirstOrDefault(),
            ILocalSymbol local => local.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(cancellationToken))
                .OfType<VariableDeclaratorSyntax>()
                .Select(declarator => declarator.Parent?.Parent is VariableDeclarationSyntax declaration ? declaration.Type.ToString() : null)
                .FirstOrDefault(),
            _ => null
        };

        return typeSyntax?.StartsWith("IQueryable", StringComparison.Ordinal) == true ||
               typeSyntax?.StartsWith("System.Linq.IQueryable", StringComparison.Ordinal) == true;
    }

    private static bool ReceiverIdentifierHasQueryableParameterType(ExpressionSyntax? receiver)
    {
        if (receiver is not IdentifierNameSyntax identifierName)
        {
            return false;
        }

        var containingMethod = MeridianAnalyzerRuleHelpers.GetContainingMethod(receiver);
        if (containingMethod is null)
        {
            return false;
        }

        return containingMethod.ParameterList.Parameters.Any(parameter =>
            string.Equals(parameter.Identifier.ValueText, identifierName.Identifier.ValueText, StringComparison.Ordinal) &&
            IsQueryableTypeName(parameter.Type?.ToString()));
    }

    private static bool IsQueryableTypeName(string? typeName)
    {
        return typeName?.StartsWith("IQueryable", StringComparison.Ordinal) == true ||
               typeName?.StartsWith("System.Linq.IQueryable", StringComparison.Ordinal) == true;
    }

    private static bool IsInsideExpressionTreeLambda(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var lambdaExpression = node.AncestorsAndSelf().OfType<LambdaExpressionSyntax>().FirstOrDefault();
        if (lambdaExpression is null)
        {
            return false;
        }

        var convertedType = semanticModel.GetTypeInfo(lambdaExpression, cancellationToken).ConvertedType;
        return convertedType is not null && IsExpressionTreeType(convertedType);
    }

    private static bool IsKnownQueryMethodName(InvocationExpressionSyntax invocation)
    {
        var invocationName = MeridianAnalyzerRuleHelpers.GetSimpleInvocationName(invocation);
        return QueryMethodNames.Any(name => string.Equals(invocationName, name, StringComparison.Ordinal));
    }

    private static bool IsSystemLinqQueryableMethod(IMethodSymbol methodSymbol)
    {
        return string.Equals(methodSymbol.ContainingType?.Name, "Queryable", StringComparison.Ordinal) &&
               string.Equals(methodSymbol.ContainingNamespace?.ToDisplayString(), "System.Linq", StringComparison.Ordinal);
    }

    private static ExpressionSyntax? GetInvocationReceiver(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
            ? memberAccess.Expression
            : null;
    }

    private static bool IsIQueryableType(ITypeSymbol type)
    {
        return IsIQueryableNamedType(type) ||
               type.AllInterfaces.Any(IsIQueryableNamedType);
    }

    private static bool IsIQueryableNamedType(ITypeSymbol type)
    {
        return string.Equals(type.Name, "IQueryable", StringComparison.Ordinal) &&
               string.Equals(type.ContainingNamespace?.ToDisplayString(), "System.Linq", StringComparison.Ordinal);
    }

    private static bool IsExpressionTreeType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               string.Equals(namedType.Name, "Expression", StringComparison.Ordinal) &&
               string.Equals(namedType.ContainingNamespace?.ToDisplayString(), "System.Linq.Expressions", StringComparison.Ordinal);
    }
}
