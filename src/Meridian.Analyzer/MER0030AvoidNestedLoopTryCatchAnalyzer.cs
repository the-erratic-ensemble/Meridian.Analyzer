using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0030AvoidNestedLoopTryCatchAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0030";

    private static readonly LocalizableString Title = "Avoid broad per-iteration try/catch inside nested loop exception flow";
    private static readonly LocalizableString MessageFormat =
        "Extract this broad per-iteration try/catch from the enclosing while loop and outer try flow";
    private static readonly LocalizableString Description =
        "A broad per-iteration try/catch inside a while loop that already sits inside outer try flow hides which failures end the loop, " +
        "which failures are logged and skipped, and which failures escape. Extract the iteration work into a helper or result-returning boundary.";

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

        if (!HasBroadCatchThatContinuesControlFlow(tryStatement))
        {
            return;
        }

        if (!TryGetEnclosingWhileStatement(tryStatement, out var enclosingWhile))
        {
            return;
        }

        if (!HasEnclosingTryOutsideWhile(enclosingWhile))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, tryStatement.TryKeyword.GetLocation()));
    }

    private static bool TryGetEnclosingWhileStatement(
        TryStatementSyntax tryStatement,
        out WhileStatementSyntax whileStatement)
    {
        foreach (var ancestor in tryStatement.Ancestors())
        {
            if (ancestor is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)
            {
                break;
            }

            if (ancestor is WhileStatementSyntax candidate)
            {
                whileStatement = candidate;
                return true;
            }
        }

        whileStatement = null!;
        return false;
    }

    private static bool HasEnclosingTryOutsideWhile(WhileStatementSyntax whileStatement)
    {
        foreach (var ancestor in whileStatement.Ancestors())
        {
            if (ancestor is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)
            {
                return false;
            }

            if (ancestor is TryStatementSyntax)
            {
                return true;
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
}
