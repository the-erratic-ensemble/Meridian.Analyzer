using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0007ContainRawConfigurationReadsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0007";

    private static readonly LocalizableString Title = "Contain raw configuration and environment reads";
    private static readonly LocalizableString MessageFormat = "Move raw configuration/environment reads behind typed options, startup guards, or provider adapters";
    private static readonly LocalizableString Description =
        "Direct Environment and IConfiguration reads bypass typed option validation and the Meridian configuration catalog. Runtime feature code should depend on validated options instead.";

    private static readonly string[] ApprovedPathSegments =
    {
        "/Configuration/",
        "/Configurations/",
        "/Options/",
        "/Provider",
        "/Providers/",
        "/Startup/",
        "/StartupGuards.cs",
        "/StartupEnvironmentLoader.cs",
        "/Program.cs",
        "/Meridian.CLI/",
        "/Meridian.OpenApiExporter/",
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
        context.RegisterSyntaxNodeAction(AnalyzeElementAccess, SyntaxKind.ElementAccessExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation || IsApprovedLocation(invocation.SyntaxTree.FilePath))
        {
            return;
        }

        if (IsEnvironmentRead(invocation) || IsConfigurationLookup(invocation))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        }
    }

    private static void AnalyzeElementAccess(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ElementAccessExpressionSyntax elementAccess || IsApprovedLocation(elementAccess.SyntaxTree.FilePath))
        {
            return;
        }

        if (LooksLikeConfigurationReceiver(elementAccess.Expression.ToString()))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, elementAccess.GetLocation()));
        }
    }

    private static bool IsEnvironmentRead(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Expression.ToString() is "Environment" or "System.Environment" &&
               memberAccess.Name.Identifier.ValueText == "GetEnvironmentVariable";
    }

    private static bool IsConfigurationLookup(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var memberName = memberAccess.Name.Identifier.ValueText;
        return memberName is "GetValue" or "GetSection" or "GetRequiredSection" or "GetConnectionString" &&
               LooksLikeConfigurationReceiver(memberAccess.Expression.ToString());
    }

    private static bool LooksLikeConfigurationReceiver(string receiver)
    {
        return receiver.IndexOf("configuration", StringComparison.OrdinalIgnoreCase) >= 0 ||
               receiver.IndexOf("config", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsApprovedLocation(string filePath)
    {
        return MeridianAnalyzerRuleHelpers.IsTestPath(filePath) ||
               MeridianAnalyzerSyntaxHelpers.PathContainsAny(filePath, ApprovedPathSegments);
    }
}
