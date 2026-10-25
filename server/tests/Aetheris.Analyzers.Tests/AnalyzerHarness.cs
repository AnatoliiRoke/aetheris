using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aetheris.Analyzers.Tests;

/// <summary>
/// Compiles a C# snippet in memory and runs the analyzers over it.
/// </summary>
/// <remarks>
/// The official <c>Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.XUnit</c> package still
/// targets Roslyn 1.0 and xunit 2, so the tests drive
/// <see cref="CompilationWithAnalyzers"/> directly instead.
/// </remarks>
internal static class AnalyzerHarness
{
    /// <summary>
    /// The metadata references used to compile the test snippets.
    /// </summary>
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    /// <summary>
    /// Compiles the snippet and returns the diagnostics reported by the given analyzers.
    /// </summary>
    /// <param name="source">The C# source to compile.</param>
    /// <param name="analyzers">The analyzers to run.</param>
    /// <returns>The diagnostics reported by the analyzers.</returns>
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source,
        params DiagnosticAnalyzer[] analyzers)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest));

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerTestAssembly",
            syntaxTrees: [tree],
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // The snippet has to compile, otherwise the analyzer would be tested against broken code.
        ImmutableArray<Diagnostic> compileErrors = compilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        if (!compileErrors.IsEmpty)
        {
            string details = string.Join(
                Environment.NewLine,
                compileErrors.Select(diagnostic => diagnostic.ToString()));

            throw new InvalidOperationException(
                $"The test snippet does not compile:{Environment.NewLine}{details}");
        }

        CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers(
            analyzers.ToImmutableArray(),
            new CompilationWithAnalyzersOptions(
                options: new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                onAnalyzerException: null,
                concurrentAnalysis: true,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: false));

        return await withAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }

    /// <summary>
    /// Compiles the snippet and returns the identifiers of the reported diagnostics.
    /// </summary>
    /// <param name="source">The C# source to compile.</param>
    /// <param name="analyzers">The analyzers to run.</param>
    /// <returns>The identifiers of the reported diagnostics, in source order.</returns>
    public static async Task<string[]> GetDiagnosticIdsAsync(
        string source,
        params DiagnosticAnalyzer[] analyzers)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source, analyzers);

        return diagnostics
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .Select(diagnostic => diagnostic.Id)
            .ToArray();
    }

    /// <summary>
    /// Builds the metadata references of the runtime and of the test framework.
    /// </summary>
    /// <returns>The metadata references.</returns>
    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        List<MetadataReference> references = [];

        // The trusted platform assemblies of the running runtime contain everything
        // a snippet may need: System.Runtime, System.Linq, System.Collections and so on.
        string? trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;

        if (trustedAssemblies is not null)
        {
            foreach (string path in trustedAssemblies.Split(Path.PathSeparator))
            {
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                }
            }
        }

        if (references.Count == 0)
        {
            // Fall back to the assemblies loaded into the current process.
            references.AddRange(
                AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    .Select(assembly => MetadataReference.CreateFromFile(assembly.Location)));
        }

        return references.ToImmutableArray();
    }
}
