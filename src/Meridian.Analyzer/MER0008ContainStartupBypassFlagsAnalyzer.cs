using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0008ContainStartupBypassFlagsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0008";

    private const string StartupBypassPrefix = "MERIDIAN_SKIP_";

    private static readonly LocalizableString Title = "Keep startup bypass flags inside approved startup guard boundaries";
    private static readonly LocalizableString MessageFormat = "Move MERIDIAN_SKIP_* access behind StartupGuards or a dedicated typed startup-skip options boundary";
    private static readonly LocalizableString Description =
        "MERIDIAN_SKIP_* flags can disable production-significant validation or services. " +
        "Raw reads should stay in StartupGuards, tests, or a dedicated typed startup-skip options boundary.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Security,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeElementAccess, SyntaxKind.ElementAccessExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var bypassLiteral = GetStartupBypassLiteral(invocation, context);
        if (bypassLiteral is null)
        {
            return;
        }

        if (IsApprovedBypassLocation(context.Node.SyntaxTree.FilePath))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static void AnalyzeElementAccess(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ElementAccessExpressionSyntax elementAccess)
        {
            return;
        }

        var bypassLiteral = elementAccess.ArgumentList.Arguments
            .Select(argument => GetStringConstant(argument.Expression, context))
            .FirstOrDefault(IsStartupBypassKey);
        if (bypassLiteral is null)
        {
            return;
        }

        if (IsApprovedBypassLocation(context.Node.SyntaxTree.FilePath))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, elementAccess.GetLocation()));
    }

    private static string? GetStartupBypassLiteral(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        var methodName = MeridianAnalyzerSyntaxHelpers.GetSimpleName(memberAccess.Name);
        if (methodName is not "GetEnvironmentVariable" and not "GetValue")
        {
            return null;
        }

        return invocation.ArgumentList.Arguments
            .Select(argument => GetStringConstant(argument.Expression, context))
            .FirstOrDefault(IsStartupBypassKey);
    }

    private static string? GetStringConstant(ExpressionSyntax expression, SyntaxNodeAnalysisContext context)
    {
        var literal = MeridianAnalyzerSyntaxHelpers.GetStringLiteral(expression);
        if (literal is not null)
        {
            return literal;
        }

        var constantValue = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);
        return constantValue is { HasValue: true, Value: string value }
            ? value
            : null;
    }

    private static bool IsStartupBypassKey(string? value)
    {
        return value is not null && value.StartsWith(StartupBypassPrefix, StringComparison.Ordinal);
    }

    private static bool IsApprovedBypassLocation(string filePath)
    {
        return MeridianAnalyzerSyntaxHelpers.PathContainsAny(
            filePath,
            "/Infrastructure/Startup/StartupGuards.cs",
            "/StartupSkipOptions",
            "/tests/",
            "/Tests/");
    }
}
