using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0059GuardSearchResultUseAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0059";

    private static readonly LocalizableString Title = "Guard search results before index use";

    private static readonly LocalizableString MessageFormat =
        "Check this search result before using it as an index or range boundary";

    private static readonly LocalizableString Description =
        "IndexOf, FindIndex, and BinarySearch report misses with negative values that cannot be used as ordinary indexes.";

    private static readonly string[] SearchMethodNames =
    {
        "IndexOf",
        "IndexOfAny",
        "LastIndexOf",
        "LastIndexOfAny",
        "FindIndex",
        "FindLastIndex",
        "BinarySearch"
    };

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
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax searchInvocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(searchInvocation.SyntaxTree.FilePath) ||
            !IsSearchInvocation(context, searchInvocation, out var isBinarySearch))
            return;

        var local = GetAssignedSymbol(context, searchInvocation);
        if (local is null)
        {
            if (HasDirectIndexUse(context, searchInvocation, isBinarySearch))
                context.ReportDiagnostic(Diagnostic.Create(Rule, searchInvocation.GetLocation()));
            return;
        }

        var method = searchInvocation.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method is null)
            return;

        var references = method.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => identifier.SpanStart > searchInvocation.Span.End)
            .Where(identifier => SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                local))
            .OrderBy(identifier => identifier.SpanStart)
            .ToArray();

        foreach (var reference in references)
        {
            if (IsSimpleReassignment(reference))
                return;

            if (IsIndexUse(context, reference))
            {
                if (!IsExplicitBinarySearchConversion(reference, isBinarySearch) &&
                    !HasValidSearchGuard(context, reference, local, isBinarySearch))
                    context.ReportDiagnostic(Diagnostic.Create(Rule, reference.GetLocation()));

                return;
            }

            if (!IsReadReference(reference))
                continue;

            if (IsGuardConditionReference(context, reference, local, isBinarySearch))
                continue;

            return;
        }
    }

    private static bool IsSearchInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        out bool isBinarySearch)
    {
        isBinarySearch = false;
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (method?.ReturnType.SpecialType != SpecialType.System_Int32 ||
            !SearchMethodNames.Contains(method.Name, StringComparer.Ordinal))
            return false;

        isBinarySearch = method.Name == "BinarySearch";

        var type = method.ContainingType;
        var namespaceName = type?.ContainingNamespace?.ToDisplayString();
        return (namespaceName == "System" && type?.Name is ("String" or "Array" or "MemoryExtensions" or
            "Span" or "ReadOnlySpan")) ||
               (namespaceName == "System.Collections.Generic" && type?.Name == "List") ||
               (namespaceName == "System.Collections.Immutable" && type?.Name == "ImmutableArray");
    }

    private static ISymbol? GetAssignedSymbol(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        foreach (var ancestor in invocation.Ancestors())
        {
            if (ancestor is VariableDeclaratorSyntax declaration &&
                declaration.Initializer?.Value.Span.Contains(invocation.Span) == true)
                return context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);

            if (ancestor is AssignmentExpressionSyntax assignment &&
                assignment.Right.Span.Contains(invocation.Span))
                return context.SemanticModel.GetSymbolInfo(
                    assignment.Left,
                    context.CancellationToken).Symbol;

            if (ancestor is MethodDeclarationSyntax)
                break;
        }

        return null;
    }

    private static bool HasDirectIndexUse(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax searchInvocation,
        bool isBinarySearch)
    {
        if (IsExplicitBinarySearchConversion(searchInvocation, isBinarySearch))
            return false;

        foreach (var ancestor in searchInvocation.Ancestors())
        {
            if (ancestor is RangeExpressionSyntax || ancestor is ElementAccessExpressionSyntax)
                return true;

            if (ancestor is InvocationExpressionSyntax invocation &&
                IsIndexTakingInvocation(context, invocation) &&
                invocation.ArgumentList.Arguments.Any(argument => argument.Expression.Span.Contains(searchInvocation.Span)))
                return true;

            if (ancestor is StatementSyntax)
                return false;
        }

        return false;
    }

    private static bool IsIndexUse(
        SyntaxNodeAnalysisContext context,
        IdentifierNameSyntax identifier)
    {
        foreach (var ancestor in identifier.Ancestors())
        {
            if (ancestor is RangeExpressionSyntax || ancestor is ElementAccessExpressionSyntax)
                return true;

            if (ancestor is InvocationExpressionSyntax invocation &&
                IsIndexTakingInvocation(context, invocation) &&
                invocation.ArgumentList.Arguments.Any(argument => argument.Expression.Span.Contains(identifier.Span)))
                return true;

            if (ancestor is StatementSyntax)
                return false;
        }

        return false;
    }

    private static bool IsIndexTakingInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        var type = method?.ContainingType;
        var namespaceName = type?.ContainingNamespace?.ToDisplayString();
        return (method?.Name is "Substring" or "Remove") && namespaceName == "System" && type?.Name == "String" ||
               (method?.Name is "RemoveAt" or "Insert" or "GetRange") &&
               namespaceName == "System.Collections.Generic" && type?.Name == "List" ||
               method?.Name == "Slice" && namespaceName == "System" &&
               type?.Name is ("Span" or "ReadOnlySpan" or "MemoryExtensions") ||
               (method?.Name is "Remove" or "Insert") && namespaceName == "System.Text" && type?.Name == "StringBuilder";
    }

    private static bool IsExplicitBinarySearchConversion(SyntaxNode node, bool isBinarySearch)
    {
        return isBinarySearch && node.AncestorsAndSelf().Any(ancestor =>
            ancestor is PrefixUnaryExpressionSyntax prefix &&
            prefix.IsKind(SyntaxKind.BitwiseNotExpression) &&
            prefix.Operand.Span.Contains(node.Span));
    }

    private static bool HasValidSearchGuard(
        SyntaxNodeAnalysisContext context,
        IdentifierNameSyntax identifier,
        ISymbol searchSymbol,
        bool isBinarySearch)
    {
        foreach (var ancestor in identifier.Ancestors())
        {
            if (ancestor is BinaryExpressionSyntax logical &&
                logical.IsKind(SyntaxKind.LogicalAndExpression) &&
                logical.Right.Span.Contains(identifier.Span) &&
                TryGetValidity(context, logical.Left, searchSymbol, isBinarySearch, true, out _))
                return true;

            if (ancestor is IfStatementSyntax ifStatement &&
                !ifStatement.Condition.Span.Contains(identifier.Span))
            {
                var inThen = ifStatement.Statement.Span.Contains(identifier.Span);
                var inElse = ifStatement.Else?.Statement.Span.Contains(identifier.Span) == true;
                if ((inThen || inElse) &&
                    TryGetValidity(context, ifStatement.Condition, searchSymbol, isBinarySearch, inThen, out _))
                    return true;
            }

            if (ancestor is ConditionalExpressionSyntax conditional &&
                !conditional.Condition.Span.Contains(identifier.Span))
            {
                var inThen = conditional.WhenTrue.Span.Contains(identifier.Span);
                var inElse = conditional.WhenFalse.Span.Contains(identifier.Span);
                if ((inThen || inElse) &&
                    TryGetValidity(context, conditional.Condition, searchSymbol, isBinarySearch, inThen, out _))
                    return true;
            }
        }

        var method = identifier.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method is null)
            return false;

        foreach (var ifStatement in method.DescendantNodes().OfType<IfStatementSyntax>())
        {
            if (ifStatement.SpanStart >= identifier.SpanStart ||
                ifStatement.Span.End >= identifier.SpanStart ||
                !TryGetValidity(context, ifStatement.Condition, searchSymbol, isBinarySearch, false, out _) ||
                !IsTerminating(ifStatement.Statement))
                continue;

            if (ifStatement.Else is null ||
                !ifStatement.Else.Statement.Span.Contains(identifier.Span))
                return true;
        }

        return false;
    }

    private static bool IsGuardConditionReference(
        SyntaxNodeAnalysisContext context,
        IdentifierNameSyntax identifier,
        ISymbol searchSymbol,
        bool isBinarySearch)
    {
        foreach (var ancestor in identifier.Ancestors())
        {
            var condition = ancestor switch
            {
                IfStatementSyntax ifStatement when ifStatement.Condition.Span.Contains(identifier.Span) =>
                    ifStatement.Condition,
                WhileStatementSyntax whileStatement when whileStatement.Condition.Span.Contains(identifier.Span) =>
                    whileStatement.Condition,
                DoStatementSyntax doStatement when doStatement.Condition.Span.Contains(identifier.Span) =>
                    doStatement.Condition,
                ForStatementSyntax forStatement when forStatement.Condition?.Span.Contains(identifier.Span) == true =>
                    forStatement.Condition,
                ConditionalExpressionSyntax conditional when conditional.Condition.Span.Contains(identifier.Span) =>
                    conditional.Condition,
                _ => null
            };

            if (condition is not null &&
                (TryGetValidity(context, condition, searchSymbol, isBinarySearch, true, out _) ||
                 TryGetValidity(context, condition, searchSymbol, isBinarySearch, false, out _)))
                return true;
        }

        return false;
    }

    private static bool TryGetValidity(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax condition,
        ISymbol searchSymbol,
        bool isBinarySearch,
        bool whenTrue,
        out object? comparisonValue)
    {
        comparisonValue = null;
        condition = MeridianAnalyzerSemanticHelpers.Unwrap(condition);
        if (condition is PrefixUnaryExpressionSyntax prefix &&
            prefix.IsKind(SyntaxKind.LogicalNotExpression))
            return TryGetValidity(context, prefix.Operand, searchSymbol, isBinarySearch, !whenTrue, out comparisonValue);

        if (condition is BinaryExpressionSyntax logical &&
            logical.IsKind(SyntaxKind.LogicalAndExpression) &&
            whenTrue &&
            (TryGetValidity(context, logical.Left, searchSymbol, isBinarySearch, true, out comparisonValue) ||
             TryGetValidity(context, logical.Right, searchSymbol, isBinarySearch, true, out comparisonValue)))
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
        var valueOnLeft = SymbolEqualityComparer.Default.Equals(leftSymbol, searchSymbol) && rightConstant.HasValue;
        var valueOnRight = SymbolEqualityComparer.Default.Equals(rightSymbol, searchSymbol) && leftConstant.HasValue;
        if (!valueOnLeft && !valueOnRight)
            return false;

        comparisonValue = valueOnLeft ? rightConstant.Value : leftConstant.Value;
        var kind = valueOnLeft ? comparison.Kind() : ReverseComparison(comparison.Kind());
        if (!TryGetInteger(comparisonValue, out var numericValue) ||
            !TryGetValidWhenTrue(kind, numericValue, isBinarySearch, out var validWhenTrue))
            return false;

        return whenTrue == validWhenTrue;
    }

    private static SyntaxKind ReverseComparison(SyntaxKind kind)
    {
        return kind switch
        {
            SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
            SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
            _ => kind
        };
    }

    private static bool TryGetValidWhenTrue(
        SyntaxKind kind,
        long value,
        bool isBinarySearch,
        out bool validWhenTrue)
    {
        validWhenTrue = false;
        switch (kind)
        {
            case SyntaxKind.GreaterThanOrEqualExpression when value >= 0:
            case SyntaxKind.GreaterThanExpression when value >= -1:
            case SyntaxKind.EqualsExpression when value >= 0:
                validWhenTrue = true;
                return true;
            case SyntaxKind.LessThanExpression when value >= 0:
            case SyntaxKind.LessThanOrEqualExpression when value >= -1:
                return true;
            case SyntaxKind.EqualsExpression when value == -1 && !isBinarySearch:
                return true;
            case SyntaxKind.NotEqualsExpression when value == -1 && !isBinarySearch:
                validWhenTrue = true;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetInteger(object? value, out long number)
    {
        number = 0;
        switch (value)
        {
            case sbyte signedByte:
                number = signedByte;
                return true;
            case byte unsignedByte:
                number = unsignedByte;
                return true;
            case short signedShort:
                number = signedShort;
                return true;
            case ushort unsignedShort:
                number = unsignedShort;
                return true;
            case int signedInt:
                number = signedInt;
                return true;
            case uint unsignedInt:
                number = unsignedInt;
                return true;
            case long signedLong:
                number = signedLong;
                return true;
            case ulong unsignedLong when unsignedLong <= long.MaxValue:
                number = (long)unsignedLong;
                return true;
            default:
                return false;
        }
    }

    private static bool IsSimpleReassignment(IdentifierNameSyntax identifier)
    {
        return identifier.Parent is AssignmentExpressionSyntax assignment &&
               assignment.Left == identifier &&
               assignment.IsKind(SyntaxKind.SimpleAssignmentExpression);
    }

    private static bool IsReadReference(IdentifierNameSyntax identifier)
    {
        return !(identifier.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == identifier) &&
               !(identifier.Parent is AssignmentExpressionSyntax assignment &&
                 assignment.Left == identifier &&
                 assignment.IsKind(SyntaxKind.SimpleAssignmentExpression));
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
