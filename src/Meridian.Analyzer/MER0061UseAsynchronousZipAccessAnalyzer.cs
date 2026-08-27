using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0061UseAsynchronousZipAccessAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0061";

    private static readonly LocalizableString Title = "Use asynchronous ZIP access";

    private static readonly LocalizableString MessageFormat =
        "Use {0} for ZIP access inside asynchronous code";

    private static readonly LocalizableString Description =
        "File-backed ZIP access can block an asynchronous workflow when opened through synchronous APIs.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Performance,
        DiagnosticSeverity.Warning,
        true,
        Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(invocation.SyntaxTree.FilePath) ||
            !IsInsideAsyncCode(invocation) ||
            context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not
            IMethodSymbol method ||
            AsyncAlternative(method) is not { } alternative)
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), alternative));
    }

    private static string? AsyncAlternative(IMethodSymbol method)
    {
        if (method.Name == "OpenRead" &&
            method.Parameters.Length == 1 &&
            IsType(method.ContainingType, "System.IO.Compression", "ZipFile"))
            return "ZipFile.OpenReadAsync";

        return method.Name == "Open" &&
               method.Parameters.Length == 0 &&
               IsType(method.ContainingType, "System.IO.Compression", "ZipArchiveEntry")
            ? "ZipArchiveEntry.OpenAsync"
            : null;
    }

    private static bool IsInsideAsyncCode(InvocationExpressionSyntax invocation)
    {
        foreach (var ancestor in invocation.Ancestors())
        {
            if (ancestor is AnonymousFunctionExpressionSyntax anonymousFunction)
                return anonymousFunction.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);

            if (ancestor is LocalFunctionStatementSyntax localFunction)
                return MeridianAnalyzerRuleHelpers.HasModifier(
                    localFunction.Modifiers,
                    SyntaxKind.AsyncKeyword);

            if (ancestor is BaseMethodDeclarationSyntax method)
                return MeridianAnalyzerRuleHelpers.HasModifier(method.Modifiers, SyntaxKind.AsyncKeyword);
        }

        return false;
    }

    private static bool IsType(ITypeSymbol? type, string namespaceName, string typeName)
    {
        return type?.Name == typeName &&
               type.ContainingNamespace?.ToDisplayString() == namespaceName;
    }
}
