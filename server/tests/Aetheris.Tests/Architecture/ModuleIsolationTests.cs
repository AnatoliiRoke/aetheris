using System.Reflection;
using Aetheris.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Architecture;

/// <summary>
/// Tests that the game modules stay independent of each other.
/// </summary>
/// <remarks>
/// The rule from documentation/tech/01-arkhitektura.md, section 8: modules communicate only
/// through public interfaces and never reach into the internals of another module.
/// This is acceptance criterion 6 of task 00001.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ModuleIsolationTests
{
    private static readonly string[] ModuleNames =
    [
        "Aetheris.Economy",
        "Aetheris.Planets",
        "Aetheris.Fleet",
        "Aetheris.Market",
        "Aetheris.Rating",
        "Aetheris.Chat",
        "Aetheris.Admin",
        "Aetheris.World",
        "Aetheris.Requirements",
        "Aetheris.Research",
    ];

    /// <summary>
    /// The references between modules that are allowed: the Planets module reads the world
    /// through its contract and plugs into its extension points (task 00011), and it keeps the
    /// requirements of its own subjects in the common requirements module (task 00012). The
    /// dependencies go one way only.
    /// </summary>
    private static readonly Dictionary<string, string[]> AllowedReferences = new(StringComparer.Ordinal)
    {
        ["Aetheris.Planets"] = ["Aetheris.World", "Aetheris.Requirements"],
        ["Aetheris.Research"] = ["Aetheris.World", "Aetheris.Planets", "Aetheris.Requirements"],
    };

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleIsolationTests"/> class.
    /// </summary>
    /// <param name="factory">The test host factory, which builds the real host.</param>
    public ModuleIsolationTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// No module references another module's assembly, except the allowed one-way references.
    /// </summary>
    /// <param name="moduleName">The module under test.</param>
    [Theory]
    [InlineData("Aetheris.Economy")]
    [InlineData("Aetheris.Planets")]
    [InlineData("Aetheris.Fleet")]
    [InlineData("Aetheris.Market")]
    [InlineData("Aetheris.Rating")]
    [InlineData("Aetheris.Chat")]
    [InlineData("Aetheris.Admin")]
    [InlineData("Aetheris.World")]
    [InlineData("Aetheris.Requirements")]
    [InlineData("Aetheris.Research")]
    public void A_module_does_not_reference_another_module(string moduleName)
    {
        Assembly assembly = GetModuleAssembly(moduleName);

        List<string> foreignReferences = assembly
            .GetReferencedAssemblies()
            .Where(reference => ModuleNames.Contains(reference.Name ?? string.Empty))
            .Where(reference => reference.Name != moduleName)
            .Where(reference => !AllowedReferences.GetValueOrDefault(moduleName, []).Contains(reference.Name))
            .Select(reference => reference.Name ?? string.Empty)
            .ToList();

        Assert.True(
            foreignReferences.Count == 0,
            $"{moduleName} references other modules directly: {string.Join(", ", foreignReferences)}.");
    }

    /// <summary>
    /// Every module exposes exactly one public contract interface and keeps the rest internal.
    /// </summary>
    /// <param name="moduleName">The module under test.</param>
    [Theory]
    [InlineData("Aetheris.Economy")]
    [InlineData("Aetheris.Planets")]
    [InlineData("Aetheris.Fleet")]
    [InlineData("Aetheris.Market")]
    [InlineData("Aetheris.Rating")]
    [InlineData("Aetheris.Chat")]
    [InlineData("Aetheris.Admin")]
    [InlineData("Aetheris.World")]
    [InlineData("Aetheris.Requirements")]
    [InlineData("Aetheris.Research")]
    public void A_module_exposes_exactly_one_public_contract(string moduleName)
    {
        Assembly assembly = GetModuleAssembly(moduleName);

        List<Type> publicContracts = assembly
            .GetExportedTypes()
            .Where(type => type.IsInterface)
            .Where(type => type.Name.EndsWith("Module", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            publicContracts.Count == 1,
            $"{moduleName} should expose exactly one public module interface, " +
            $"but found {publicContracts.Count}: {string.Join(", ", publicContracts.Select(type => type.Name))}.");
    }

    /// <summary>
    /// The public contract of a module starts with the letter I and ends with Module.
    /// </summary>
    /// <param name="moduleName">The module under test.</param>
    [Theory]
    [InlineData("Aetheris.Economy")]
    [InlineData("Aetheris.Planets")]
    [InlineData("Aetheris.Fleet")]
    [InlineData("Aetheris.Market")]
    [InlineData("Aetheris.Rating")]
    [InlineData("Aetheris.Chat")]
    [InlineData("Aetheris.Admin")]
    [InlineData("Aetheris.World")]
    [InlineData("Aetheris.Requirements")]
    [InlineData("Aetheris.Research")]
    public void The_public_contract_follows_the_naming_convention(string moduleName)
    {
        Assembly assembly = GetModuleAssembly(moduleName);

        Type? contract = assembly
            .GetExportedTypes()
            .FirstOrDefault(type => type.IsInterface && type.Name.EndsWith("Module", StringComparison.Ordinal));

        Assert.NotNull(contract);
        Assert.StartsWith("I", contract.Name, StringComparison.Ordinal);
    }

    /// <summary>
    /// The host registers every module, and the module can be resolved from the real
    /// service provider.
    /// </summary>
    /// <remarks>
    /// This test resolves the modules from the container built by <c>Program.cs</c>. If a
    /// registration is removed from the host, the test fails.
    /// </remarks>
    [Fact]
    public void The_host_registers_every_module()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.NotNull(services.GetService<Aetheris.Economy.IEconomyModule>());
        Assert.NotNull(services.GetService<Aetheris.Planets.IPlanetsModule>());
        Assert.NotNull(services.GetService<Aetheris.Fleet.IFleetModule>());
        Assert.NotNull(services.GetService<Aetheris.Market.IMarketModule>());
        Assert.NotNull(services.GetService<Aetheris.Rating.IRatingModule>());
        Assert.NotNull(services.GetService<Aetheris.Chat.IChatModule>());
        Assert.NotNull(services.GetService<Aetheris.Admin.IAdminModule>());
        Assert.NotNull(services.GetService<Aetheris.World.IWorldModule>());
        Assert.NotNull(services.GetService<Aetheris.Requirements.IRequirementsModule>());
        Assert.NotNull(services.GetService<Aetheris.Research.IResearchModule>());
    }

    /// <summary>
    /// The modules are registered as singletons, so the same instance is returned every time.
    /// </summary>
    [Fact]
    public void The_modules_are_registered_as_singletons()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Aetheris.Economy.IEconomyModule first = services.GetRequiredService<Aetheris.Economy.IEconomyModule>();
        Aetheris.Economy.IEconomyModule second = services.GetRequiredService<Aetheris.Economy.IEconomyModule>();

        Assert.Same(first, second);
    }

    private static Assembly GetModuleAssembly(string moduleName)
    {
        Assembly? assembly = Assembly.Load(moduleName);

        Assert.NotNull(assembly);

        return assembly;
    }
}
