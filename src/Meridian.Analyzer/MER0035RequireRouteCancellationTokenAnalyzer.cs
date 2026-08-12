using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0035RequireRouteCancellationTokenAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0035";

    private static readonly LocalizableString Title = "Propagate cancellation from async route boundaries";

    private static readonly LocalizableString MessageFormat =
        "Async route boundaries should accept and forward a CancellationToken instead of CancellationToken.None";

    private static readonly LocalizableString Description =
        "Route handlers are request boundaries. Async handlers that invoke cancellable work should expose and forward the request or host cancellation token.";

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
        context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodDeclaration ||
            IsExcludedLocation(methodDeclaration) ||
            !IsAsyncRouteBoundary(methodDeclaration) ||
            MeridianAnalyzerRuleHelpers.HasCancellationTokenParameter(methodDeclaration) ||
            !ContainsCancellationTokenNone(methodDeclaration))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation()));
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MemberAccessExpressionSyntax memberAccess ||
            IsExcludedLocation(memberAccess) ||
            !MeridianAnalyzerRuleHelpers.IsMemberAccessNamed(memberAccess, "CancellationToken", "None"))
            return;

        var containingMethod = MeridianAnalyzerRuleHelpers.GetContainingMethod(memberAccess);
        if (containingMethod is null ||
            !IsAsyncRouteBoundary(containingMethod) ||
            !MeridianAnalyzerRuleHelpers.HasCancellationTokenParameter(containingMethod))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation()));
    }

    private static bool IsAsyncRouteBoundary(MethodDeclarationSyntax methodDeclaration)
    {
        var containingClass = MeridianAnalyzerRuleHelpers.GetContainingClass(methodDeclaration);
        var methodName = methodDeclaration.Identifier.ValueText;
        return containingClass?.Identifier.ValueText.EndsWith("Routes", StringComparison.Ordinal) == true &&
               methodName.StartsWith("Handle", StringComparison.Ordinal) &&
               methodName.EndsWith("Async", StringComparison.Ordinal) &&
               MeridianAnalyzerRuleHelpers.IsAsyncLike(methodDeclaration) &&
               methodDeclaration.ParameterList.Parameters.Any(parameter =>
                   parameter.Type?.ToString().EndsWith("Request", StringComparison.Ordinal) == true);
    }

    private static bool ContainsCancellationTokenNone(MethodDeclarationSyntax methodDeclaration)
    {
        return methodDeclaration.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess =>
                MeridianAnalyzerRuleHelpers.IsMemberAccessNamed(memberAccess, "CancellationToken", "None"));
    }

    private static bool IsExcludedLocation(SyntaxNode node)
    {
        return MeridianAnalyzerRuleHelpers.IsTestPath(node.SyntaxTree.FilePath);
    }
}
