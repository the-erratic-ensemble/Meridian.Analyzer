using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0010UseClockAndTimeProviderBoundariesAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0010";

    private static readonly LocalizableString Title = "Use Meridian clock or TimeProvider boundaries";
    private static readonly LocalizableString MessageFormat = "Production runtime code should use IMeridianClock or TimeProvider instead of direct system time, raw Task.Delay, or raw timers";
    private static readonly LocalizableString Description =
        "Direct time access and raw delays make request and background behaviour harder to test deterministically.";

    private static readonly string[] ApprovedBoundaryPathSegments =
    {
        "/MeridianClock.cs",
        "/Clock/",
        "/Time/",
        "/Migrations/",
        "/Serialization/"
    };

    private static readonly string[] PassiveModelPathSegments =
    {
        "/DTOs/",
        "/Entities/",
        "/Models/",
        "/Requests/",
        "/Responses/",
        "/ValueObjects/",
        "/Database/Entities/"
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
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MemberAccessExpressionSyntax memberAccess ||
            IsApprovedLocation(context.Node.SyntaxTree.FilePath) ||
            IsPassiveModelDefault(memberAccess))
        {
            return;
        }

        if (IsClockMember(memberAccess))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation()));
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation || IsApprovedLocation(context.Node.SyntaxTree.FilePath))
        {
            return;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            string.Equals(memberAccess.Expression.ToString(), "Task", StringComparison.Ordinal) &&
            string.Equals(memberAccess.Name.Identifier.ValueText, "Delay", StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        }
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ObjectCreationExpressionSyntax objectCreation || IsApprovedLocation(context.Node.SyntaxTree.FilePath))
        {
            return;
        }

        var typeName = objectCreation.Type.ToString();
        if (typeName is "Timer" or "System.Threading.Timer" or "PeriodicTimer" or "System.Threading.PeriodicTimer")
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, objectCreation.Type.GetLocation()));
        }
    }

    private static bool IsClockMember(MemberAccessExpressionSyntax memberAccess)
    {
        var receiver = memberAccess.Expression.ToString();
        var memberName = memberAccess.Name.Identifier.ValueText;

        return (receiver is "DateTime" or "System.DateTime" or "DateTimeOffset" or "System.DateTimeOffset") &&
               memberName is "UtcNow" or "Now";
    }

    private static bool IsApprovedLocation(string filePath)
    {
        return MeridianAnalyzerRuleHelpers.IsTestPath(filePath) ||
               MeridianAnalyzerSyntaxHelpers.PathContainsAny(filePath, ApprovedBoundaryPathSegments);
    }

    private static bool IsPassiveModelDefault(SyntaxNode node)
    {
        if (!MeridianAnalyzerSyntaxHelpers.PathContainsAny(node.SyntaxTree.FilePath, PassiveModelPathSegments))
        {
            return false;
        }

        return node.Ancestors()
            .OfType<EqualsValueClauseSyntax>()
            .Any(IsPropertyOrFieldInitializer);
    }

    private static bool IsPropertyOrFieldInitializer(EqualsValueClauseSyntax initializer)
    {
        return initializer.Parent is PropertyDeclarationSyntax or VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax };
    }
}
