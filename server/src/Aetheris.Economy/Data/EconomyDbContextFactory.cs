using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aetheris.Economy.Data;

/// <summary>
/// Creates the context of the module for the tools of the migrations (task 00015).
/// </summary>
public sealed class EconomyDbContextFactory : IDesignTimeDbContextFactory<EconomyDbContext>
{
    /// <inheritdoc />
    public EconomyDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<EconomyDbContext> options = new();
        EconomyDatabase.Configure(options, new Npgsql.NpgsqlConnection());

        return new EconomyDbContext(options.Options);
    }
}
