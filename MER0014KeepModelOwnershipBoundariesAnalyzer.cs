using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0014KeepModelOwnershipBoundariesAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0014";

    private static readonly LocalizableString Title = "Keep backend model ownership boundaries";
    private static readonly LocalizableString MessageFormat = "Review this model placement against Meridian DTO/entity ownership conventions";
    private static readonly LocalizableString Description =
        "HTTP DTOs should stay feature-local, persistence entities should stay in the database entity boundary, and Core models should not carry EF persistence attributes.";

    private static readonly string[] EntityApprovedPathSegments =
    {
        "/Database/Entities/",
        "/Migrations/"
    };

    private static readonly string[] PersistenceAttributeNames =
    {
        "Column",
        "ForeignKey",
        "Index",
        "Key",
        "NotMapped",
        "Table"
    };

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Architecture,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDeclaration ||
            MeridianAnalyzerRuleHelpers.IsTestPath(classDeclaration.SyntaxTree.FilePath))
        {
            return;
        }

        var filePath = classDeclaration.SyntaxTree.FilePath;
        var className = classDeclaration.Identifier.ValueText;

        if (className.EndsWith("Entity", StringComparison.Ordinal) &&
            !MeridianAnalyzerSyntaxHelpers.PathContainsAny(filePath, EntityApprovedPathSegments))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, classDeclaration.Identifier.GetLocation()));
            return;
        }

        if (IsHttpContractName(className) && IsSharedOrCoreProject(filePath))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, classDeclaration.Identifier.GetLocation()));
            return;
        }

        if (MeridianAnalyzerSyntaxHelpers.PathContains(filePath, "/Meridian.Core/") &&
            HasPersistenceAttribute(classDeclaration))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, classDeclaration.Identifier.GetLocation()));
        }
    }

    private static bool IsHttpContractName(string className)
    {
        return className.EndsWith("Request", StringComparison.Ordinal) ||
               className.EndsWith("Response", StringComparison.Ordinal) ||
               className.EndsWith("Dto", StringComparison.Ordinal) ||
               className.EndsWith("DTO", StringComparison.Ordinal);
    }

    private static bool IsSharedOrCoreProject(string filePath)
    {
        return MeridianAnalyzerSyntaxHelpers.PathContains(filePath, "/Meridian.Shared/") ||
               MeridianAnalyzerSyntaxHelpers.PathContains(filePath, "/Meridian.Core/");
    }

    private static bool HasPersistenceAttribute(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.AttributeLists
            .SelectMany(attributeList => attributeList.Attributes)
            .Any(attribute => MeridianAnalyzerSyntaxHelpers.IsAttributeNamed(attribute, PersistenceAttributeNames));
    }
}
