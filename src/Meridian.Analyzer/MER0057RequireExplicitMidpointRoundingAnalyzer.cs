using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0057RequireExplicitMidpointRoundingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0057";

    private static readonly LocalizableString Title = "State the midpoint rounding mode";

    private static readonly LocalizableString MessageFormat =
        "Pass MidpointRounding explicitly so half-value behavior is visible";

    private static readonly LocalizableString Description =
        "Math.Round and MathF.Round default to midpoint-to-even rounding when no mode is supplied.";

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
            context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method ||
            method.Name != "Round" ||
            method.ContainingType is not
            {
                Name: "Math" or "MathF",
                ContainingNamespace: { } containingNamespace
            } ||
            containingNamespace.ToDisplayString() != "System" ||
            method.Parameters.Any(parameter =>
                parameter.Type.Name == "MidpointRounding" &&
                parameter.Type.ContainingNamespace?.ToDisplayString() == "System"))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }
}
