using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0006DoNotResolveServicesInsideControllerActionsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0006";

    private static readonly LocalizableString Title = "Do not use service location inside controller actions";
    private static readonly LocalizableString MessageFormat = "Use constructor injection or [FromServices] method injection instead of resolving services from RequestServices inside controller actions";
    private static readonly LocalizableString Description =
        "Controller action service location hides dependencies from action signatures and weakens startup validation. " +
        "Use constructor injection for controller-wide collaborators or [FromServices] for action-specific collaborators.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Architecture,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (!IsServiceResolutionInvocation(invocation, context))
        {
            return;
        }

        if (!IsInsideControllerAction(invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsServiceResolutionInvocation(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var methodName = MeridianAnalyzerSyntaxHelpers.GetSimpleName(memberAccess.Name);
        if (methodName is not "GetService" and not "GetRequiredService")
        {
            return false;
        }

        var targetType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
        if (IsServiceProviderType(targetType))
        {
            return true;
        }

        var targetSymbolType = GetSymbolType(context.SemanticModel.GetSymbolInfo(memberAccess.Expression, context.CancellationToken).Symbol);
        if (IsServiceProviderType(targetSymbolType))
        {
            return true;
        }

        if (targetType is not null && targetType.TypeKind != TypeKind.Error)
        {
            return false;
        }

        var targetText = memberAccess.Expression.ToString();
        return targetText.IndexOf("RequestServices", StringComparison.Ordinal) >= 0 ||
               targetText.IndexOf("ServiceProvider", StringComparison.Ordinal) >= 0 ||
               targetText.IndexOf("serviceProvider", StringComparison.Ordinal) >= 0 ||
               targetText.IndexOf("_serviceProvider", StringComparison.Ordinal) >= 0 ||
               string.Equals(targetText, "provider", StringComparison.Ordinal);
    }

    private static ITypeSymbol? GetSymbolType(ISymbol? symbol)
    {
        return symbol switch
        {
            IFieldSymbol field => field.Type,
            ILocalSymbol local => local.Type,
            IParameterSymbol parameter => parameter.Type,
            IPropertySymbol property => property.Type,
            _ => null
        };
    }

    private static bool IsServiceProviderType(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        return IsSystemServiceProvider(namedType) ||
               namedType.AllInterfaces.Any(IsSystemServiceProvider);
    }

    private static bool IsSystemServiceProvider(INamedTypeSymbol type)
    {
        return type.Name == "IServiceProvider" &&
               type.ContainingNamespace.ToDisplayString() == "System";
    }

    private static bool IsInsideControllerAction(SyntaxNode node)
    {
        var methodDeclaration = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration is null || !MeridianAnalyzerSyntaxHelpers.HasHttpMethodAttribute(methodDeclaration))
        {
            return false;
        }

        return methodDeclaration.Parent is ClassDeclarationSyntax classDeclaration &&
               MeridianAnalyzerSyntaxHelpers.IsControllerClass(classDeclaration);
    }
}
