using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0011AvoidStaticMutableControllerStateAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0011";

    private static readonly LocalizableString Title = "Avoid static mutable state in controllers and auth handlers";
    private static readonly LocalizableString MessageFormat = "Move static mutable controller/auth state into an injectable bounded service";
    private static readonly LocalizableString Description =
        "Static mutable state hides lifecycle, eviction, test-isolation, and tenant-scope policy. Controllers and auth handlers should use explicit services for caches, log throttles, and timers.";

    private static readonly string[] MutableTypeNames =
    {
        "ConcurrentDictionary",
        "Dictionary",
        "HashSet",
        "List",
        "MemoryCache",
        "Timer",
        "PeriodicTimer"
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
        context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
    }

    private static void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not FieldDeclarationSyntax fieldDeclaration ||
            !MeridianAnalyzerRuleHelpers.HasModifier(fieldDeclaration.Modifiers, SyntaxKind.StaticKeyword))
        {
            return;
        }

        var containingClass = MeridianAnalyzerRuleHelpers.GetContainingClass(fieldDeclaration);
        if (containingClass is null || !IsSensitiveRuntimeType(containingClass))
        {
            return;
        }

        var typeName = fieldDeclaration.Declaration.Type.ToString();
        if (!MutableTypeNames.Any(name => typeName.Contains(name, StringComparison.Ordinal)))
        {
            return;
        }

        var variable = fieldDeclaration.Declaration.Variables.FirstOrDefault();
        context.ReportDiagnostic(Diagnostic.Create(Rule, (variable ?? (SyntaxNode)fieldDeclaration).GetLocation()));
    }

    private static bool IsSensitiveRuntimeType(ClassDeclarationSyntax classDeclaration)
    {
        var className = classDeclaration.Identifier.ValueText;
        return MeridianAnalyzerSyntaxHelpers.IsControllerClass(classDeclaration) ||
               className.EndsWith("AuthenticationHandler", StringComparison.Ordinal) ||
               className.EndsWith("AuthHandler", StringComparison.Ordinal) ||
               className.EndsWith("SessionHandler", StringComparison.Ordinal);
    }
}
