using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aetheris.Analyzers.Tests;

/// <summary>
/// Tests for <see cref="RawSqlAnalyzer"/> and the <c>AETHERIS002</c> rule.
/// </summary>
/// <remarks>
/// The snippets declare small stand-ins for the Entity Framework Core and Dapper methods
/// with the same names, namespaces and parameter names, so the tests do not need the real
/// packages.
/// </remarks>
public sealed class RawSqlAnalyzerTests
{
    /// <summary>
    /// The analyzer under test.
    /// </summary>
    private static readonly DiagnosticAnalyzer Analyzer = new RawSqlAnalyzer();

    /// <summary>
    /// The stand-ins of the database libraries.
    /// </summary>
    private const string Libraries = """
        namespace Microsoft.EntityFrameworkCore
        {
            public sealed class DatabaseFacade
            {
            }

            public static class RelationalDatabaseFacadeExtensions
            {
                public static int ExecuteSqlRaw(this DatabaseFacade database, string sql, params object[] parameters) => 0;

                public static int ExecuteSql(this DatabaseFacade database, System.FormattableString sql) => 0;
            }
        }

        namespace Dapper
        {
            public static class SqlMapper
            {
                public static int Execute(this System.Data.IDbConnection connection, string sql, object param = null) => 0;
            }
        }
        """;

    /// <summary>
    /// Concatenated SQL text passed to a raw method is reported.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task Concatenated_sql_is_reported()
    {
        // Arrange
        const string source = Libraries + """

            public class Store
            {
                public int Remove(Microsoft.EntityFrameworkCore.DatabaseFacade database, string name)
                {
                    return Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRaw(
                        database,
                        "DELETE FROM item WHERE name = '" + name + "'");
                }
            }
            """;

        // Act
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, Analyzer);

        // Assert
        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(RawSqlAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    /// <summary>
    /// Interpolated SQL text passed to a raw method of an extension call is reported.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task Interpolated_sql_in_an_extension_call_is_reported()
    {
        // Arrange
        const string source = Libraries + """

            namespace Snippet
            {
                using Microsoft.EntityFrameworkCore;

                public class Store
                {
                    public int Remove(DatabaseFacade database, string name)
                    {
                        return database.ExecuteSqlRaw($"DELETE FROM item WHERE name = '{name}'");
                    }
                }
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Equal([RawSqlAnalyzer.DiagnosticId], ids);
    }

    /// <summary>
    /// SQL text built at run time and passed to Dapper is reported.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task Built_sql_passed_to_dapper_is_reported()
    {
        // Arrange
        const string source = Libraries + """

            namespace Snippet
            {
                using Dapper;

                public class Store
                {
                    public int Remove(System.Data.IDbConnection connection, string table)
                    {
                        string sql = string.Concat("DELETE FROM ", table);
                        return connection.Execute(sql);
                    }
                }
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Equal([RawSqlAnalyzer.DiagnosticId], ids);
    }

    /// <summary>
    /// SQL text built at run time and assigned to a command is reported.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task Built_command_text_is_reported()
    {
        // Arrange
        const string source = """
            public class Store
            {
                public void Prepare(System.Data.Common.DbCommand command, string name)
                {
                    command.CommandText = "SELECT * FROM item WHERE name = '" + name + "'";
                }
            }
            """;

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Equal([RawSqlAnalyzer.DiagnosticId], ids);
    }

    /// <summary>
    /// Constant SQL text with parameters, and the methods that parameterize an interpolated
    /// string, are allowed.
    /// </summary>
    /// <returns>A task that completes when the assertion is done.</returns>
    [Fact]
    public async Task Constant_sql_and_parameterized_interpolation_are_allowed()
    {
        // Arrange
        const string source = Libraries + """"

            namespace Snippet
            {
                using Dapper;
                using Microsoft.EntityFrameworkCore;

                public class Store
                {
                    private const string RemoveByName = "DELETE FROM item WHERE name = @name";

                    public int Remove(DatabaseFacade database, System.Data.IDbConnection connection, string name)
                    {
                        int first = database.ExecuteSqlRaw("DELETE FROM item WHERE name = {0}", name);
                        int second = database.ExecuteSql($"DELETE FROM item WHERE name = {name}");
                        int third = connection.Execute(RemoveByName, new { name });
                        int fourth = connection.Execute(sql: """
                            DELETE FROM item
                            WHERE name = @name
                            """, param: new { name });

                        return first + second + third + fourth;
                    }
                }
            }
            """";

        // Act
        string[] ids = await AnalyzerHarness.GetDiagnosticIdsAsync(source, Analyzer);

        // Assert
        Assert.Empty(ids);
    }
}
