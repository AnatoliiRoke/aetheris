using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aetheris.Analyzers;

/// <summary>
/// Reports numeric literals that are used directly in code instead of being named.
/// </summary>
/// <remarks>
/// <para>
/// The balance of the game lives in the database (documentation/game-design/14),
/// so a bare number in a rule is almost always a mistake: it cannot be changed without a
/// release and it cannot be reviewed as a design decision.
/// </para>
/// <para>
/// The analyzer ignores values that carry no meaning on their own:
/// <list type="bullet">
/// <item><description><c>-1</c>, <c>0</c> and <c>1</c>, which are used as indices, counts and flags.</description></item>
/// <item><description>Numbers that are part of a name, such as <c>Base64</c> or <c>Md5</c>.</description></item>
/// <item><description>Enum members and their arguments.</description></item>
/// <item><description>Attribute arguments.</description></item>
/// <item><description>Bit masks and shift counts, where the number is part of the operation.</description></item>
/// <item><description>Numbers that define a collection size.</description></item>
/// <item><description>Numbers compared against a named constant, where the constant owns the value.</description></item>
/// </list>
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MagicNumberAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic identifier, used in the suppression files and in the editor.
    /// </summary>
    public const string DiagnosticId = "AETHERIS001";

    /// <summary>
    /// The diagnostic category.
    /// </summary>
    private const string Category = "Aetheris.Balance";

    /// <summary>
    /// The diagnostic severity.
    /// </summary>
    private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

    /// <summary>
    /// The values that never carry a meaning on their own.
    /// </summary>
    private static readonly ImmutableHashSet<string> IgnoredValues =
        ImmutableHashSet.Create("-1", "0", "1");

    /// <summary>
    /// The parameter names that describe a size rather than a rule.
    /// </summary>
    private static readonly ImmutableHashSet<string> SizeParameterNames =
        ImmutableHashSet.Create(
            "bufferSize",
            "capacity",
            "count",
            "initialCapacity",
            "length",
            "maxLength",
            "maximumLength",
            "minLength",
            "minimumLength",
            "size",
            "sizeHint");

    /// <summary>
    /// The diagnostic descriptor.
    /// </summary>
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Magic number",
        messageFormat: "Number '{0}' should be a named constant or a configuration parameter",
        category: Category,
        defaultSeverity: Severity,
        isEnabledByDefault: true,
        description:
            "Balance values live in the database so that they can be changed without a new " +
            "release. A bare number in the code cannot be changed and cannot be reviewed as " +
            "a design decision. Use a named constant or a configuration parameter instead.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeLiteral, SyntaxKind.NumericLiteralExpression);
    }

    private static void AnalyzeLiteral(SyntaxNodeAnalysisContext context)
    {
        LiteralExpressionSyntax literal = (LiteralExpressionSyntax)context.Node;

        if (literal.Token.Value is not int and not long and not double and not decimal and not float)
        {
            return;
        }

        string text = literal.Token.ValueText;

        if (IgnoredValues.Contains(text))
        {
            return;
        }

        if (IsPartOfAName(literal)
            || IsEnumValue(context, literal)
            || IsConstantDeclaration(context, literal)
            || IsAttributeArgument(literal)
            || IsBitMaskOrShift(literal)
            || IsCollectionSize(context, literal)
            || IsComparedWithAConstant(context, literal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, literal.GetLocation(), text));
    }

    /// <summary>
    /// Checks whether the literal is part of an identifier, for example <c>Base64</c>.
    /// </summary>
    /// <param name="literal">The literal to check.</param>
    /// <returns><see langword="true"/> when the literal belongs to a name.</returns>
    private static bool IsPartOfAName(LiteralExpressionSyntax literal)
    {
        return literal.Parent is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Name.Identifier.ValueText.IndexOf(literal.Token.ValueText, System.StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    /// Checks whether the literal belongs to an enum, either as a member value or as
    /// an argument of the enum constructor.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="literal">The literal to check.</param>
    /// <returns><see langword="true"/> when the literal belongs to an enum.</returns>
    private static bool IsEnumValue(SyntaxNodeAnalysisContext context, LiteralExpressionSyntax literal)
    {
        // The value of an enum member is declared by the member itself, so the symbol
        // has to be looked up on the member declaration and not on the literal.
        if (literal.Parent is EqualsValueClauseSyntax { Parent: EnumMemberDeclarationSyntax member })
        {
            IFieldSymbol? field = context.SemanticModel.GetDeclaredSymbol(member);

            if (field is { IsConst: true } && field.ContainingType?.TypeKind == TypeKind.Enum)
            {
                return true;
            }
        }

        // An argument of the enum constructor, for example (Level)25.
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(literal);

        if (symbolInfo.Symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            && constructor.ContainingType?.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        // A cast of a literal to an enum type.
        if (literal.Parent is CastExpressionSyntax cast
            && context.SemanticModel.GetTypeInfo(cast).Type?.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether the literal is the value of a named constant, either a field
    /// or a local. Such a value is named, so it is not a magic number.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="literal">The literal to check.</param>
    /// <returns><see langword="true"/> when the literal declares a constant.</returns>
    private static bool IsConstantDeclaration(SyntaxNodeAnalysisContext context, LiteralExpressionSyntax literal)
    {
        // The initializer of a constant can be a compound expression, for example
        // 64 * 1024, so the literal is searched from the top of the expression.
        for (SyntaxNode? node = literal; node is not null; node = node.Parent)
        {
            if (node is not EqualsValueClauseSyntax equalsValue)
            {
                continue;
            }

            switch (equalsValue.Parent)
            {
                case VariableDeclaratorSyntax variable:
                    ISymbol? declared = context.SemanticModel.GetDeclaredSymbol(variable);

                    // A constant field and a constant local are both named constants.
                    if (declared is IFieldSymbol { IsConst: true }
                        or ILocalSymbol { IsConst: true })
                    {
                        return true;
                    }

                    break;

                case EnumMemberDeclarationSyntax:
                    // Handled by IsEnumValue.
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether the literal is an attribute argument.
    /// </summary>
    /// <param name="literal">The literal to check.</param>
    /// <returns><see langword="true"/> when the literal is an attribute argument.</returns>
    private static bool IsAttributeArgument(LiteralExpressionSyntax literal)
    {
        for (SyntaxNode? node = literal.Parent; node is not null; node = node.Parent)
        {
            if (node is AttributeSyntax)
            {
                return true;
            }

            if (node is StatementSyntax or MemberDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether the literal is a bit mask or a shift count.
    /// </summary>
    /// <param name="literal">The literal to check.</param>
    /// <returns><see langword="true"/> when the literal is part of a bitwise operation.</returns>
    private static bool IsBitMaskOrShift(LiteralExpressionSyntax literal)
    {
        if (literal.Parent is BinaryExpressionSyntax binary)
        {
            return binary.Kind() is SyntaxKind.BitwiseAndExpression
                or SyntaxKind.BitwiseOrExpression
                or SyntaxKind.ExclusiveOrExpression
                or SyntaxKind.LeftShiftExpression
                or SyntaxKind.RightShiftExpression
                or SyntaxKind.UnsignedRightShiftExpression;
        }

        return literal.Parent is PrefixUnaryExpressionSyntax prefix
            && prefix.IsKind(SyntaxKind.BitwiseNotExpression);
    }

    /// <summary>
    /// Checks whether the literal defines a collection size.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="literal">The literal to check.</param>
    /// <returns><see langword="true"/> when the literal is a size.</returns>
    private static bool IsCollectionSize(SyntaxNodeAnalysisContext context, LiteralExpressionSyntax literal)
    {
        // The size of an array is a child of the rank specifier, for example new byte[256].
        if (literal.Parent is ArrayRankSpecifierSyntax rank
            && rank.Sizes.Any(size => size == literal))
        {
            return true;
        }

        // A sized array creation, for example new byte[256].
        if (literal.Parent is ArrayCreationExpressionSyntax arrayCreation)
        {
            ArrayRankSpecifierSyntax? firstRank = arrayCreation.Type.RankSpecifiers.FirstOrDefault();

            if (firstRank?.Sizes.Any(size => size == literal) == true)
            {
                return true;
            }
        }

        if (literal.Parent is ArgumentSyntax argument)
        {
            if (argument.NameColon is not null)
            {
                return SizeParameterNames.Contains(argument.NameColon.Name.Identifier.ValueText);
            }

            IMethodSymbol? method = ResolveTargetMethod(context, argument);

            if (method is null)
            {
                return false;
            }

            int index = GetArgumentIndex(argument);

            if (index < 0 || index >= method.Parameters.Length)
            {
                return false;
            }

            return SizeParameterNames.Contains(method.Parameters[index].Name);
        }

        // A size computed from a length, for example new StringBuilder(name.Length + 8).
        if (literal.Parent is BinaryExpressionSyntax sizeExpression
            && sizeExpression.Kind() is SyntaxKind.AddExpression
            && IsLengthExpression(sizeExpression.Left))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether the expression reads the length or the count of a collection.
    /// </summary>
    /// <param name="expression">The expression to check.</param>
    /// <returns><see langword="true"/> when the expression is a length.</returns>
    private static bool IsLengthExpression(ExpressionSyntax? expression)
    {
        if (expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        return memberAccess.Name.Identifier.ValueText is "Length" or "Count";
    }

    /// <summary>
    /// Resolves the method that the argument is passed to.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="argument">The argument to resolve.</param>
    /// <returns>The target method, or <see langword="null"/> when it cannot be resolved.</returns>
    private static IMethodSymbol? ResolveTargetMethod(SyntaxNodeAnalysisContext context, ArgumentSyntax argument)
    {
        // A constructor is resolved on the creation expression, not on the argument.
        if (argument.Parent?.Parent is ObjectCreationExpressionSyntax creation)
        {
            return context.SemanticModel.GetSymbolInfo(creation).Symbol as IMethodSymbol;
        }

        if (argument.Parent?.Parent is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            return context.SemanticModel.GetSymbolInfo(implicitCreation).Symbol as IMethodSymbol;
        }

        // A regular invocation, including a fluent call of an extension method.
        if (argument.Parent?.Parent is InvocationExpressionSyntax invocation)
        {
            SymbolInfo invocationSymbol = context.SemanticModel.GetSymbolInfo(invocation);

            if (invocationSymbol.Symbol is IMethodSymbol method)
            {
                return method;
            }

            // An extension method that was reduced to its instance parameter is not
            // reported by the symbol of the invocation, so the candidate symbols are
            // used instead.
            return invocationSymbol.CandidateSymbols
                .OfType<IMethodSymbol>()
                .FirstOrDefault(candidate => candidate.MethodKind == MethodKind.Ordinary);
        }

        return context.SemanticModel.GetSymbolInfo(argument).Symbol as IMethodSymbol;
    }

    /// <summary>
    /// Gets the position of the argument inside its argument list.
    /// </summary>
    /// <param name="argument">The argument to locate.</param>
    /// <returns>The zero based index of the argument.</returns>
    private static int GetArgumentIndex(ArgumentSyntax argument)
    {
        if (argument.Parent is not ArgumentListSyntax argumentList)
        {
            return -1;
        }

        int index = 0;

        foreach (ArgumentSyntax sibling in argumentList.Arguments)
        {
            if (sibling == argument)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    /// <summary>
    /// Checks whether the literal is compared against a named constant, which means the
    /// number belongs to the constant rather than to the code.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="literal">The literal to check.</param>
    /// <returns><see langword="true"/> when the literal is compared with a constant.</returns>
    private static bool IsComparedWithAConstant(SyntaxNodeAnalysisContext context, LiteralExpressionSyntax literal)
    {
        if (literal.Parent is not BinaryExpressionSyntax binary)
        {
            return false;
        }

        if (binary.Kind() is not (SyntaxKind.EqualsExpression
            or SyntaxKind.NotEqualsExpression
            or SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression))
        {
            return false;
        }

        SyntaxNode other = binary.Left == literal ? binary.Right : binary.Left;

        if (other is not IdentifierNameSyntax identifier)
        {
            return false;
        }

        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(identifier);

        return symbolInfo.Symbol is IFieldSymbol { IsConst: true }
            or ILocalSymbol { IsConst: true };
    }
}
