using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Meridian.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MER0001DoNotUseConditionalExpressionsInInitializersAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MER0001";

    private static readonly LocalizableString Title = "Do not branch payload construction directly in initializer members";
    private static readonly LocalizableString MessageFormat = "Stage payload-construction branches before object or anonymous-object initializers";
    private static readonly LocalizableString Description =
        "Conditional expressions that branch into object or anonymous-object payload construction hide branching inside initializer members. " +
        "Stage the branch in a named local or helper before building the initializer.";

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
        context.RegisterSyntaxNodeAction(AnalyzeConditionalExpression, SyntaxKind.ConditionalExpression);
    }

    private static void AnalyzeConditionalExpression(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ConditionalExpressionSyntax conditionalExpression)
        {
            return;
        }

        if (!IsDirectAnonymousObjectMemberExpression(conditionalExpression) &&
            !IsDirectObjectInitializerAssignmentExpression(conditionalExpression))
        {
            return;
        }

        if (!HasPayloadConstructionBranch(conditionalExpression))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, conditionalExpression.GetLocation()));
    }

    private static bool IsDirectAnonymousObjectMemberExpression(ConditionalExpressionSyntax conditionalExpression)
    {
        return conditionalExpression.Parent is AnonymousObjectMemberDeclaratorSyntax anonymousMember &&
               anonymousMember.Expression == conditionalExpression;
    }

    private static bool IsDirectObjectInitializerAssignmentExpression(ConditionalExpressionSyntax conditionalExpression)
    {
        return conditionalExpression.Parent is AssignmentExpressionSyntax { Right: { } right } assignment &&
               assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
               !IsElementStyleAssignment(assignment) &&
               right == conditionalExpression &&
               assignment.Parent is InitializerExpressionSyntax initializer &&
               initializer.IsKind(SyntaxKind.ObjectInitializerExpression);
    }

    private static bool IsElementStyleAssignment(AssignmentExpressionSyntax assignment)
    {
        return assignment.Left is ImplicitElementAccessSyntax or ElementAccessExpressionSyntax;
    }

    private static bool HasPayloadConstructionBranch(ConditionalExpressionSyntax conditionalExpression)
    {
        return IsPayloadConstructionExpression(conditionalExpression.WhenTrue) ||
               IsPayloadConstructionExpression(conditionalExpression.WhenFalse);
    }

    private static bool IsPayloadConstructionExpression(ExpressionSyntax expression)
    {
        return expression switch
        {
            AnonymousObjectCreationExpressionSyntax => true,
            ObjectCreationExpressionSyntax => true,
            ImplicitObjectCreationExpressionSyntax => true,
            _ => false
        };
    }
}
