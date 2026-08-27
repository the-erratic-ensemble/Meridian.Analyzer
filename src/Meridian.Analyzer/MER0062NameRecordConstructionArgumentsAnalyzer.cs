using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0062NameRecordConstructionArgumentsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0062";

    private const int MinimumArgumentCount = 3;

    private static readonly LocalizableString Title = "Name record construction arguments";

    private static readonly LocalizableString MessageFormat =
        "Use named arguments when constructing record '{0}'";

    private static readonly LocalizableString Description =
        "Record construction should name each argument so values remain tied to their declared members.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Readability,
        DiagnosticSeverity.Warning,
        true,
        Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeObjectCreation,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not BaseObjectCreationExpressionSyntax objectCreation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(objectCreation.SyntaxTree.FilePath) ||
            objectCreation.ArgumentList is not { } argumentList ||
            argumentList.Arguments.Count < MinimumArgumentCount ||
            argumentList.Arguments.All(argument => argument.NameColon is not null) ||
            context.SemanticModel.GetSymbolInfo(
                objectCreation,
                context.CancellationToken).Symbol is not IMethodSymbol constructor ||
            constructor.ContainingType is not INamedTypeSymbol containingType ||
            !containingType.IsRecord ||
            containingType.IsValueType)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            objectCreation.GetLocation(),
            containingType.Name));
    }
}
