using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0002DoNotNestTryCatchInTryBlockAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0002";

    private static readonly LocalizableString Title = "Do not hide fallback flow in broad nested try/catch blocks";
    private static readonly LocalizableString MessageFormat = "Extract broad nested try/catch fallback flow from the surrounding try block";
    private static readonly LocalizableString Description =
        "Broad nested try/catch blocks inside another try block make degraded fallback paths harder to follow. " +
        "Extract the inner operation into a helper or flatten the exception handling.";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        MeridianDiagnosticCategories.Readability,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeTryStatement, Microsoft.CodeAnalysis.CSharp.SyntaxKind.TryStatement);
    }

    private static void AnalyzeTryStatement(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not TryStatementSyntax tryStatement)
        {
            return;
        }

        if (tryStatement.Catches.Count == 0)
        {
            return;
        }

        if (!IsNestedInsideAnotherTryBlock(tryStatement))
        {
            return;
        }

        if (!HasBroadCatchThatContinuesControlFlow(tryStatement))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, tryStatement.TryKeyword.GetLocation()));
    }

    private static bool IsNestedInsideAnotherTryBlock(TryStatementSyntax tryStatement)
    {
        foreach (var ancestor in tryStatement.Ancestors())
        {
            if (ancestor is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)
            {
                return false;
            }

            if (ancestor is TryStatementSyntax outerTry)
            {
                return outerTry.Block.DescendantNodes().Contains(tryStatement);
            }
        }

        return false;
    }

    private static bool HasBroadCatchThatContinuesControlFlow(TryStatementSyntax tryStatement)
    {
        foreach (var catchClause in tryStatement.Catches)
        {
            if (!IsBroadCatchClause(catchClause))
            {
                continue;
            }

            if (CatchTerminatesControlFlow(catchClause.Block))
            {
                continue;
            }

            if (IsLogOnlyCatchBlock(catchClause.Block))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsBroadCatchClause(CatchClauseSyntax catchClause)
    {
        if (catchClause.Declaration is null)
        {
            return true;
        }

        return catchClause.Declaration.Type switch
        {
            IdentifierNameSyntax { Identifier.ValueText: "Exception" } => true,
            QualifiedNameSyntax
            {
                Left: IdentifierNameSyntax { Identifier.ValueText: "System" },
                Right: IdentifierNameSyntax { Identifier.ValueText: "Exception" }
            } => true,
            _ => false
        };
    }

    private static bool CatchTerminatesControlFlow(BlockSyntax catchBlock)
    {
        return catchBlock.Statements.LastOrDefault() switch
        {
            ReturnStatementSyntax => true,
            ThrowStatementSyntax => true,
            _ => false
        };
    }

    private static bool IsLogOnlyCatchBlock(BlockSyntax catchBlock)
    {
        return catchBlock.Statements.Count > 0 &&
               catchBlock.Statements.All(IsLoggingStatement);
    }

    private static bool IsLoggingStatement(StatementSyntax statement)
    {
        if (statement is not ExpressionStatementSyntax
            {
                Expression: InvocationExpressionSyntax
                {
                    Expression: var invocationTarget
                }
            })
        {
            return false;
        }

        return GetInvocationName(invocationTarget) is
            "Debug" or
            "LogDebug" or
            "Information" or
            "LogInformation" or
            "Warning" or
            "LogWarning" or
            "Error" or
            "LogError" or
            "Fatal" or
            "LogTrace" or
            "Trace";
    }

    private static string? GetInvocationName(ExpressionSyntax invocationTarget)
    {
        return invocationTarget switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            _ => null
        };
    }
}
