using System.Reflection;
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
    ];

    /// <summary>
    /// No module references another module's assembly.
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
    public void A_module_does_not_reference_another_module(string moduleName)
    {
        Assembly assembly = GetModuleAssembly(moduleName);

        List<string> foreignReferences = assembly
            .GetReferencedAssemblies()
            .Where(reference => ModuleNames.Contains(reference.Name ?? string.Empty))
            .Where(reference => reference.Name != moduleName)
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
    /// The host registers every module exactly once.
    /// </summary>
    [Fact]
    public void The_host_registers_every_module()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddSingleton<Aetheris.Economy.IEconomyModule, Aetheris.Economy.EconomyModule>();
        services.AddSingleton<Aetheris.Planets.IPlanetsModule, Aetheris.Planets.PlanetsModule>();
        services.AddSingleton<Aetheris.Fleet.IFleetModule, Aetheris.Fleet.FleetModule>();
        services.AddSingleton<Aetheris.Market.IMarketModule, Aetheris.Market.MarketModule>();
        services.AddSingleton<Aetheris.Rating.IRatingModule, Aetheris.Rating.RatingModule>();
        services.AddSingleton<Aetheris.Chat.IChatModule, Aetheris.Chat.ChatModule>();
        services.AddSingleton<Aetheris.Admin.IAdminModule, Aetheris.Admin.AdminModule>();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<Aetheris.Economy.IEconomyModule>());
        Assert.NotNull(provider.GetRequiredService<Aetheris.Planets.IPlanetsModule>());
        Assert.NotNull(provider.GetRequiredService<Aetheris.Fleet.IFleetModule>());
        Assert.NotNull(provider.GetRequiredService<Aetheris.Market.IMarketModule>());
        Assert.NotNull(provider.GetRequiredService<Aetheris.Rating.IRatingModule>());
        Assert.NotNull(provider.GetRequiredService<Aetheris.Chat.IChatModule>());
        Assert.NotNull(provider.GetRequiredService<Aetheris.Admin.IAdminModule>());
    }

    private static Assembly GetModuleAssembly(string moduleName)
    {
        Assembly? assembly = Assembly.Load(moduleName);

        Assert.NotNull(assembly);

        return assembly;
    }
}
