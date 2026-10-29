using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aetheris.Processes.Data;

/// <summary>
/// Creates the context of the module for the tools of the migrations (task 00014).
/// </summary>
public sealed class ProcessesDbContextFactory : IDesignTimeDbContextFactory<ProcessesDbContext>
{
    /// <inheritdoc />
    public ProcessesDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<ProcessesDbContext> options = new();
        ProcessesDatabase.Configure(options, new Npgsql.NpgsqlConnection());

        return new ProcessesDbContext(options.Options);
    }
}
