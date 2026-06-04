using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0016UseSharedJsonProfilesAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0016";

    private static readonly LocalizableString Title = "Use shared Meridian JSON profiles";
    private static readonly LocalizableString MessageFormat = "Move ad hoc JSON options into MeridianJsonProfiles or a named JSON options factory";
    private static readonly LocalizableString Description =
        "Ad hoc System.Text.Json option construction in runtime code creates serializer drift. Shared profiles should own the option shape unless a dedicated factory documents the exception.";

    private static readonly string[] ApprovedPathSegments =
    {
        "/MeridianJsonProfiles.cs",
        "/Migrations/",
        "/Meridian.CLI/",
        "/Meridian.OpenApiExporter/",
        "/tools/"
    };

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Architecture,
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
        if (context.Node is not ObjectCreationExpressionSyntax objectCreation || IsApprovedLocation(objectCreation))
        {
            return;
        }

        var typeName = objectCreation.Type.ToString();
        if (typeName.EndsWith("JsonSerializerOptions", StringComparison.Ordinal) ||
            typeName.EndsWith("JsonDocumentOptions", StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, objectCreation.Type.GetLocation()));
        }
    }

    private static bool IsApprovedLocation(SyntaxNode node)
    {
        var filePath = node.SyntaxTree.FilePath;
        var containingClass = MeridianAnalyzerRuleHelpers.GetContainingClass(node);
        var className = containingClass?.Identifier.ValueText ?? string.Empty;

        return MeridianAnalyzerRuleHelpers.IsTestPath(filePath) ||
               MeridianAnalyzerSyntaxHelpers.PathContainsAny(filePath, ApprovedPathSegments) ||
               className.Contains("JsonProfile", StringComparison.Ordinal) ||
               className.Contains("JsonOptionsFactory", StringComparison.Ordinal);
    }
}
