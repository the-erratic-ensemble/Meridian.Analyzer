using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0003DoNotCombineOutputCacheWithSensitiveEndpointMetadataAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0003";

    private static readonly LocalizableString Title = "Do not cache tenant, entitlement, quota, or policy-sensitive endpoints";
    private static readonly LocalizableString MessageFormat = "Remove [OutputCache] from endpoints with tenant, entitlement, quota, plan, or explicit policy metadata unless a reviewed persona-safe policy exists";
    private static readonly LocalizableString Description =
        "Output-cache hits skip action execution and can bypass Meridian entitlement, quota, tenant, plan, or persona-sensitive checks. " +
        "Use no-store response caching or a reviewed persona-safe cache policy instead.";

    private static readonly string[] SensitiveAttributeNames =
    {
        "ConsumeFeature",
        "RequireNonAnonymousAccess",
        "RequirePlan",
        "RequireStepUpVerification",
        "TenantScoped"
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
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDeclaration)
        {
            return;
        }

        var outputCacheAttribute = MeridianAnalyzerSyntaxHelpers
            .GetAttributes(classDeclaration, "OutputCache")
            .FirstOrDefault();
        if (outputCacheAttribute is null)
        {
            return;
        }

        if (!HasSensitiveEndpointMetadata(classDeclaration) &&
            !classDeclaration.Members
                .OfType<MethodDeclarationSyntax>()
                .Any(HasSensitiveEndpointMetadata))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, outputCacheAttribute.GetLocation()));
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodDeclaration)
        {
            return;
        }

        if (methodDeclaration.Parent is not ClassDeclarationSyntax classDeclaration)
        {
            return;
        }

        var methodOutputCacheAttribute = MeridianAnalyzerSyntaxHelpers
            .GetAttributes(methodDeclaration, "OutputCache")
            .FirstOrDefault();
        if (methodOutputCacheAttribute is null)
        {
            return;
        }

        var methodHasSensitiveMetadata = HasSensitiveEndpointMetadata(methodDeclaration);
        var classHasSensitiveMetadata = HasSensitiveEndpointMetadata(classDeclaration);
        if (!methodHasSensitiveMetadata && !classHasSensitiveMetadata)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, methodOutputCacheAttribute.GetLocation()));
    }

    private static bool HasSensitiveEndpointMetadata(MemberDeclarationSyntax member)
    {
        if (MeridianAnalyzerSyntaxHelpers.HasAttribute(member, SensitiveAttributeNames))
        {
            return true;
        }

        return MeridianAnalyzerSyntaxHelpers
            .GetAttributes(member, "Authorize")
            .Any(HasExplicitPolicy);
    }

    private static bool HasExplicitPolicy(AttributeSyntax authorizeAttribute)
    {
        return authorizeAttribute.ArgumentList?.Arguments.Any(argument =>
            argument.NameEquals?.Name.Identifier.ValueText == "Policy" ||
            argument.NameColon?.Name.Identifier.ValueText == "Policy" ||
            argument.NameEquals is null && argument.NameColon is null) == true;
    }
}
