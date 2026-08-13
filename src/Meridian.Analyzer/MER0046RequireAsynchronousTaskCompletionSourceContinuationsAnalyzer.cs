using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0046RequireAsynchronousTaskCompletionSourceContinuationsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0046";

    private static readonly LocalizableString Title = "Run TaskCompletionSource continuations asynchronously";

    private static readonly LocalizableString MessageFormat =
        "Create this TaskCompletionSource with RunContinuationsAsynchronously";

    private static readonly LocalizableString Description =
        "TaskCompletionSource continuations should not run inline on the thread that completes the source.";

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
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ObjectCreationExpressionSyntax objectCreation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(objectCreation.SyntaxTree.FilePath) ||
            context.SemanticModel.GetTypeInfo(
                objectCreation,
                context.CancellationToken).Type is not INamedTypeSymbol type ||
            !IsTaskCompletionSource(type) ||
            HasAsynchronousContinuationsOption(context, objectCreation))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, objectCreation.GetLocation()));
    }

    private static bool IsTaskCompletionSource(INamedTypeSymbol type)
    {
        return string.Equals(type.Name, "TaskCompletionSource", StringComparison.Ordinal) &&
               string.Equals(type.ContainingNamespace?.ToDisplayString(), "System.Threading.Tasks",
                   StringComparison.Ordinal);
    }

    private static bool HasAsynchronousContinuationsOption(
        SyntaxNodeAnalysisContext context,
        ObjectCreationExpressionSyntax objectCreation)
    {
        return objectCreation.ArgumentList?.Arguments.Any(argument =>
        {
            var argumentType = context.SemanticModel.GetTypeInfo(
                argument.Expression,
                context.CancellationToken).ConvertedType;
            return IsTaskCreationOptions(argumentType) &&
                   ContainsRunContinuationsAsynchronously(context, argument.Expression);
        }) == true;
    }

    private static bool IsTaskCreationOptions(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.TypeKind == TypeKind.Enum &&
               string.Equals(namedType.Name, "TaskCreationOptions", StringComparison.Ordinal) &&
               string.Equals(namedType.ContainingNamespace?.ToDisplayString(), "System.Threading.Tasks",
                   StringComparison.Ordinal);
    }

    private static bool ContainsRunContinuationsAsynchronously(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression)
    {
        var constantValue = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);
        if (constantValue.HasValue &&
            TryGetInt64(constantValue.Value, out var constant) &&
            TryGetRunContinuationsValue(context, expression, out var flag))
            return (constant & flag) == flag;

        return expression.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess =>
            {
                var symbol = context.SemanticModel.GetSymbolInfo(
                    memberAccess,
                    context.CancellationToken).Symbol;
                return symbol is IFieldSymbol field &&
                       IsRunContinuationsField(field);
            });
    }

    private static bool TryGetRunContinuationsValue(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression,
        out long value)
    {
        value = 0;
        foreach (var memberAccess in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
        {
            if (context.SemanticModel.GetSymbolInfo(
                    memberAccess,
                    context.CancellationToken).Symbol is IFieldSymbol field &&
                IsRunContinuationsField(field) &&
                TryGetInt64(field.ConstantValue, out value))
                return true;
        }

        var taskCreationOptions = context.SemanticModel.Compilation.GetTypeByMetadataName(
            "System.Threading.Tasks.TaskCreationOptions");
        var flagField = taskCreationOptions?.GetMembers("RunContinuationsAsynchronously")
            .OfType<IFieldSymbol>()
            .FirstOrDefault();
        return flagField is not null && TryGetInt64(flagField.ConstantValue, out value);
    }

    private static bool IsRunContinuationsField(IFieldSymbol field)
    {
        return field.IsConst &&
               string.Equals(field.Name, "RunContinuationsAsynchronously", StringComparison.Ordinal) &&
               IsTaskCreationOptions(field.ContainingType);
    }

    private static bool TryGetInt64(object? value, out long result)
    {
        if (value is null)
        {
            result = 0;
            return false;
        }

        try
        {
            result = Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch (FormatException)
        {
            result = 0;
            return false;
        }
        catch (InvalidCastException)
        {
            result = 0;
            return false;
        }
        catch (OverflowException)
        {
            result = 0;
            return false;
        }
    }
}
