using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0019UseProblemDetailsBoundaryAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0019";

    private static readonly LocalizableString Title = "Use the shared ProblemDetails boundary";
    private static readonly LocalizableString MessageFormat = "Controller actions should use shared ProblemDetails helpers instead of constructing ProblemDetails inline";
    private static readonly LocalizableString Description =
        "Meridian controller errors should use the shared RFC 7807 mapping boundary so status codes, codes, and extensions remain consistent.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Reliability,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ObjectCreationExpressionSyntax objectCreation)
        {
            return;
        }

        var containingMethod = MeridianAnalyzerRuleHelpers.GetContainingMethod(objectCreation);
        if (containingMethod is null || !MeridianAnalyzerRuleHelpers.IsControllerAction(containingMethod))
        {
            return;
        }

        var typeName = objectCreation.Type.ToString();
        if (typeName == "ProblemDetails" || typeName.EndsWith(".ProblemDetails", StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, objectCreation.Type.GetLocation()));
        }
    }
}
