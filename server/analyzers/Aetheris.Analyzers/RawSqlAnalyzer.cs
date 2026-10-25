using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aetheris.Analyzers;

/// <summary>
/// Reports SQL text that is built at run time and passed to a method that runs it as is.
/// </summary>
/// <remarks>
/// <para>
/// Every query of the server is parameterized (task 00002, section 2.4). The methods that
/// take raw SQL text must therefore get a compile-time constant: a literal, a raw string
/// literal or a constant. Values travel as parameters, never inside the text. Concatenation,
/// interpolation and any other text built at run time are errors.
/// </para>
/// <para>
/// The methods that take a <c>FormattableString</c>, such as <c>ExecuteSqlAsync</c> or
/// <c>FromSql</c>, turn every interpolated value into a parameter and are not reported.
/// </para>
/// <para>Checked methods:</para>
/// <list type="bullet">
/// <item><description>Entity Framework Core: <c>ExecuteSqlRaw</c>, <c>ExecuteSqlRawAsync</c>, <c>FromSqlRaw</c>, <c>SqlQueryRaw</c>.</description></item>
/// <item><description>Dapper: every <c>SqlMapper</c> method that takes the SQL text.</description></item>
/// <item><description>ADO.NET: the <c>CommandText</c> property and the command constructors.</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RawSqlAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic identifier, used in the suppression files and in the editor.
    /// </summary>
    public const string DiagnosticId = "AETHERIS002";

    /// <summary>
    /// The diagnostic category.
    /// </summary>
    private const string Category = "Aetheris.Security";

    /// <summary>
    /// The name of the SQL text parameter of the checked methods.
    /// </summary>
    private const string SqlParameterName = "sql";

    /// <summary>
    /// The Entity Framework Core methods that run raw SQL text.
    /// </summary>
    private static readonly ImmutableHashSet<string> EntityFrameworkMethods =
        ImmutableHashSet.Create("ExecuteSqlRaw", "ExecuteSqlRawAsync", "FromSqlRaw", "SqlQueryRaw");

    /// <summary>
    /// The diagnostic descriptor.
    /// </summary>
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "SQL text built at run time",
        messageFormat: "The SQL text passed to '{0}' must be a constant; pass the values as parameters",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "SQL text that is concatenated or interpolated at run time opens the door to SQL " +
            "injection. Use a constant text with parameters, or a method that takes a " +
            "FormattableString and turns the interpolated values into parameters.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeCreation, SyntaxKind.ObjectCreationExpression, SyntaxKind.ImplicitObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        IMethodSymbol original = method.ReducedFrom ?? method;

        if (!IsEntityFrameworkRawMethod(original) && !IsDapperMethod(original))
        {
            return;
        }

        ExpressionSyntax? sqlArgument = FindSqlArgument(context, invocation.ArgumentList, method);

        if (sqlArgument is not null && !IsConstant(context, sqlArgument))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, sqlArgument.GetLocation(), method.Name));
        }
    }

    private static void AnalyzeCreation(SyntaxNodeAnalysisContext context)
    {
        BaseObjectCreationExpressionSyntax creation = (BaseObjectCreationExpressionSyntax)context.Node;

        if (creation.ArgumentList is null || creation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !IsDbCommand(constructor.ContainingType))
        {
            return;
        }

        // The first parameter of a command constructor is the command text.
        IParameterSymbol? first = constructor.Parameters.FirstOrDefault();

        if (first is null || first.Type.SpecialType != SpecialType.System_String)
        {
            return;
        }

        ExpressionSyntax text = creation.ArgumentList.Arguments[0].Expression;

        if (!IsConstant(context, text))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, text.GetLocation(), constructor.ContainingType.Name));
        }
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        AssignmentExpressionSyntax assignment = (AssignmentExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IPropertySymbol property
            || property.Name != "CommandText"
            || !IsDbCommand(property.ContainingType))
        {
            return;
        }

        if (!IsConstant(context, assignment.Right))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.Right.GetLocation(), "CommandText"));
        }
    }

    private static bool IsEntityFrameworkRawMethod(IMethodSymbol method) =>
        EntityFrameworkMethods.Contains(method.Name)
        && method.ContainingNamespace?.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore", System.StringComparison.Ordinal) == true;

    private static bool IsDapperMethod(IMethodSymbol method) =>
        method.ContainingType?.Name == "SqlMapper"
        && method.ContainingNamespace?.ToDisplayString() == "Dapper";

    private static bool IsDbCommand(INamedTypeSymbol? type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == "DbCommand" && current.ContainingNamespace?.ToDisplayString() == "System.Data.Common")
            {
                return true;
            }
        }

        return type?.AllInterfaces.Any(contract =>
            contract.Name == "IDbCommand" && contract.ContainingNamespace?.ToDisplayString() == "System.Data") == true;
    }

    /// <summary>
    /// Finds the argument that holds the SQL text: the argument named <c>sql</c>, or the
    /// argument in the position of the <c>sql</c> parameter.
    /// </summary>
    private static ExpressionSyntax? FindSqlArgument(
        SyntaxNodeAnalysisContext context,
        ArgumentListSyntax argumentList,
        IMethodSymbol method)
    {
        foreach (ArgumentSyntax argument in argumentList.Arguments)
        {
            if (argument.NameColon?.Name.Identifier.ValueText == SqlParameterName)
            {
                return argument.Expression;
            }
        }

        int index = -1;

        for (int position = 0; position < method.Parameters.Length; position++)
        {
            if (method.Parameters[position].Name == SqlParameterName)
            {
                index = position;
                break;
            }
        }

        if (index < 0 || index >= argumentList.Arguments.Count)
        {
            return null;
        }

        ArgumentSyntax positional = argumentList.Arguments[index];

        // The SQL text of these methods is a string; a FormattableString overload is safe.
        ITypeSymbol? type = context.SemanticModel.GetTypeInfo(positional.Expression, context.CancellationToken).ConvertedType;

        return type?.SpecialType == SpecialType.System_String ? positional.Expression : null;
    }

    private static bool IsConstant(SyntaxNodeAnalysisContext context, ExpressionSyntax expression) =>
        context.SemanticModel.GetConstantValue(expression, context.CancellationToken).HasValue;
}
