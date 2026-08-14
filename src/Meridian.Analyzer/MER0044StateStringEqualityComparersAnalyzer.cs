using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0044StateStringEqualityComparersAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0044";

    private static readonly LocalizableString Title = "State string equality semantics";

    private static readonly LocalizableString MessageFormat =
        "State the equality comparer for this string collection operation";

    private static readonly LocalizableString Description =
        "String-key collections and equality-based LINQ operations should make their equality policy explicit.";

    private static readonly string[] LinqMethodNames =
    {
        "ToDictionary",
        "ToHashSet",
        "ToLookup",
        "Distinct",
        "GroupBy",
        "Union",
        "Intersect",
        "Except"
    };

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Readability,
        DiagnosticSeverity.Warning,
        true,
        Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ObjectCreationExpressionSyntax objectCreation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(objectCreation.SyntaxTree.FilePath) ||
            context.SemanticModel.GetTypeInfo(
                objectCreation,
                context.CancellationToken).Type is not INamedTypeSymbol type ||
            !IsStringEqualityCollection(type) ||
            objectCreation.ArgumentList is { } argumentList &&
            HasExplicitStringComparer(context, argumentList.Arguments))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, objectCreation.GetLocation()));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(invocation.SyntaxTree.FilePath) ||
            context.SemanticModel.GetSymbolInfo(
                invocation,
                context.CancellationToken).Symbol is not IMethodSymbol method ||
            !IsEnumerableMethod(method) ||
            !LinqMethodNames.Contains(method.Name, StringComparer.Ordinal) ||
            !UsesStringEqualityType(method) ||
            HasExplicitStringComparer(context, invocation.ArgumentList.Arguments))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsStringEqualityCollection(INamedTypeSymbol type)
    {
        return string.Equals(type.ContainingNamespace?.ToDisplayString(), "System.Collections.Generic",
                   StringComparison.Ordinal) &&
               type.Name is "Dictionary" or "HashSet" &&
               type.TypeArguments.Length > 0 &&
               type.TypeArguments[0].SpecialType == SpecialType.System_String;
    }

    private static bool IsEnumerableMethod(IMethodSymbol method)
    {
        return string.Equals(method.ContainingType?.Name, "Enumerable", StringComparison.Ordinal) &&
               string.Equals(method.ContainingNamespace?.ToDisplayString(), "System.Linq",
                   StringComparison.Ordinal);
    }

    private static bool UsesStringEqualityType(IMethodSymbol method)
    {
        return method.Name switch
        {
            "ToDictionary" or "ToLookup" => method.TypeArguments.Length > 1 &&
                method.TypeArguments[1].SpecialType == SpecialType.System_String,
            "ToHashSet" or "Distinct" or "Union" or "Intersect" or "Except" =>
                method.TypeArguments.Length > 0 &&
                method.TypeArguments[0].SpecialType == SpecialType.System_String,
            "GroupBy" => method.TypeArguments.Length > 1 &&
                method.TypeArguments[1].SpecialType == SpecialType.System_String,
            _ => false
        };
    }

    private static bool HasExplicitStringComparer(
        SyntaxNodeAnalysisContext context,
        SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        return arguments.Any(argument => IsExplicitStringComparer(context, argument.Expression));
    }

    private static bool IsExplicitStringComparer(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression)
    {
        expression = Unwrap(expression);
        if (expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } ||
            expression is DefaultExpressionSyntax ||
            expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.DefaultLiteralExpression })
            return false;

        var typeInfo = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken);
        var type = typeInfo.ConvertedType ?? typeInfo.Type;
        return IsStringEqualityComparer(type) ||
               type?.AllInterfaces.Any(interfaceType => IsStringEqualityComparer(interfaceType)) == true;
    }

    private static bool IsStringEqualityComparer(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol namedType &&
               string.Equals(namedType.Name, "IEqualityComparer", StringComparison.Ordinal) &&
               string.Equals(namedType.ContainingNamespace?.ToDisplayString(), "System.Collections.Generic",
                   StringComparison.Ordinal) &&
               namedType.TypeArguments.Length == 1 &&
               namedType.TypeArguments[0].SpecialType == SpecialType.System_String;
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax || expression is CastExpressionSyntax)
        {
            expression = expression switch
            {
                ParenthesizedExpressionSyntax parenthesizedExpression => parenthesizedExpression.Expression,
                CastExpressionSyntax castExpression => castExpression.Expression,
                _ => expression
            };
        }

        return expression;
    }
}
