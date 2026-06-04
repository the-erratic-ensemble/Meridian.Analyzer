using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0018ContainRawSqlAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0018";

    private static readonly LocalizableString Title = "Contain raw SQL APIs";
    private static readonly LocalizableString MessageFormat = "Keep raw SQL in approved persistence boundaries and prefer interpolated APIs over raw SQL string construction";
    private static readonly LocalizableString Description =
        "Raw SQL in request/runtime code and unsafe raw SQL APIs are high-risk. SQL should live in repositories, migrations, CLI schema tools, or another named persistence boundary.";

    private static readonly string[] RawSqlMethodNames =
    {
        "ExecuteSqlRaw",
        "FromSqlRaw",
        "SqlQueryRaw"
    };

    private static readonly string[] InterpolatedSqlMethodNames =
    {
        "ExecuteSqlInterpolated",
        "FromSqlInterpolated",
        "SqlQueryInterpolated"
    };

    private static readonly string[] ApprovedPathSegments =
    {
        "/Database/",
        "/Migrations/",
        "/Persistence/",
        "/Repositories/",
        "/Repository/",
        "/Meridian.CLI/",
        "/tools/"
    };

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
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(invocation.SyntaxTree.FilePath))
        {
            return;
        }

        var invocationName = MeridianAnalyzerRuleHelpers.GetSimpleInvocationName(invocation);
        if (RawSqlMethodNames.Any(name => string.Equals(invocationName, name, StringComparison.Ordinal)))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
            return;
        }

        if (InterpolatedSqlMethodNames.Any(name => string.Equals(invocationName, name, StringComparison.Ordinal)) &&
            !IsApprovedSqlBoundary(invocation.SyntaxTree.FilePath))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        }
    }

    private static bool IsApprovedSqlBoundary(string filePath)
    {
        return MeridianAnalyzerSyntaxHelpers.PathContainsAny(filePath, ApprovedPathSegments);
    }
}
