using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0023OwnDetachedRuntimeTasksAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0023";

    private static readonly LocalizableString Title = "Own detached runtime tasks and cancellation";
    private static readonly LocalizableString MessageFormat = "Runtime background work should have an owned lifetime, cancellation path, and observability boundary";
    private static readonly LocalizableString Description =
        "Detached Task.Run, fire-and-forget async calls, and broad CancellationToken.None usage hide failures, shutdown behavior, and request cancellation semantics.";

    private static readonly string[] ApprovedPathSegments =
    {
        "/Migrations/",
        "/Meridian.CLI/",
        "/Meridian.Analyzers/",
        "/tools/",
        "/Tools/"
    };

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
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation || IsApprovedLocation(invocation))
        {
            return;
        }

        if (IsTaskRun(invocation))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        }
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not AssignmentExpressionSyntax assignment || IsApprovedLocation(assignment))
        {
            return;
        }

        if (assignment.Left is not IdentifierNameSyntax identifierName ||
            identifierName.Identifier.ValueText != "_" ||
            assignment.Right is not InvocationExpressionSyntax invocation ||
            IsTaskRun(invocation) ||
            !MeridianAnalyzerRuleHelpers.GetSimpleInvocationName(invocation).EndsWith("Async", StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.GetLocation()));
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MemberAccessExpressionSyntax memberAccess || IsApprovedLocation(memberAccess))
        {
            return;
        }

        if (!MeridianAnalyzerRuleHelpers.IsMemberAccessNamed(memberAccess, "CancellationToken", "None"))
        {
            return;
        }

        var containingMethod = MeridianAnalyzerRuleHelpers.GetContainingMethod(memberAccess);
        if (containingMethod is not null && MeridianAnalyzerRuleHelpers.IsControllerAction(containingMethod))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation()));
    }

    private static bool IsTaskRun(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Expression.ToString() is "Task" or "System.Threading.Tasks.Task" &&
               memberAccess.Name.Identifier.ValueText == "Run";
    }

    private static bool IsApprovedLocation(SyntaxNode node)
    {
        var filePath = node.SyntaxTree.FilePath;
        if (MeridianAnalyzerRuleHelpers.IsTestPath(filePath) ||
            MeridianAnalyzerSyntaxHelpers.PathContainsAny(filePath, ApprovedPathSegments))
        {
            return true;
        }

        var containingMethod = MeridianAnalyzerRuleHelpers.GetContainingMethod(node);
        var containingClass = MeridianAnalyzerRuleHelpers.GetContainingClass(node);
        return containingMethod?.Identifier.ValueText == "ExecuteAsync" &&
               containingClass is not null &&
               MeridianAnalyzerSyntaxHelpers.InheritsFrom(containingClass, "BackgroundService");
    }
}
