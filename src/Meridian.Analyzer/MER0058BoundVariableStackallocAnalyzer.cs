using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0058BoundVariableStackallocAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0058";

    private static readonly LocalizableString Title = "Bound variable stack allocation size";

    private static readonly LocalizableString MessageFormat =
        "Give this variable stackalloc size a fixed upper bound or use pooled or heap storage";

    private static readonly LocalizableString Description =
        "A runtime stack allocation without a visible ceiling can exhaust the thread stack.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Performance,
        DiagnosticSeverity.Warning,
        true,
        Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeStackAlloc, SyntaxKind.StackAllocArrayCreationExpression);
    }

    private static void AnalyzeStackAlloc(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not StackAllocArrayCreationExpressionSyntax stackAlloc ||
            MeridianAnalyzerRuleHelpers.IsTestPath(stackAlloc.SyntaxTree.FilePath) ||
            stackAlloc.Type is not ArrayTypeSyntax arrayType ||
            arrayType.RankSpecifiers.Count != 1 ||
            arrayType.RankSpecifiers[0].Sizes.Count != 1)
            return;

        var size = arrayType.RankSpecifiers[0].Sizes[0];
        if (context.SemanticModel.GetConstantValue(size, context.CancellationToken).HasValue ||
            HasVisibleBound(context, stackAlloc, size))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, size.GetLocation()));
    }

    private static bool HasVisibleBound(
        SyntaxNodeAnalysisContext context,
        StackAllocArrayCreationExpressionSyntax stackAlloc,
        ExpressionSyntax size)
    {
        var valueSymbol = context.SemanticModel.GetSymbolInfo(
            MeridianAnalyzerSemanticHelpers.Unwrap(size),
            context.CancellationToken).Symbol;
        if (valueSymbol is null)
            return false;

        foreach (var ancestor in stackAlloc.Ancestors())
        {
            if (ancestor is ConditionalExpressionSyntax conditional &&
                conditional.Condition.Span.Contains(stackAlloc.Span))
                continue;

            if (ancestor is ConditionalExpressionSyntax conditionalExpression &&
                (conditionalExpression.WhenTrue.Span.Contains(stackAlloc.Span) ||
                 conditionalExpression.WhenFalse.Span.Contains(stackAlloc.Span)) &&
                TryGetCap(
                    context,
                    conditionalExpression.Condition,
                    valueSymbol,
                    conditionalExpression.WhenTrue.Span.Contains(stackAlloc.Span),
                    out _))
                return true;

            if (ancestor is IfStatementSyntax ifStatement &&
                (ifStatement.Statement.Span.Contains(stackAlloc.Span) ||
                 ifStatement.Else?.Statement.Span.Contains(stackAlloc.Span) == true) &&
                TryGetCap(
                    context,
                    ifStatement.Condition,
                    valueSymbol,
                    ifStatement.Statement.Span.Contains(stackAlloc.Span),
                    out _))
                return true;
        }

        return HasEarlierTerminatingCap(context, stackAlloc, valueSymbol);
    }

    private static bool HasEarlierTerminatingCap(
        SyntaxNodeAnalysisContext context,
        StackAllocArrayCreationExpressionSyntax stackAlloc,
        ISymbol valueSymbol)
    {
        var block = stackAlloc.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
        var containingStatement = stackAlloc.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        if (block is null || containingStatement is null)
            return false;

        var statementIndex = block.Statements.IndexOf(containingStatement);
        if (statementIndex < 0)
            return false;

        for (var index = 0; index < statementIndex; index++)
        {
            if (block.Statements[index] is IfStatementSyntax ifStatement &&
                TryGetCap(context, ifStatement.Condition, valueSymbol, false, out _) &&
                IsTerminating(ifStatement.Statement))
                return true;
        }

        return false;
    }

    private static bool TryGetCap(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax condition,
        ISymbol valueSymbol,
        bool whenTrue,
        out object? bound)
    {
        bound = null;
        condition = MeridianAnalyzerSemanticHelpers.Unwrap(condition);

        if (condition is PrefixUnaryExpressionSyntax prefix &&
            prefix.IsKind(SyntaxKind.LogicalNotExpression))
            return TryGetCap(context, prefix.Operand, valueSymbol, !whenTrue, out bound);

        if (condition is BinaryExpressionSyntax logical &&
            logical.IsKind(SyntaxKind.LogicalAndExpression) &&
            whenTrue &&
            (TryGetCap(context, logical.Left, valueSymbol, true, out bound) ||
             TryGetCap(context, logical.Right, valueSymbol, true, out bound)))
            return true;

        if (condition is not BinaryExpressionSyntax comparison)
            return false;

        var leftSymbol = context.SemanticModel.GetSymbolInfo(
            MeridianAnalyzerSemanticHelpers.Unwrap(comparison.Left),
            context.CancellationToken).Symbol;
        var rightSymbol = context.SemanticModel.GetSymbolInfo(
            MeridianAnalyzerSemanticHelpers.Unwrap(comparison.Right),
            context.CancellationToken).Symbol;
        var leftConstant = context.SemanticModel.GetConstantValue(comparison.Left, context.CancellationToken);
        var rightConstant = context.SemanticModel.GetConstantValue(comparison.Right, context.CancellationToken);

        var valueOnLeft = SymbolEqualityComparer.Default.Equals(leftSymbol, valueSymbol) && rightConstant.HasValue;
        var valueOnRight = SymbolEqualityComparer.Default.Equals(rightSymbol, valueSymbol) && leftConstant.HasValue;
        if (!valueOnLeft && !valueOnRight)
            return false;

        bound = valueOnLeft ? rightConstant.Value : leftConstant.Value;
        var kind = comparison.Kind();
        var valueIsCappedWhenTrue = valueOnLeft
            ? kind is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
            : kind is SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression;

        return whenTrue == valueIsCappedWhenTrue;
    }

    private static bool IsTerminating(StatementSyntax statement)
    {
        return statement switch
        {
            ReturnStatementSyntax or ThrowStatementSyntax or BreakStatementSyntax or ContinueStatementSyntax => true,
            BlockSyntax block when block.Statements.Count > 0 => IsTerminating(
                block.Statements[block.Statements.Count - 1]),
            _ => false
        };
    }
}
