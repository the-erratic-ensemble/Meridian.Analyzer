using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0013RespectBackendLayerBoundariesAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0013";

    private static readonly LocalizableString Title = "Respect Meridian backend layer boundaries";
    private static readonly LocalizableString MessageFormat = "Remove this dependency because it crosses a documented Meridian backend layer boundary";
    private static readonly LocalizableString Description =
        "Meridian backend projects follow Clean Architecture dependency flow. " +
        "Core must not depend on API, Infrastructure, EF Core, or ASP.NET Core; Infrastructure must not depend on API; Shared must not depend on backend projects; Analytics must not use the standard MeridianDbContext/PostgreSQL repository boundary.";

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
        context.RegisterSyntaxNodeAction(AnalyzeCompilationUnit, SyntaxKind.CompilationUnit);
    }

    private static void AnalyzeCompilationUnit(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not CompilationUnitSyntax compilationUnit)
        {
            return;
        }

        var project = GetProjectFromPath(context.Node.SyntaxTree.FilePath);
        if (project is null)
        {
            return;
        }

        foreach (var usingDirective in compilationUnit.Usings)
        {
            var usingName = usingDirective.Name?.ToString();
            if (usingName is null)
            {
                continue;
            }

            if (IsForbiddenUsing(project.Value, usingName))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, usingDirective.GetLocation()));
            }
        }

        foreach (var qualifiedName in compilationUnit.DescendantNodes().OfType<QualifiedNameSyntax>())
        {
            if (qualifiedName.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax ||
                qualifiedName.FirstAncestorOrSelf<UsingDirectiveSyntax>() is not null)
            {
                continue;
            }

            var nameText = NormalizeQualifiedName(qualifiedName.ToString());
            if (IsForbiddenUsing(project.Value, nameText))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, qualifiedName.GetLocation()));
            }
        }

        if (project.Value == BackendProject.Analytics)
        {
            foreach (var identifier in compilationUnit.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (identifier.Identifier.ValueText == "MeridianDbContext" &&
                    identifier.FirstAncestorOrSelf<QualifiedNameSyntax>() is null &&
                    identifier.FirstAncestorOrSelf<AliasQualifiedNameSyntax>() is null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation()));
                }
            }
        }
    }

    private static BackendProject? GetProjectFromPath(string filePath)
    {
        if (MeridianAnalyzerSyntaxHelpers.PathContains(filePath, "/Meridian.Core/"))
        {
            return BackendProject.Core;
        }

        if (MeridianAnalyzerSyntaxHelpers.PathContains(filePath, "/Meridian.Infrastructure/"))
        {
            return BackendProject.Infrastructure;
        }

        if (MeridianAnalyzerSyntaxHelpers.PathContains(filePath, "/Meridian.Shared/"))
        {
            return BackendProject.Shared;
        }

        if (MeridianAnalyzerSyntaxHelpers.PathContains(filePath, "/Meridian.Analytics/"))
        {
            return BackendProject.Analytics;
        }

        return null;
    }

    private static bool IsForbiddenUsing(BackendProject project, string usingName)
    {
        return project switch
        {
            BackendProject.Core => StartsWithAny(
                usingName,
                "Meridian.API",
                "Meridian.Infrastructure",
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore"),
            BackendProject.Infrastructure => StartsWithAny(usingName, "Meridian.API"),
            BackendProject.Shared => StartsWithAny(
                usingName,
                "Meridian.API",
                "Meridian.Core",
                "Meridian.Infrastructure"),
            BackendProject.Analytics => StartsWithAny(usingName, "Meridian.Infrastructure.Database"),
            _ => false
        };
    }

    private static bool StartsWithAny(string value, params string[] prefixes)
    {
        return prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string NormalizeQualifiedName(string value)
    {
        const string GlobalAliasPrefix = "global::";
        return value.StartsWith(GlobalAliasPrefix, StringComparison.Ordinal)
            ? value.Substring(GlobalAliasPrefix.Length)
            : value;
    }

    private enum BackendProject
    {
        Analytics,
        Core,
        Infrastructure,
        Shared
    }
}
