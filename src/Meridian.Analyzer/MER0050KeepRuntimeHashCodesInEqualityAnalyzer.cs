using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0050KeepRuntimeHashCodesInEqualityAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0050";

    private static readonly LocalizableString Title = "Keep runtime hash codes inside equality hashing";

    private static readonly LocalizableString MessageFormat =
        "Use a stable hash for persisted, deterministic, or cross-process values";

    private static readonly LocalizableString Description =
        "Runtime hash codes are implementation and process dependent; use them only for equality infrastructure.";

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
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            MeridianAnalyzerRuleHelpers.IsTestPath(invocation.SyntaxTree.FilePath) ||
            !IsRuntimeHashInvocation(context, invocation) ||
            IsEqualityHashMethod(context, invocation))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsRuntimeHashInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
            return false;

        if (method.Name == "GetHashCode" && !method.IsStatic && method.Parameters.Length == 0)
            return true;

        if (method.Name == "GetHashCode" &&
            method.Parameters.Length == 1 &&
            MeridianAnalyzerSemanticHelpers.IsTypeOrDerivedFrom(
                method.ContainingType,
                "System",
                "StringComparer"))
            return true;

        return method.Name == "Combine" &&
               method.IsStatic &&
               method.ContainingType is
               {
                   Name: "HashCode",
                   ContainingNamespace: { } containingNamespace
               } &&
               containingNamespace.ToDisplayString() == "System";
    }

    private static bool IsEqualityHashMethod(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var methodDeclaration = invocation.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (methodDeclaration is null ||
            context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol method ||
            method.Name != "GetHashCode")
            return false;

        if (IsObjectGetHashCodeOverride(method))
            return true;

        if (method.ExplicitInterfaceImplementations.Any(IsEqualityComparerGetHashCode))
            return true;

        var containingType = method.ContainingType;
        return containingType?.AllInterfaces
                   .Where(IsEqualityComparerType)
                   .SelectMany(interfaceType => interfaceType.GetMembers("GetHashCode").OfType<IMethodSymbol>())
                   .Any(interfaceMethod =>
                       SymbolEqualityComparer.Default.Equals(
                           containingType.FindImplementationForInterfaceMember(interfaceMethod),
                           method)) == true;
    }

    private static bool IsObjectGetHashCodeOverride(IMethodSymbol method)
    {
        for (var overridden = method.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
        {
            if (overridden.Name == "GetHashCode" &&
                overridden.Parameters.Length == 0 &&
                overridden.ContainingType?.SpecialType == SpecialType.System_Object)
                return true;
        }

        return false;
    }

    private static bool IsEqualityComparerGetHashCode(IMethodSymbol method)
    {
        return method.Name == "GetHashCode" &&
               method.Parameters.Length == 1 &&
               IsEqualityComparerType(method.ContainingType);
    }

    private static bool IsEqualityComparerType(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.Name == "IEqualityComparer" &&
               namedType.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic" &&
               namedType.TypeArguments.Length == 1;
    }
}
