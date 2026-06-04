using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0021EnforceBackendLoggingContractAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0021";

    private static readonly LocalizableString Title = "Use the backend Serilog logging contract";
    private static readonly LocalizableString MessageFormat = "Avoid Microsoft ILogger<T> or Console logging in production backend code outside framework-edge boundaries";
    private static readonly LocalizableString Description =
        "Meridian backend runtime code standardises on Serilog. Microsoft ILogger<T> and Console output should stay in framework adapters, hosting edges, CLI/tools, or explicitly documented exceptions.";

    private static readonly string[] ApprovedPathSegments =
    {
        "/Configuration/",
        "/Diagnostics/",
        "/Filters/",
        "/Health/",
        "/Hosting/",
        "/Logging/",
        "/Middleware/",
        "/Observability/",
        "/Program.cs",
        "/Startup/",
        "/Telemetry/",
        "/Meridian.CLI/",
        "/Meridian.OpenApiExporter/",
        "/Meridian.Analyzer/",
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
        context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);
        context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ParameterSyntax parameter || IsApprovedLocation(parameter.SyntaxTree.FilePath))
        {
            return;
        }

        if (IsMicrosoftLoggerType(parameter.Type))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, parameter.GetLocation()));
        }
    }

    private static void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not FieldDeclarationSyntax fieldDeclaration || IsApprovedLocation(fieldDeclaration.SyntaxTree.FilePath))
        {
            return;
        }

        if (IsMicrosoftLoggerType(fieldDeclaration.Declaration.Type))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, fieldDeclaration.Declaration.Type.GetLocation()));
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation || IsApprovedLocation(invocation.SyntaxTree.FilePath))
        {
            return;
        }

        if (IsConsoleWrite(invocation))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        }
    }

    private static bool IsMicrosoftLoggerType(TypeSyntax? type)
    {
        if (type is null)
        {
            return false;
        }

        var typeName = type.ToString();
        return typeName.StartsWith("ILogger<", StringComparison.Ordinal) ||
               typeName.Contains(".ILogger<", StringComparison.Ordinal) ||
               typeName.StartsWith("Microsoft.Extensions.Logging.ILogger<", StringComparison.Ordinal);
    }

    private static bool IsConsoleWrite(InvocationExpressionSyntax invocation)
    {
        var expression = invocation.Expression.ToString();
        return expression.StartsWith("Console.Write", StringComparison.Ordinal) ||
               expression.StartsWith("Console.Error.Write", StringComparison.Ordinal) ||
               expression.StartsWith("Console.Out.Write", StringComparison.Ordinal) ||
               expression.StartsWith("System.Console.Write", StringComparison.Ordinal) ||
               expression.StartsWith("System.Console.Error.Write", StringComparison.Ordinal) ||
               expression.StartsWith("System.Console.Out.Write", StringComparison.Ordinal);
    }

    private static bool IsApprovedLocation(string filePath)
    {
        return MeridianAnalyzerRuleHelpers.IsTestPath(filePath) ||
               MeridianAnalyzerSyntaxHelpers.PathContainsAny(filePath, ApprovedPathSegments);
    }
}
