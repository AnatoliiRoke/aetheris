namespace Aetheris.Tests.Infrastructure;

/// <summary>
/// The shared collection for every test that touches the database.
/// </summary>
/// <remarks>
/// All database tests share one PostgreSQL database, so they must not run in parallel:
/// one test writing rows would otherwise interfere with another test reading them. The
/// collection fixture also gives every test class the same host instance, and the
/// database is cleared once before the collection starts.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DatabaseCollection : ICollectionFixture<AetherisTestFactory>
{
    /// <summary>
    /// The name of the collection.
    /// </summary>
    public const string Name = "database";
}
