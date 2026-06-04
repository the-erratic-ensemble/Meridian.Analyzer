using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0009RequireControllerCancellationTokenAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0009";

    private static readonly LocalizableString Title = "Expose cancellation at async controller boundaries";
    private static readonly LocalizableString MessageFormat = "Async controller actions should accept a CancellationToken and avoid CancellationToken.None in request-scoped code";
    private static readonly LocalizableString Description =
        "Meridian controller actions are request boundaries. Async actions should expose request cancellation and should not intentionally detach work from the request token.";

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
        context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodDeclaration ||
            !MeridianAnalyzerRuleHelpers.IsControllerAction(methodDeclaration) ||
            !MeridianAnalyzerRuleHelpers.IsAsyncLike(methodDeclaration))
        {
            return;
        }

        if (MeridianAnalyzerRuleHelpers.HasCancellationTokenParameter(methodDeclaration))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation()));
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MemberAccessExpressionSyntax memberAccess ||
            !MeridianAnalyzerRuleHelpers.IsMemberAccessNamed(memberAccess, "CancellationToken", "None"))
        {
            return;
        }

        var containingMethod = MeridianAnalyzerRuleHelpers.GetContainingMethod(memberAccess);
        if (containingMethod is null || !MeridianAnalyzerRuleHelpers.IsControllerAction(containingMethod))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation()));
    }
}
