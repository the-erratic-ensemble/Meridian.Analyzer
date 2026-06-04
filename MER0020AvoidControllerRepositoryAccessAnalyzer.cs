using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0020AvoidControllerRepositoryAccessAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0020";

    private static readonly LocalizableString Title = "Keep controller actions out of repository and DbContext details";
    private static readonly LocalizableString MessageFormat = "Controller actions should delegate repository/DbContext work to a service or facade boundary";
    private static readonly LocalizableString Description =
        "Controllers should stay HTTP-focused. Direct repository, DbContext, or EF query work in action bodies grows coupling and bypasses feature-service contracts.";

    private static readonly string[] EfMethodNames =
    {
        "Add",
        "AddAsync",
        "Entry",
        "Find",
        "FindAsync",
        "Remove",
        "SaveChanges",
        "SaveChangesAsync",
        "Set",
        "ToArrayAsync",
        "ToDictionaryAsync",
        "ToHashSetAsync",
        "ToListAsync",
        "Update"
    };

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

        var containingMethod = MeridianAnalyzerRuleHelpers.GetContainingMethod(invocation);
        if (containingMethod is null || !MeridianAnalyzerRuleHelpers.IsControllerAction(containingMethod))
        {
            return;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var memberName = memberAccess.Name.Identifier.ValueText;
        if (LooksLikeRepositoryReceiver(context, memberAccess.Expression) ||
            LooksLikeDbContextReceiver(context, memberAccess.Expression) && IsEfMethod(memberName))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        }
    }

    private static bool IsEfMethod(string memberName)
    {
        return EfMethodNames.Any(name => string.Equals(memberName, name, StringComparison.Ordinal));
    }

    private static bool LooksLikeRepositoryReceiver(SyntaxNodeAnalysisContext context, ExpressionSyntax receiver)
    {
        return ContainsRepository(GetReceiverSymbolName(context, receiver)) ||
               ContainsRepository(GetReceiverType(context, receiver)?.Name);
    }

    private static bool LooksLikeDbContextReceiver(SyntaxNodeAnalysisContext context, ExpressionSyntax receiver)
    {
        var receiverName = GetReceiverSymbolName(context, receiver);
        var receiverType = GetReceiverType(context, receiver);
        var receiverText = receiver.ToString();

        return IsDbContextName(receiverName) ||
               IsDbContextType(receiverType) ||
               receiverText.StartsWith("_db.", StringComparison.OrdinalIgnoreCase) ||
               receiverText.StartsWith("_dbContext.", StringComparison.OrdinalIgnoreCase) ||
               receiverText.StartsWith("dbContext.", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetReceiverSymbolName(SyntaxNodeAnalysisContext context, ExpressionSyntax receiver)
    {
        return context.SemanticModel.GetSymbolInfo(receiver, context.CancellationToken).Symbol?.Name;
    }

    private static ITypeSymbol? GetReceiverType(SyntaxNodeAnalysisContext context, ExpressionSyntax receiver)
    {
        return context.SemanticModel.GetTypeInfo(receiver, context.CancellationToken).Type;
    }

    private static bool ContainsRepository(string? value)
    {
        return value?.IndexOf("repository", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsDbContextName(string? value)
    {
        return value is not null &&
               (string.Equals(value, "_db", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "db", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "_dbContext", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "dbContext", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith("DbContext", StringComparison.Ordinal));
    }

    private static bool IsDbContextType(ITypeSymbol? typeSymbol)
    {
        for (var current = typeSymbol; current is not null; current = current.BaseType)
        {
            if (current is INamedTypeSymbol namedType &&
                (string.Equals(namedType.Name, "DbContext", StringComparison.Ordinal) ||
                 namedType.Name.EndsWith("DbContext", StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }
}
