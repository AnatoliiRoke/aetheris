using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aetheris.Analyzers.Tests;

/// <summary>
/// Tests for <see cref="MagicNumberAnalyzer"/> and the <c>AETHERIS001</c> rule.
/// </summary>
public sealed class MagicNumberAnalyzerTests
{
    /// <summary>
    /// The analyzer under test.
    /// </summary>
    private static readonly DiagnosticAnalyzer Analyzer = new MagicNumberAnalyzer();

    /// <summary>
    /// A number that carries a rule on its own has to be reported.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task A_balance_value_in_an_arithmetic_expression_is_reported()
    {
        // Arrange
        const string source = """
            public class Calculator
            {
                public int Scale(int input)
                {
                    return input * 160;
                }
            }
            """;

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, Analyzer);

        // Assert
        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(MagicNumberAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("160", diagnostic.GetMessage());
    }

    /// <summary>
    /// The values that carry no meaning on their own have to stay silent.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task The_neutral_values_are_ignored()
    {
        // Arrange
        const string source = """
            public class Calculator
            {
                public int Index(int[] values)
                {
                    int first = values[0];
                    int last = values[values.Length - 1];
                    return first + last + 1;
                }
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    /// A number that is part of a name is not a rule.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task A_number_inside_a_name_is_ignored()
    {
        // Arrange
        const string source = """
            public class Encoding
            {
                public string Encode(byte[] data)
                {
                    return System.Convert.ToBase64String(data);
                }
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    /// Enum members and their arguments belong to the enum, not to the rule.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task Enum_members_and_their_arguments_are_ignored()
    {
        // Arrange
        const string source = """
            public enum Level
            {
                None = 0,
                Low = 10,
                High = 20,
            }

            public class Settings
            {
                public Level Default => Level.Low;

                public Level Custom() => (Level)25;
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    /// An attribute argument is a declaration, not a rule.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task Attribute_arguments_are_ignored()
    {
        // Arrange
        const string source = """
            using System;

            [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
            public sealed class CooldownAttribute : Attribute
            {
                public CooldownAttribute(int seconds)
                {
                    Seconds = seconds;
                }

                public int Seconds { get; }
            }

            [Cooldown(300)]
            public class Weapon
            {
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    /// A bit mask or a shift count is part of the operation.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task Bit_masks_and_shift_counts_are_ignored()
    {
        // Arrange
        const string source = """
            public class Bits
            {
                public int Read(int value)
                {
                    int masked = value & 0xFF;
                    int shifted = value << 4;
                    int flagged = value | 0x10;
                    int inverted = ~value;
                    return masked + shifted + flagged + inverted;
                }
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    /// A number that defines a collection size is not a rule.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task Collection_sizes_are_ignored()
    {
        // Arrange
        const string source = """
            using System.Collections.Generic;

            public class Buffers
            {
                public byte[] Create()
                {
                    return new byte[256];
                }

                public List<int> Grow(int count)
                {
                    return new List<int>(count);
                }

                public string Take(string source, int length)
                {
                    return source[..length];
                }
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    /// A size that is passed to a method with a size parameter is not a rule.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task A_size_passed_to_a_size_parameter_is_ignored()
    {
        // Arrange
        const string source = """
            public class Validation
            {
                public bool IsValid(string code)
                {
                    return Check(code, 64);
                }

                public bool Check(string value, int maximumLength)
                {
                    return value.Length <= maximumLength;
                }
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    /// A buffer that is sized from a length is not a rule.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task A_buffer_sized_from_a_length_is_ignored()
    {
        // Arrange
        const string source = """
            using System.Text;

            public class Buffers
            {
                public StringBuilder Create(string name)
                {
                    return new StringBuilder(name.Length + 8);
                }
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    /// The name of a parameter that describes a size has to be recognized even when the
    /// method is an extension method of a fluent interface.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task A_size_passed_to_a_fluent_extension_method_is_ignored()
    {
        // Arrange
        const string source = """
            public static class Rules
            {
                public static RuleBuilder Limit(this RuleBuilder builder, int maxLength)
                {
                    return builder;
                }
            }

            public class RuleBuilder
            {
            }

            public class Validation
            {
                public RuleBuilder Build()
                {
                    return new RuleBuilder().Limit(64);
                }
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    /// A number that is compared with a named constant belongs to the constant.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task Numbers_compared_with_a_named_constant_are_ignored()
    {
        // Arrange
        const string source = """
            public class Limits
            {
                public const int MaxLevel = 50;

                public bool IsAllowed(int level)
                {
                    return level <= MaxLevel;
                }

                public bool IsTooHigh(int level)
                {
                    const int SoftLimit = 10;
                    return level > SoftLimit;
                }
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    /// The declaration of a named constant is not reported, otherwise no constant
    /// could ever be introduced.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task A_constant_declaration_is_not_reported()
    {
        // Arrange
        const string source = """
            public class Calculator
            {
                public const int ScaleMultiplier = 160;
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    /// The initializer of a constant can be a compound expression, and its numbers
    /// belong to the constant as well.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task A_compound_constant_declaration_is_not_reported()
    {
        // Arrange
        const string source = """
            public class Limits
            {
                public const int MaximumMessageSize = 64 * 1024;

                private const int ReloadIntervalSeconds = 30;
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    /// The rule has to be reported for every occurrence in a method body.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task Every_magic_number_in_a_method_body_is_reported()
    {
        // Arrange
        const string source = """
            public class Calculator
            {
                public int Compute(int input)
                {
                    int first = input * 160;
                    int second = input * 90;
                    return first - second;
                }
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Equal(
            new[] { MagicNumberAnalyzer.DiagnosticId, MagicNumberAnalyzer.DiagnosticId },
            ids);
    }

    /// <summary>
    /// The rule has to be enabled by default and to be a warning, so that the build
    /// reports it without breaking the compilation.
    /// </summary>
    [Fact]
    public void The_rule_is_enabled_by_default_with_warning_severity()
    {
        // Act
        DiagnosticDescriptor descriptor = Assert.Single(Analyzer.SupportedDiagnostics);

        // Assert
        Assert.Equal(MagicNumberAnalyzer.DiagnosticId, descriptor.Id);
        Assert.Equal("Aetheris.Balance", descriptor.Category);
        Assert.Equal(DiagnosticSeverity.Warning, descriptor.DefaultSeverity);
        Assert.True(descriptor.IsEnabledByDefault);
    }
}
