using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0025AvoidIsPatternBracesAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0025";

    private static readonly LocalizableString Title = "Avoid empty is-pattern brace syntax";
    private static readonly LocalizableString MessageFormat =
        "Avoid empty brace pattern syntax in '{0}'; prefer Shared helpers or explicit null checks where applicable";
    private static readonly LocalizableString Description =
        "Empty property-pattern braces such as `is { }`, `is not { }`, and tuple elements like `({ }, { })` hide the shared helper and explicit null-check patterns used across the codebase.";

    private static readonly string[] ExcludedPathSegments =
    {
        "/Meridian.Analyzer/"
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
        context.RegisterSyntaxNodeAction(AnalyzeIsPatternExpression, SyntaxKind.IsPatternExpression);
    }

    private static void AnalyzeIsPatternExpression(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not IsPatternExpressionSyntax isPatternExpression ||
            IsExcludedLocation(isPatternExpression.SyntaxTree.FilePath) ||
            !ContainsEmptyBracePattern(isPatternExpression.Pattern))
        {
            return;
        }

        var patternText = NormalizePatternText(isPatternExpression.Pattern);
        context.ReportDiagnostic(Diagnostic.Create(Rule, isPatternExpression.Pattern.GetLocation(), patternText));
    }

    private static bool ContainsEmptyBracePattern(PatternSyntax pattern)
    {
        return pattern
            .DescendantNodesAndSelf()
            .OfType<RecursivePatternSyntax>()
            .Any(recursivePattern => recursivePattern.PropertyPatternClause is { Subpatterns.Count: 0 });
    }

    private static bool IsExcludedLocation(string filePath)
    {
        return MeridianAnalyzerRuleHelpers.IsTestPath(filePath) ||
               MeridianAnalyzerSyntaxHelpers.PathContainsAny(filePath, ExcludedPathSegments);
    }

    private static string NormalizePatternText(PatternSyntax pattern)
    {
        var singleLine = pattern
            .ToString()
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        return string.Join(" ", singleLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
