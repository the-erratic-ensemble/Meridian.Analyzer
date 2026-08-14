using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0051AvoidAsyncVoidCallbacksAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0051";

    private static readonly LocalizableString Title = "Keep asynchronous callbacks awaitable";

    private static readonly LocalizableString MessageFormat =
        "Use a task-returning delegate instead of an async void callback";

    private static readonly LocalizableString Description =
        "Async anonymous functions converted to void delegates cannot expose completion or failures to their callers.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Reliability,
        DiagnosticSeverity.Warning,
        true,
        Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeAnonymousFunction,
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.AnonymousMethodExpression);
    }

    private static void AnalyzeAnonymousFunction(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not AnonymousFunctionExpressionSyntax anonymousFunction ||
            MeridianAnalyzerRuleHelpers.IsTestPath(anonymousFunction.SyntaxTree.FilePath) ||
            !anonymousFunction.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword) ||
            context.SemanticModel.GetTypeInfo(anonymousFunction, context.CancellationToken).ConvertedType is not
            INamedTypeSymbol delegateType ||
            delegateType.DelegateInvokeMethod?.ReturnsVoid != true ||
            IsDirectEventSubscription(context, anonymousFunction))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, anonymousFunction.GetLocation()));
    }

    private static bool IsDirectEventSubscription(
        SyntaxNodeAnalysisContext context,
        AnonymousFunctionExpressionSyntax anonymousFunction)
    {
        return anonymousFunction.Ancestors()
            .OfType<AssignmentExpressionSyntax>()
            .Any(assignment =>
                assignment.IsKind(SyntaxKind.AddAssignmentExpression) &&
                assignment.Right.Span.Contains(anonymousFunction.Span) &&
                context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is IEventSymbol);
    }
}
