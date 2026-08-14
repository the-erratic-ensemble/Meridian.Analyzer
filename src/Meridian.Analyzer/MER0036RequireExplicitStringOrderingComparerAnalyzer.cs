using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0036RequireExplicitStringOrderingComparerAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0036";

    private static readonly LocalizableString Title = "Require an explicit string ordering comparer";

    private static readonly LocalizableString MessageFormat =
        "Pass an explicit comparer to this string ordering operation";

    private static readonly LocalizableString Description =
        "String ordering should state its comparer so deterministic and culture-aware order remain visible at the call site.";

    private static readonly string[] OrderingMethodNames =
    {
        "OrderBy",
        "OrderByDescending",
        "ThenBy",
        "ThenByDescending"
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
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(invocation.SyntaxTree.FilePath) ||
            context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method ||
            invocation.ArgumentList.Arguments.Count != 1 ||
            !OrderingMethodNames.Contains(method.Name, StringComparer.Ordinal) ||
            !string.Equals(method.ContainingType?.Name, "Enumerable", StringComparison.Ordinal) ||
            !string.Equals(method.ContainingNamespace?.ToDisplayString(), "System.Linq", StringComparison.Ordinal) ||
            method.TypeArguments.Length < 2 ||
            method.TypeArguments[1].SpecialType != SpecialType.System_String)
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ObjectCreationExpressionSyntax objectCreation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(objectCreation.SyntaxTree.FilePath) ||
            context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken).Type is not INamedTypeSymbol type ||
            !IsSupportedSortedType(type) ||
            type.TypeArguments.Length == 0 ||
            type.TypeArguments[0].SpecialType != SpecialType.System_String ||
            HasExplicitComparer(context, objectCreation))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, objectCreation.GetLocation()));
    }

    private static bool IsSupportedSortedType(INamedTypeSymbol type)
    {
        return string.Equals(type.ContainingNamespace?.ToDisplayString(), "System.Collections.Generic",
                   StringComparison.Ordinal) &&
               type.Name is "SortedSet" or "SortedDictionary";
    }

    private static bool HasExplicitComparer(
        SyntaxNodeAnalysisContext context,
        ObjectCreationExpressionSyntax objectCreation)
    {
        return objectCreation.ArgumentList?.Arguments.Any(argument =>
        {
            var type = context.SemanticModel.GetTypeInfo(argument.Expression, context.CancellationToken).Type;
            return type is not null &&
                   (type.AllInterfaces.Any(interfaceType => IsStringComparerInterface(interfaceType)) ||
                    IsStringComparerInterface(type));
        }) == true;
    }

    private static bool IsStringComparerInterface(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               string.Equals(namedType.Name, "IComparer", StringComparison.Ordinal) &&
               string.Equals(namedType.ContainingNamespace?.ToDisplayString(), "System.Collections.Generic",
                   StringComparison.Ordinal) &&
               namedType.TypeArguments.Length == 1 &&
               namedType.TypeArguments[0].SpecialType == SpecialType.System_String;
    }
}
