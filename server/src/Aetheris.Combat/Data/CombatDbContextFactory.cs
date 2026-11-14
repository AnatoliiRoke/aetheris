using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Aetheris.Combat.Data;

/// <summary>
/// Creates the context of the module for <c>dotnet ef migrations add</c>.
/// </summary>
/// <remarks>
/// Adding a migration does not open a connection, so the factory uses an unconfigured
/// connection: no connection string is stored in the code.
/// </remarks>
public sealed class CombatDbContextFactory : IDesignTimeDbContextFactory<CombatDbContext>
{
    /// <inheritdoc />
    public CombatDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<CombatDbContext> options = new();
        CombatDatabase.Configure(options, new NpgsqlConnection());

        return new CombatDbContext(options.Options);
    }
}
