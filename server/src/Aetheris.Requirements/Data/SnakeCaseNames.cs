using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Aetheris.Requirements.Data;

/// <summary>
/// The names of the tables of the module in snake_case (documentation/tech/08, rule 3).
/// </summary>
/// <remarks>
/// The helper lives in this module, so the module keeps no reference to another module of the
/// game (documentation/tech/01, section 8).
/// </remarks>
public static class SnakeCaseNames
{
    /// <summary>
    /// Converts every column, key and index name of the model to snake_case.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    public static void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            foreach (IMutableKey key in entityType.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName() ?? string.Empty));
            }

            foreach (IMutableForeignKey foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName() ?? string.Empty));
            }

            foreach (IMutableIndex index in entityType.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName() ?? string.Empty));
            }
        }
    }

    /// <summary>
    /// Converts a PascalCase name to snake_case.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The converted name.</returns>
    public static string ToSnakeCase(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        StringBuilder builder = new(name.Length + name.Length);

        for (int index = 0; index < name.Length; index++)
        {
            char current = name[index];

            if (char.IsUpper(current) && index > 0 && name[index - 1] != '_'
                && (char.IsLower(name[index - 1]) || char.IsDigit(name[index - 1])
                    || (index + 1 < name.Length && char.IsLower(name[index + 1]))))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}
