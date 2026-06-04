using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0004RequireExplicitControllerAuthorizationPolicyAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0004";

    private static readonly LocalizableString Title = "Declare explicit authorization policy on high-risk controller surfaces";
    private static readonly LocalizableString MessageFormat = "Declare an explicit authorization policy on admin or high-risk controller actions instead of relying only on inherited [Authorize]";
    private static readonly LocalizableString Description =
        "Base controller authorization proves authentication, not the required Meridian policy boundary. " +
        "Admin and high-risk tenant/report/support/search/subscription/analytics controllers need explicit policy metadata.";

    private static readonly string[] HighRiskFeaturePathSegments =
    {
        "/Features/Analytics/",
        "/Features/Reports/",
        "/Features/Search/",
        "/Features/Subscriptions/",
        "/Features/Support/",
        "/Features/Tenants/"
    };

    private static readonly string[] AllowAnonymousPathSegments =
    {
        "/Features/Anonymous/",
        "/Features/Authentication/",
        "/Features/Dev/",
        "/Features/Monitoring/",
        "/Features/Webhooks/",
        "/Infrastructure/Health/"
    };

    private static readonly string[] AnalyticsEventsAnonymousMethods =
    {
        "SubmitEvent",
        "SubmitEvents",
        "SubmitEventsDefault",
        "Health"
    };
    private const string AnalyticsEventsControllerName = "EventsController";
    private const string AnalyticsEventsControllerNamespace = "Meridian.Analytics.Controllers";

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
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol namedType ||
            namedType.TypeKind != TypeKind.Class)
        {
            return;
        }

        var classDeclarations = namedType.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(context.CancellationToken))
            .OfType<ClassDeclarationSyntax>()
            .ToArray();
        if (classDeclarations.Length == 0)
        {
            return;
        }

        if (!classDeclarations.Any(MeridianAnalyzerSyntaxHelpers.IsControllerClass))
        {
            return;
        }

        var unexpectedAllowAnonymousClass = classDeclarations
            .FirstOrDefault(declaration => HasUnexpectedAllowAnonymous(declaration, declaration.SyntaxTree.FilePath));
        if (unexpectedAllowAnonymousClass is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, unexpectedAllowAnonymousClass.Identifier.GetLocation()));
            return;
        }

        if (!IsPolicyRequiredController(classDeclarations))
        {
            return;
        }

        var actionMethods = classDeclarations
            .SelectMany(declaration => declaration.Members)
            .OfType<MethodDeclarationSyntax>()
            .Where(MeridianAnalyzerSyntaxHelpers.HasHttpMethodAttribute)
            .ToArray();
        if (actionMethods.Length == 0)
        {
            return;
        }

        if (actionMethods.Any(method => HasUnexpectedAllowAnonymous(method, method.SyntaxTree.FilePath)))
        {
            return;
        }

        if (classDeclarations.Any(HasAuthorizePolicy) || actionMethods.All(HasAuthorizePolicy))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, classDeclarations[0].Identifier.GetLocation()));
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodDeclaration)
        {
            return;
        }

        if (!MeridianAnalyzerSyntaxHelpers.HasHttpMethodAttribute(methodDeclaration))
        {
            return;
        }

        if (methodDeclaration.Parent is not ClassDeclarationSyntax classDeclaration)
        {
            return;
        }

        if (!MeridianAnalyzerSyntaxHelpers.IsControllerClass(classDeclaration))
        {
            return;
        }

        if (!HasUnexpectedAllowAnonymous(methodDeclaration, context.Node.SyntaxTree.FilePath))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation()));
    }

    private static bool IsPolicyRequiredController(IEnumerable<ClassDeclarationSyntax> classDeclarations)
    {
        return classDeclarations.Any(classDeclaration => {
            var filePath = classDeclaration.SyntaxTree.FilePath;
            return IsAdminController(classDeclaration, filePath) ||
                   MeridianAnalyzerSyntaxHelpers.PathContainsAny(filePath, HighRiskFeaturePathSegments) ||
                   GetRouteTemplates(classDeclaration).Any(IsHighRiskRoute);
        });
    }

    private static bool IsAdminController(ClassDeclarationSyntax classDeclaration, string filePath)
    {
        return classDeclaration.Identifier.ValueText.StartsWith("Admin", StringComparison.Ordinal) ||
               MeridianAnalyzerSyntaxHelpers.InheritsFrom(classDeclaration, "BaseAdminController") ||
               MeridianAnalyzerSyntaxHelpers.PathContains(filePath, "/Features/Admin/Controllers/") ||
               GetRouteTemplates(classDeclaration).Any(route => MeridianAnalyzerSyntaxHelpers.StartsWithOrdinal(route, "api/admin"));
    }

    private static bool IsHighRiskRoute(string route)
    {
        return route.StartsWith("api/reports", StringComparison.Ordinal) ||
               route.StartsWith("api/search", StringComparison.Ordinal) ||
               route.StartsWith("api/subscriptions", StringComparison.Ordinal) ||
               route.StartsWith("api/support", StringComparison.Ordinal) ||
               route.StartsWith("api/tenants", StringComparison.Ordinal) ||
               route.StartsWith("api/analytics", StringComparison.Ordinal);
    }

    private static bool HasAuthorizePolicy(MemberDeclarationSyntax member)
    {
        return MeridianAnalyzerSyntaxHelpers
            .GetAttributes(member, "Authorize")
            .Any(HasExplicitPolicyArgument);
    }

    private static bool HasExplicitPolicyArgument(AttributeSyntax attribute)
    {
        return attribute.ArgumentList?.Arguments.Any(argument =>
            argument.NameEquals?.Name.Identifier.ValueText == "Policy" ||
            argument.NameColon?.Name.Identifier.ValueText == "Policy" ||
            argument.NameEquals is null && argument.NameColon is null) == true;
    }

    private static bool HasUnexpectedAllowAnonymous(MemberDeclarationSyntax member, string filePath)
    {
        return MeridianAnalyzerSyntaxHelpers.HasAttribute(member, "AllowAnonymous") &&
               !IsApprovedAllowAnonymousMember(member, filePath);
    }

    private static bool IsApprovedAllowAnonymousMember(MemberDeclarationSyntax member, string filePath)
    {
        return MeridianAnalyzerSyntaxHelpers.PathContainsAny(filePath, AllowAnonymousPathSegments) ||
               IsApprovedAnalyticsEventsMethod(member);
    }

    private static bool IsApprovedAnalyticsEventsMethod(MemberDeclarationSyntax member)
    {
        return member is MethodDeclarationSyntax { Parent: ClassDeclarationSyntax classDeclaration } methodDeclaration &&
               IsClassInNamespace(classDeclaration, AnalyticsEventsControllerName, AnalyticsEventsControllerNamespace) &&
               AnalyticsEventsAnonymousMethods.Contains(methodDeclaration.Identifier.ValueText, StringComparer.Ordinal);
    }

    private static bool IsClassInNamespace(
        ClassDeclarationSyntax classDeclaration,
        string className,
        string namespaceName)
    {
        if (!string.Equals(classDeclaration.Identifier.ValueText, className, StringComparison.Ordinal))
        {
            return false;
        }

        var namespaceDeclaration = classDeclaration
            .Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        return string.Equals(namespaceDeclaration?.Name.ToString(), namespaceName, StringComparison.Ordinal);
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
