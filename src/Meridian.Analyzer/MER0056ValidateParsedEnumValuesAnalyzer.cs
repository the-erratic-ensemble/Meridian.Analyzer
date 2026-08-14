using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0056ValidateParsedEnumValuesAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0056";

    private static readonly LocalizableString Title = "Validate parsed ordinary enum values";

    private static readonly LocalizableString MessageFormat =
        "Check Enum.Parse or Enum.TryParse with Enum.IsDefined before using this ordinary enum value";

    private static readonly LocalizableString Description =
        "Enum parsing accepts numeric text that can produce an undeclared value in an ordinary enum.";

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
        if (context.Node is not InvocationExpressionSyntax invocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(invocation.SyntaxTree.FilePath) ||
            !TryGetParsedEnumType(context, invocation, out var enumType, out var isTryParse) ||
            HasFlagsAttribute(enumType) ||
            (isTryParse
                ? HasTryParseDefinedCheck(context, invocation, enumType)
                : HasParseDefinedCheck(context, invocation, enumType)))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool TryGetParsedEnumType(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        out INamedTypeSymbol enumType,
        out bool isTryParse)
    {
        enumType = null!;
        isTryParse = false;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method ||
            method.ContainingType?.SpecialType != SpecialType.System_Enum ||
            method.Name is not ("Parse" or "TryParse") ||
            !method.IsGenericMethod ||
            method.TypeArguments.Length != 1 ||
            method.TypeArguments[0] is not INamedTypeSymbol namedType ||
            namedType.TypeKind != TypeKind.Enum)
            return false;

        enumType = namedType;
        isTryParse = method.Name == "TryParse";
        return true;
    }

    private static bool HasParseDefinedCheck(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol enumType)
    {
        if (invocation.Ancestors().OfType<InvocationExpressionSyntax>().Any(ancestor =>
                IsEnumIsDefined(context, ancestor, enumType) &&
                ancestor.ArgumentList.Arguments.Any(argument =>
                    argument.Expression.Span.Contains(invocation.Span))))
            return true;

        var local = GetAssignedLocal(context, invocation);
        if (local is null)
            return false;

        var references = invocation.AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault()?
            .DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => identifier.SpanStart > invocation.Span.End)
            .Where(identifier => SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                local))
            .OrderBy(identifier => identifier.SpanStart)
            .ToArray();

        return references is { Length: > 0 } &&
               references[0].AncestorsAndSelf()
                   .OfType<InvocationExpressionSyntax>()
                   .Any(ancestor =>
                       IsEnumIsDefined(context, ancestor, enumType) &&
                       HasDefinedValueArgument(context, ancestor, local));
    }

    private static bool HasTryParseDefinedCheck(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol enumType)
    {
        if (!TryGetOutSymbol(context, invocation, out var parsedValue))
            return false;

        foreach (var ancestor in invocation.Ancestors())
        {
            var condition = ancestor switch
            {
                IfStatementSyntax ifStatement when ifStatement.Condition.Span.Contains(invocation.Span) =>
                    ifStatement.Condition,
                WhileStatementSyntax whileStatement when whileStatement.Condition.Span.Contains(invocation.Span) =>
                    whileStatement.Condition,
                DoStatementSyntax doStatement when doStatement.Condition.Span.Contains(invocation.Span) =>
                    doStatement.Condition,
                ForStatementSyntax forStatement when forStatement.Condition?.Span.Contains(invocation.Span) == true =>
                    forStatement.Condition,
                ConditionalExpressionSyntax conditional when conditional.Condition.Span.Contains(invocation.Span) =>
                    conditional.Condition,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression) &&
                                                   binary.Span.Contains(invocation.Span) => binary,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalOrExpression) &&
                                                   binary.Span.Contains(invocation.Span) => binary,
                _ => null
            };

            if (condition is not null &&
                (condition is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalOrExpression }
                 || IsNegated(context, invocation)) &&
                HasDefinedCheck(context, condition, parsedValue, enumType) &&
                (condition is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalOrExpression } ||
                 HasNegatedDefinedCheck(context, condition, parsedValue, enumType)))
                return true;
        }

        return false;
    }

    private static bool HasDefinedCheck(
        SyntaxNodeAnalysisContext context,
        SyntaxNode condition,
        ISymbol parsedValue,
        INamedTypeSymbol enumType)
    {
        return condition.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                IsEnumIsDefined(context, invocation, enumType) &&
                HasDefinedValueArgument(context, invocation, parsedValue));
    }

    private static bool HasNegatedDefinedCheck(
        SyntaxNodeAnalysisContext context,
        SyntaxNode condition,
        ISymbol parsedValue,
        INamedTypeSymbol enumType)
    {
        return condition.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                IsEnumIsDefined(context, invocation, enumType) &&
                IsNegated(context, invocation) &&
                HasDefinedValueArgument(context, invocation, parsedValue));
    }

    private static bool HasDefinedValueArgument(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ISymbol parsedValue)
    {
        return invocation.ArgumentList.Arguments.Any(argument =>
            SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetSymbolInfo(
                    MeridianAnalyzerSemanticHelpers.Unwrap(argument.Expression),
                    context.CancellationToken).Symbol,
                parsedValue));
    }

    private static bool IsNegated(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        return invocation.Parent is PrefixUnaryExpressionSyntax prefix &&
               prefix.IsKind(SyntaxKind.LogicalNotExpression);
    }

    private static bool IsEnumIsDefined(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol enumType)
    {
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method ||
            method.ContainingType?.SpecialType != SpecialType.System_Enum ||
            method.Name != "IsDefined")
            return false;

        if (invocation.ArgumentList.Arguments.Count == 1)
            return method.IsGenericMethod &&
                   method.TypeArguments.Length == 1 &&
                   SymbolEqualityComparer.Default.Equals(method.TypeArguments[0], enumType);

        if (invocation.ArgumentList.Arguments.Count != 2)
            return false;

        var typeArgument = invocation.ArgumentList.Arguments[0].Expression;
        return typeArgument is TypeOfExpressionSyntax typeOf &&
               SymbolEqualityComparer.Default.Equals(
                   context.SemanticModel.GetTypeInfo(typeOf.Type, context.CancellationToken).Type,
                   enumType);
    }

    private static bool HasFlagsAttribute(INamedTypeSymbol enumType)
    {
        return enumType.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");
    }

    private static ILocalSymbol? GetAssignedLocal(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        foreach (var ancestor in invocation.Ancestors())
        {
            if (ancestor is VariableDeclaratorSyntax declaration &&
                declaration.Initializer?.Value.Span.Contains(invocation.Span) == true)
                return context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) as ILocalSymbol;

            if (ancestor is AssignmentExpressionSyntax assignment &&
                assignment.Right.Span.Contains(invocation.Span) &&
                assignment.Left is IdentifierNameSyntax identifier)
                return context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol as ILocalSymbol;

            if (ancestor is MethodDeclarationSyntax)
                break;
        }

        return null;
    }

    private static bool TryGetOutSymbol(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        out ISymbol symbol)
    {
        symbol = null!;
        var argument = invocation.ArgumentList.Arguments.FirstOrDefault(argument =>
            argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword));
        if (argument is null)
            return false;

        symbol = argument.Expression switch
        {
            IdentifierNameSyntax identifier => context.SemanticModel.GetSymbolInfo(
                identifier,
                context.CancellationToken).Symbol!,
            DeclarationExpressionSyntax declaration when declaration.Designation is SingleVariableDesignationSyntax designation =>
                context.SemanticModel.GetDeclaredSymbol(designation, context.CancellationToken)!,
            _ => null!
        };

        return symbol is not null;
    }
}
