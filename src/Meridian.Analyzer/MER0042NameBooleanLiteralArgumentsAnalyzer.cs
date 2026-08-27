using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0042NameBooleanLiteralArgumentsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0042";

    private static readonly LocalizableString Title = "Name boolean literal arguments";

    private static readonly LocalizableString MessageFormat =
        "Name this boolean argument '{0}' at the call site";

    private static readonly LocalizableString Description =
        "Boolean literals should show the target parameter name when positional syntax would hide their meaning.";

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
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(invocation.SyntaxTree.FilePath) ||
            context.SemanticModel.GetSymbolInfo(
                invocation,
                context.CancellationToken).Symbol is not IMethodSymbol method)
            return;

        AnalyzeArguments(context, invocation.ArgumentList.Arguments, method.Parameters);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ObjectCreationExpressionSyntax objectCreation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(objectCreation.SyntaxTree.FilePath) ||
            context.SemanticModel.GetSymbolInfo(
                objectCreation,
                context.CancellationToken).Symbol is not IMethodSymbol constructor ||
            objectCreation.ArgumentList is not { } argumentList)
            return;

        AnalyzeArguments(context, argumentList.Arguments, constructor.Parameters);
    }

    private static void AnalyzeArguments(
        SyntaxNodeAnalysisContext context,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        ImmutableArray<IParameterSymbol> parameters)
    {
        var positionalIndex = 0;
        foreach (var argument in arguments)
        {
            IParameterSymbol? parameter;
            if (argument.NameColon is { } nameColon)
            {
                parameter = parameters.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, nameColon.Name.Identifier.ValueText,
                        StringComparison.Ordinal));
                if (parameter is not null)
                    positionalIndex = Math.Max(positionalIndex, parameter.Ordinal + 1);
            }
            else
            {
                parameter = GetPositionalParameter(parameters, positionalIndex++);
            }

            if (argument.NameColon is not null ||
                parameter?.Type.SpecialType != SpecialType.System_Boolean ||
                argument.Expression is not LiteralExpressionSyntax literal ||
                !IsBooleanLiteral(literal))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                literal.GetLocation(),
                parameter.Name));
        }
    }

    private static IParameterSymbol? GetPositionalParameter(
        ImmutableArray<IParameterSymbol> parameters,
        int positionalIndex)
    {
        if (positionalIndex < parameters.Length)
            return parameters[positionalIndex];

        return parameters.Length > 0 && parameters[parameters.Length - 1].IsParams
            ? parameters[parameters.Length - 1]
            : null;
    }

    private static bool IsBooleanLiteral(LiteralExpressionSyntax literal)
    {
        return literal.IsKind(SyntaxKind.TrueLiteralExpression) ||
               literal.IsKind(SyntaxKind.FalseLiteralExpression);
    }
}
