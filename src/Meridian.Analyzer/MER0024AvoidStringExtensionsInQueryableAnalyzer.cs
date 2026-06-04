using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0024AvoidStringExtensionsInQueryableAnalyzer : DiagnosticAnalyzer {
    public const string DiagnosticId = "MER0024";

    private static readonly LocalizableString Title = "Avoid shared string extension guards inside IQueryable predicates";
    private static readonly LocalizableString MessageFormat = "Replace {0} with a query-translatable guard for IQueryable filters";
    private static readonly LocalizableString Description =
        "StringExtensions.IsNullOrEmpty/IsNullOrWhiteSpace are in-memory helpers. Using them inside IQueryable/Expression predicates risks non-translatable EF queries.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Reliability,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context) {
        if (context.Node is not InvocationExpressionSyntax invocation) {
            return;
        }

        if (MeridianAnalyzerRuleHelpers.IsTestPath(invocation.SyntaxTree.FilePath)) {
            return;
        }

        if (!IsTargetStringExtensionInvocation(invocation)) {
            return;
        }

        if (!IsQueryableContext(invocation)) {
            return;
        }

        var invocationName = MeridianAnalyzerRuleHelpers.GetSimpleInvocationName(invocation);
        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), invocationName));
    }

    private static bool IsTargetStringExtensionInvocation(InvocationExpressionSyntax invocation) {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return false;
        }

        if (memberAccess.Name.Identifier.ValueText is not ("IsNullOrEmpty" or "IsNullOrWhiteSpace")) {
            return false;
        }

        if (memberAccess.Expression is IdentifierNameSyntax { Identifier.ValueText: "string" or "String" }) {
            return false;
        }

        if (memberAccess.Expression is MemberAccessExpressionSyntax qualifiedType &&
            string.Equals(qualifiedType.ToString(), "System.String", StringComparison.Ordinal)) {
            return false;
        }

        return true;
    }

    private static bool IsQueryableContext(InvocationExpressionSyntax invocation) {
        var method = MeridianAnalyzerRuleHelpers.GetContainingMethod(invocation);
        if (method is null) {
            return false;
        }

        var returnType = method.ReturnType.ToString();
        return returnType.Contains("IQueryable", StringComparison.Ordinal)
               || returnType.Contains("Expression<", StringComparison.Ordinal);
    }
}
