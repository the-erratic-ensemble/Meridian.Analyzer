using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0005EnforceAdminControllerShapeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0005";

    private static readonly LocalizableString Title = "Keep a consistent admin controller shape";
    private static readonly LocalizableString MessageFormat = "Admin controller surfaces must use the Admin*Controller name, api/admin route, and AdminControllerBase inheritance";
    private static readonly LocalizableString Description =
        "Admin routes are a higher-risk surface. Controllers under the admin surface should be named Admin*Controller, inherit AdminControllerBase, and expose api/admin routes.";

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
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDeclaration)
        {
            return;
        }

        var declarations = GetDeclarations(context, classDeclaration).ToArray();
        if (!declarations.Any(MeridianAnalyzerSyntaxHelpers.IsControllerClass))
        {
            return;
        }

        if (!IsFirstDeclaration(classDeclaration, declarations))
        {
            return;
        }

        if (!IsAdminSurface(declarations))
        {
            return;
        }

        if (declarations.Any(declaration => declaration.Modifiers.Any(SyntaxKind.AbstractKeyword)))
        {
            return;
        }

        if (HasAdminShape(declarations))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, classDeclaration.Identifier.GetLocation()));
    }

    private static IReadOnlyCollection<ClassDeclarationSyntax> GetDeclarations(
        SyntaxNodeAnalysisContext context,
        ClassDeclarationSyntax classDeclaration)
    {
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken);
        if (symbol is null)
        {
            return new[] { classDeclaration };
        }

        var declarations = symbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(context.CancellationToken))
            .OfType<ClassDeclarationSyntax>()
            .ToArray();

        return declarations.Length == 0
            ? new[] { classDeclaration }
            : declarations;
    }

    private static bool IsFirstDeclaration(
        ClassDeclarationSyntax classDeclaration,
        IReadOnlyCollection<ClassDeclarationSyntax> declarations)
    {
        var firstDeclaration = declarations
            .OrderBy(declaration => declaration.SyntaxTree.FilePath, StringComparer.Ordinal)
            .ThenBy(declaration => declaration.SpanStart)
            .First();

        return firstDeclaration.SyntaxTree.FilePath == classDeclaration.SyntaxTree.FilePath &&
               firstDeclaration.SpanStart == classDeclaration.SpanStart;
    }

    private static bool IsAdminSurface(IReadOnlyCollection<ClassDeclarationSyntax> declarations)
    {
        return declarations.Any(declaration =>
            MeridianAnalyzerSyntaxHelpers.PathContains(declaration.SyntaxTree.FilePath, "/Features/Admin/Controllers/") ||
            declaration.Identifier.ValueText.StartsWith("Admin", StringComparison.Ordinal) ||
            GetRouteTemplates(declaration).Any(route => route.StartsWith("api/admin", StringComparison.Ordinal)));
    }

    private static bool HasAdminShape(IReadOnlyCollection<ClassDeclarationSyntax> declarations)
    {
        return declarations.Any(declaration => declaration.Identifier.ValueText.StartsWith("Admin", StringComparison.Ordinal)) &&
               declarations.Any(declaration => MeridianAnalyzerSyntaxHelpers.InheritsFrom(declaration, "AdminControllerBase")) &&
               declarations.Any(declaration => GetRouteTemplates(declaration).Any(route => route.StartsWith("api/admin", StringComparison.Ordinal)));
    }

    private static IEnumerable<string> GetRouteTemplates(MemberDeclarationSyntax member)
    {
        return MeridianAnalyzerSyntaxHelpers
            .GetAttributes(member, "Route")
            .Select(MeridianAnalyzerSyntaxHelpers.GetFirstStringArgument)
            .Where(route => route is not null)
            .Select(route => route!);
    }
}
