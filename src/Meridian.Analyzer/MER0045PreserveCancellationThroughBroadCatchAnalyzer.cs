using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0045PreserveCancellationThroughBroadCatchAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0045";

    private static readonly LocalizableString Title = "Preserve cancellation through broad catches";

    private static readonly LocalizableString MessageFormat =
        "Preserve OperationCanceledException instead of converting it into an ordinary failure";

    private static readonly LocalizableString Description =
        "A broad catch around cancellation-aware work should keep cancellation as cancellation across the method boundary.";

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
        context.RegisterSyntaxNodeAction(AnalyzeCatchClause, SyntaxKind.CatchClause);
    }

    private static void AnalyzeCatchClause(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not CatchClauseSyntax catchClause ||
            MeridianAnalyzerRuleHelpers.IsTestPath(catchClause.SyntaxTree.FilePath) ||
            !IsBroadCatch(context, catchClause) ||
            catchClause.Ancestors().OfType<TryStatementSyntax>().FirstOrDefault() is not
            TryStatementSyntax tryStatement ||
            !ContainsCancellationAwareAwait(context, tryStatement.Block) ||
            PreservesCancellation(context, catchClause, tryStatement))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, catchClause.CatchKeyword.GetLocation()));
    }

    private static bool IsBroadCatch(SyntaxNodeAnalysisContext context, CatchClauseSyntax catchClause)
    {
        if (catchClause.Declaration is null)
            return true;

        var type = context.SemanticModel.GetTypeInfo(
            catchClause.Declaration.Type,
            context.CancellationToken).Type;
        return type is INamedTypeSymbol namedType &&
               string.Equals(namedType.Name, "Exception", StringComparison.Ordinal) &&
               string.Equals(namedType.ContainingNamespace?.ToDisplayString(), "System",
                   StringComparison.Ordinal);
    }

    private static bool ContainsCancellationAwareAwait(
        SyntaxNodeAnalysisContext context,
        BlockSyntax tryBlock)
    {
        return tryBlock.DescendantNodes()
            .OfType<AwaitExpressionSyntax>()
            .Where(awaitExpression =>
                !awaitExpression.Ancestors().Any(ancestor =>
                    ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .Where(awaitExpression => IsDirectlyInTryBlock(awaitExpression, tryBlock))
            .Select(awaitExpression => awaitExpression.Expression)
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => InvocationReceivesCancellationToken(context, invocation));
    }

    private static bool IsDirectlyInTryBlock(
        AwaitExpressionSyntax awaitExpression,
        BlockSyntax tryBlock)
    {
        return awaitExpression.Ancestors().OfType<TryStatementSyntax>().FirstOrDefault()?.Block == tryBlock;
    }

    private static bool InvocationReceivesCancellationToken(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        return invocation.ArgumentList.Arguments.Any(argument =>
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(
                argument.Expression,
                context.CancellationToken);
            return IsCancellationToken(typeInfo.Type) ||
                   IsCancellationToken(typeInfo.ConvertedType);
        });
    }

    private static bool IsCancellationToken(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol namedType &&
               string.Equals(namedType.Name, "CancellationToken", StringComparison.Ordinal) &&
               string.Equals(namedType.ContainingNamespace?.ToDisplayString(), "System.Threading",
                   StringComparison.Ordinal);
    }

    private static bool PreservesCancellation(
        SyntaxNodeAnalysisContext context,
        CatchClauseSyntax catchClause,
        TryStatementSyntax tryStatement)
    {
        return ExcludesCancellationFromFilter(context, catchClause) ||
               HasDedicatedCancellationCatchThatRethrows(context, catchClause, tryStatement) ||
               RethrowsCancellation(context, catchClause);
    }

    private static bool ExcludesCancellationFromFilter(
        SyntaxNodeAnalysisContext context,
        CatchClauseSyntax catchClause)
    {
        if (catchClause.Filter is null || catchClause.Declaration is null)
            return false;

        var catchSymbol = context.SemanticModel.GetDeclaredSymbol(
            catchClause.Declaration,
            context.CancellationToken);
        return catchSymbol is not null &&
               ExpressionExcludesCancellation(
                   context,
                   catchClause.Filter.FilterExpression,
                   catchSymbol);
    }

    private static bool ExpressionExcludesCancellation(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression,
        ISymbol catchSymbol)
    {
        expression = UnwrapParentheses(expression);

        if (expression is BinaryExpressionSyntax binary &&
            binary.IsKind(SyntaxKind.LogicalAndExpression))
            return ExpressionExcludesCancellation(context, binary.Left, catchSymbol) ||
                   ExpressionExcludesCancellation(context, binary.Right, catchSymbol);

        if (expression is PrefixUnaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.LogicalNotExpression,
                Operand: IsPatternExpressionSyntax nestedPattern
            })
        {
            return IsCancellationPattern(context, nestedPattern, catchSymbol);
        }

        return expression is IsPatternExpressionSyntax isPattern &&
               isPattern.Pattern is UnaryPatternSyntax
            {
                RawKind: (int)SyntaxKind.NotPattern,
                Pattern: TypePatternSyntax typePattern
            } &&
               IsCancellationPatternReceiver(context, isPattern, catchSymbol) &&
               IsOperationCanceledException(context, typePattern.Type);
    }

    private static bool IsCancellationPattern(
        SyntaxNodeAnalysisContext context,
        IsPatternExpressionSyntax expression,
        ISymbol catchSymbol)
    {
        return expression.Pattern is TypePatternSyntax typePattern &&
               IsCancellationPatternReceiver(context, expression, catchSymbol) &&
               IsOperationCanceledException(context, typePattern.Type);
    }

    private static bool IsCancellationPatternReceiver(
        SyntaxNodeAnalysisContext context,
        IsPatternExpressionSyntax expression,
        ISymbol catchSymbol)
    {
        return MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
                   expression.Expression,
                   context.SemanticModel,
                   context.CancellationToken) is { } symbol &&
               SymbolEqualityComparer.Default.Equals(symbol, catchSymbol);
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
            expression = parenthesized.Expression;

        return expression;
    }

    private static bool HasDedicatedCancellationCatchThatRethrows(
        SyntaxNodeAnalysisContext context,
        CatchClauseSyntax broadCatch,
        TryStatementSyntax tryStatement)
    {
        return tryStatement.Catches
            .Where(catchClause => catchClause.SpanStart < broadCatch.SpanStart)
            .Where(catchClause => catchClause.Declaration is not null)
            .Where(catchClause => IsOperationCanceledException(
                context,
                catchClause.Declaration!.Type))
            .Any(catchClause => HasUnconditionalRethrow(catchClause));
    }

    private static bool RethrowsCancellation(
        SyntaxNodeAnalysisContext context,
        CatchClauseSyntax catchClause)
    {
        var catchIdentifier = catchClause.Declaration?.Identifier;
        var catchSymbol = catchIdentifier is { IsMissing: false }
            ? context.SemanticModel.GetDeclaredSymbol(catchClause.Declaration!, context.CancellationToken)
            : null;

        foreach (var throwStatement in GetDirectThrowStatements(catchClause))
        {
            if (!IsUnconditionalThrow(catchClause, throwStatement))
                continue;

            if (throwStatement.Expression is null)
                return true;

            if (catchSymbol is not null &&
                MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
                    throwStatement.Expression,
                    context.SemanticModel,
                    context.CancellationToken) is { } thrownSymbol &&
                SymbolEqualityComparer.Default.Equals(catchSymbol, thrownSymbol))
                return true;

            var thrownType = context.SemanticModel.GetTypeInfo(
                throwStatement.Expression,
                context.CancellationToken).Type;
            if (IsOperationCanceledException(thrownType))
                return true;
        }

        if (catchSymbol is null)
            return false;

        return catchClause.Block.DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Where(statement => ContainsCancellationTypeCheck(
                context,
                statement.Condition,
                catchSymbol))
            .SelectMany(statement => statement.Statement.DescendantNodesAndSelf()
                .OfType<ThrowStatementSyntax>())
            .Any(throwStatement =>
                throwStatement.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault() is
                IfStatementSyntax guard &&
                ContainsCancellationTypeCheck(context, guard.Condition, catchSymbol) &&
                IsUnconditionalThrowWithinGuard(throwStatement, guard) &&
                (throwStatement.Expression is null ||
                 IsThrownCatchSymbol(context, throwStatement, catchSymbol)));
    }

    private static IEnumerable<ThrowStatementSyntax> GetDirectThrowStatements(CatchClauseSyntax catchClause)
    {
        return catchClause.Block.DescendantNodes()
            .OfType<ThrowStatementSyntax>()
            .Where(throwStatement =>
                throwStatement.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault() == catchClause);
    }

    private static bool HasUnconditionalRethrow(CatchClauseSyntax catchClause)
    {
        return GetDirectThrowStatements(catchClause)
            .Any(throwStatement => IsUnconditionalThrow(catchClause, throwStatement) &&
                throwStatement.Expression is null);
    }

    private static bool IsUnconditionalThrow(
        CatchClauseSyntax catchClause,
        ThrowStatementSyntax throwStatement)
    {
        return !HasControlFlowBoundaryBetween(throwStatement, catchClause.Block);
    }

    private static bool IsUnconditionalThrowWithinGuard(
        ThrowStatementSyntax throwStatement,
        IfStatementSyntax guard)
    {
        return !HasControlFlowBoundaryBetween(throwStatement, guard);
    }

    private static bool HasControlFlowBoundaryBetween(
        ThrowStatementSyntax throwStatement,
        SyntaxNode boundary)
    {
        return throwStatement.Ancestors()
            .TakeWhile(ancestor => ancestor != boundary)
            .Any(ancestor => ancestor is IfStatementSyntax or ConditionalExpressionSyntax or
                WhileStatementSyntax or DoStatementSyntax or ForStatementSyntax or
                ForEachStatementSyntax or SwitchStatementSyntax or SwitchSectionSyntax or
                SwitchExpressionSyntax or SwitchExpressionArmSyntax or TryStatementSyntax);
    }

    private static bool ContainsCancellationTypeCheck(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax condition,
        ISymbol catchSymbol)
    {
        return condition.DescendantNodesAndSelf()
            .OfType<IsPatternExpressionSyntax>()
            .Any(pattern =>
                MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
                    pattern.Expression,
                    context.SemanticModel,
                    context.CancellationToken) is { } symbol &&
                SymbolEqualityComparer.Default.Equals(symbol, catchSymbol) &&
                pattern.Pattern is TypePatternSyntax typePattern &&
                IsOperationCanceledException(context, typePattern.Type));
    }

    private static bool IsThrownCatchSymbol(
        SyntaxNodeAnalysisContext context,
        ThrowStatementSyntax throwStatement,
        ISymbol catchSymbol)
    {
        return throwStatement.Expression is { } expression &&
               MeridianAnalyzerSemanticHelpers.GetReferencedSymbol(
                   expression,
                   context.SemanticModel,
                   context.CancellationToken) is { } symbol &&
               SymbolEqualityComparer.Default.Equals(symbol, catchSymbol);
    }

    private static bool IsOperationCanceledException(
        SyntaxNodeAnalysisContext context,
        TypeSyntax typeSyntax)
    {
        return IsOperationCanceledException(
            context.SemanticModel.GetTypeInfo(typeSyntax, context.CancellationToken).Type);
    }

    private static bool IsOperationCanceledException(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol namedType &&
               string.Equals(namedType.Name, "OperationCanceledException", StringComparison.Ordinal) &&
               string.Equals(namedType.ContainingNamespace?.ToDisplayString(), "System", StringComparison.Ordinal);
    }
}
