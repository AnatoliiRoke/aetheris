namespace Aetheris.World.Services;

/// <summary>
/// Tells whether a module holds data of a resource: stocks, balances or rows of a cost
/// (task 00015, section 3.1).
/// </summary>
/// <remarks>
/// The world owns the scope of a resource, and the scope may not change while another module
/// holds data of the resource, because those rows were written for the old scope (a balance of a
/// civilization would be left without a currency, a cost would go to the stock of a body). The
/// contract is declared here and implemented by the modules that hold such data, so the world
/// reads no table of another module (documentation/tech/08, rule 5).
/// </remarks>
public interface IResourceUsage
{
    /// <summary>
    /// Tells whether the module holds data of a resource.
    /// </summary>
    /// <param name="resourceTypeId">The resource.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when a stock, a balance or a row of a cost names the resource.</returns>
    Task<bool> IsUsedAsync(int resourceTypeId, CancellationToken cancellationToken = default);
}
