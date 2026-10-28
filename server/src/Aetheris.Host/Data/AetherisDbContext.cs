using Aetheris.Host.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Data;

/// <summary>
/// The application database context.
/// </summary>
/// <remarks>
/// Holds the tables listed in documentation/tech/03-model-danykh.md, section 3, plus the
/// ASP.NET Core Identity tables for player accounts.
/// Table and column names use snake_case, as documented for the PostgreSQL schema.
/// </remarks>
public sealed class AetherisDbContext : IdentityDbContext<IdentityUser>
{
    /// <summary>
    /// The suffix of the first duplicate constraint name.
    /// </summary>
    private const int FirstDuplicateSuffix = 2;

    /// <summary>
    /// Initializes a new instance of the <see cref="AetherisDbContext"/> class.
    /// </summary>
    /// <param name="options">The database options.</param>
    public AetherisDbContext(DbContextOptions<AetherisDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets the configuration parameters table.
    /// </summary>
    public DbSet<ConfigParameterEntity> ConfigParameters => Set<ConfigParameterEntity>();

    /// <summary>
    /// Gets the configuration versions table.
    /// </summary>
    public DbSet<ConfigVersionEntity> ConfigVersions => Set<ConfigVersionEntity>();

    /// <summary>
    /// Gets the scheduled events table.
    /// </summary>
    public DbSet<ScheduledEventEntity> ScheduledEvents => Set<ScheduledEventEntity>();

    /// <summary>
    /// Gets the per-player notification table.
    /// </summary>
    public DbSet<PlayerEventEntity> PlayerEvents => Set<PlayerEventEntity>();

    /// <summary>
    /// Gets the administrator accounts table.
    /// </summary>
    public DbSet<AdminAccountEntity> AdminAccounts => Set<AdminAccountEntity>();

    /// <summary>
    /// Gets the administrator audit log table.
    /// </summary>
    public DbSet<AdminAuditLogEntity> AdminAuditLogs => Set<AdminAuditLogEntity>();

    /// <summary>
    /// Gets the administrator roles table.
    /// </summary>
    public DbSet<AdminRoleEntity> AdminRoles => Set<AdminRoleEntity>();

    /// <summary>
    /// Gets the administrator permissions table.
    /// </summary>
    public DbSet<AdminPermissionEntity> AdminPermissions => Set<AdminPermissionEntity>();

    /// <summary>
    /// Gets the permissions of the administrator roles.
    /// </summary>
    public DbSet<AdminRolePermissionEntity> AdminRolePermissions => Set<AdminRolePermissionEntity>();

    /// <summary>
    /// Gets the roles of the administrator accounts.
    /// </summary>
    public DbSet<AdminAccountRoleEntity> AdminAccountRoles => Set<AdminAccountRoleEntity>();

    /// <summary>
    /// Gets the administrator sessions table.
    /// </summary>
    public DbSet<AdminSessionEntity> AdminSessions => Set<AdminSessionEntity>();

    /// <summary>
    /// Gets the history of the configuration parameters.
    /// </summary>
    public DbSet<ConfigParameterChangeEntity> ConfigParameterChanges => Set<ConfigParameterChangeEntity>();

    /// <summary>
    /// Gets the parameters the admin panel never changes.
    /// </summary>
    public DbSet<ReadOnlyConfigParameterEntity> ReadOnlyConfigParameters => Set<ReadOnlyConfigParameterEntity>();

    /// <summary>
    /// Gets the security journal table.
    /// </summary>
    public DbSet<SecurityEventEntity> SecurityEvents => Set<SecurityEventEntity>();

    /// <summary>
    /// Gets the table of executed request identifiers.
    /// </summary>
    public DbSet<RequestIdempotencyEntity> RequestIdempotency => Set<RequestIdempotencyEntity>();

    /// <summary>
    /// Gets the refresh tokens table.
    /// </summary>
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    /// <summary>
    /// Gets the change journal table.
    /// </summary>
    public DbSet<ChangeJournalEntity> ChangeJournal => Set<ChangeJournalEntity>();

    /// <summary>
    /// Gets the player profiles table.
    /// </summary>
    public DbSet<PlayerProfileEntity> PlayerProfiles => Set<PlayerProfileEntity>();

    /// <summary>
    /// Gets the journal of registrations and sign-ins.
    /// </summary>
    public DbSet<AccountAccessLogEntity> AccountAccessLog => Set<AccountAccessLogEntity>();

    /// <summary>
    /// Gets the sign-in attempts table.
    /// </summary>
    public DbSet<SignInAttemptEntity> SignInAttempts => Set<SignInAttemptEntity>();

    /// <summary>
    /// Gets the blocked e-mail domains table.
    /// </summary>
    public DbSet<BlockedEmailDomainEntity> BlockedEmailDomains => Set<BlockedEmailDomainEntity>();

    /// <summary>
    /// Gets the restricted words table.
    /// </summary>
    public DbSet<RestrictedWordEntity> RestrictedWords => Set<RestrictedWordEntity>();

    /// <summary>
    /// Gets the action codes allowed before the e-mail confirmation.
    /// </summary>
    public DbSet<UnconfirmedAllowedActionEntity> UnconfirmedAllowedActions => Set<UnconfirmedAllowedActionEntity>();

    /// <summary>
    /// Gets the texts of the game data.
    /// </summary>
    public DbSet<LocalizationTextEntity> LocalizationTexts => Set<LocalizationTextEntity>();

    /// <summary>
    /// Gets the counters of the neutral codes.
    /// </summary>
    public DbSet<CodeSequenceEntity> CodeSequences => Set<CodeSequenceEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        ApplySnakeCaseNames(builder);

        builder.Entity<ConfigParameterEntity>(entity =>
        {
            entity.ToTable("config_parameter");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(128);
            entity.Property(e => e.Value).HasMaxLength(4096);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
        });

        builder.Entity<ConfigVersionEntity>(entity =>
        {
            entity.ToTable("config_version");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.AppliedBy).HasMaxLength(256);
            entity.HasIndex(e => e.Version);
        });

        builder.Entity<ScheduledEventEntity>(entity =>
        {
            entity.ToTable("scheduled_event");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Type).HasMaxLength(128);
            entity.Property(e => e.Status).HasMaxLength(32);
            entity.Property(e => e.DedupeKey).HasMaxLength(256);

            // The scheduler always asks for pending events of one sector, ordered by time.
            entity.HasIndex(e => new { e.SectorId, e.Status, e.RunAt });
            entity.HasIndex(e => e.Seq);
            entity.HasIndex(e => e.DedupeKey);
        });

        builder.Entity<PlayerEventEntity>(entity =>
        {
            entity.ToTable("player_event");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.PlayerId).HasMaxLength(450);
            entity.Property(e => e.Type).HasMaxLength(128);

            // A client asks for the events it missed by player and sequence number.
            entity.HasIndex(e => new { e.PlayerId, e.Seq });
        });

        builder.Entity<AdminAccountEntity>(entity =>
        {
            entity.ToTable("admin_account");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(450);
            entity.Property(e => e.IdentityUserId).HasMaxLength(450);
            entity.Property(e => e.DisplayName).HasMaxLength(256);
            entity.HasIndex(e => e.IdentityUserId).IsUnique();
        });

        builder.Entity<AdminRoleEntity>(entity =>
        {
            entity.ToTable("admin_role");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Code).HasMaxLength(64);
            entity.Property(e => e.NameKey).HasMaxLength(128);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        builder.Entity<AdminPermissionEntity>(entity =>
        {
            entity.ToTable("admin_permission");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Code).HasMaxLength(128);
            entity.Property(e => e.Kind).HasMaxLength(32);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        builder.Entity<AdminRolePermissionEntity>(entity =>
        {
            entity.ToTable("admin_role_permission");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => new { e.AdminRoleId, e.AdminPermissionId }).IsUnique();
            entity.HasIndex(e => e.AdminPermissionId);
            entity.HasOne<AdminRoleEntity>().WithMany().HasForeignKey(e => e.AdminRoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<AdminPermissionEntity>().WithMany().HasForeignKey(e => e.AdminPermissionId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AdminAccountRoleEntity>(entity =>
        {
            entity.ToTable("admin_account_role");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.AdminAccountId).HasMaxLength(450);
            entity.HasIndex(e => new { e.AdminAccountId, e.AdminRoleId }).IsUnique();
            entity.HasIndex(e => e.AdminRoleId);
            entity.HasOne<AdminAccountEntity>().WithMany().HasForeignKey(e => e.AdminAccountId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<AdminRoleEntity>().WithMany().HasForeignKey(e => e.AdminRoleId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AdminSessionEntity>(entity =>
        {
            entity.ToTable("admin_session");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.AdminAccountId).HasMaxLength(450);
            entity.Property(e => e.SecondFactor).HasMaxLength(32);
            entity.Property(e => e.EndReason).HasMaxLength(64);
            entity.HasIndex(e => e.AdminAccountId);
            entity.HasOne<AdminAccountEntity>().WithMany().HasForeignKey(e => e.AdminAccountId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ConfigParameterChangeEntity>(entity =>
        {
            entity.ToTable("config_parameter_change");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Key).HasMaxLength(128);
            entity.Property(e => e.OldValue).HasMaxLength(4096);
            entity.Property(e => e.NewValue).HasMaxLength(4096);
            entity.Property(e => e.Reason).HasMaxLength(2000);
            entity.Property(e => e.AdminAccountId).HasMaxLength(450);

            // The history of one parameter, newest first.
            entity.HasIndex(e => new { e.Key, e.Id });
            entity.HasOne<ConfigParameterChangeEntity>().WithMany().HasForeignKey(e => e.RolledBackChangeId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReadOnlyConfigParameterEntity>(entity =>
        {
            entity.ToTable("config_parameter_read_only");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(128);
        });

        builder.Entity<AdminAuditLogEntity>(entity =>
        {
            entity.ToTable("admin_audit_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.AdminId).HasMaxLength(450);
            entity.Property(e => e.Action).HasMaxLength(256);
            entity.Property(e => e.Target).HasMaxLength(512);
            entity.Property(e => e.Reason).HasMaxLength(2000);
            entity.HasIndex(e => e.At);
        });

        builder.Entity<SecurityEventEntity>(entity =>
        {
            entity.ToTable("security_event");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Type).HasMaxLength(64);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.Property(e => e.Detail).HasMaxLength(256);

            // The alerts count recent events of one type.
            entity.HasIndex(e => new { e.Type, e.At });
            entity.HasIndex(e => e.UserId);
        });

        builder.Entity<RequestIdempotencyEntity>(entity =>
        {
            entity.ToTable("request_idempotency");
            entity.HasKey(e => new { e.UserId, e.Key });
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.Key).HasMaxLength(256);
            entity.Property(e => e.Route).HasMaxLength(256);
            entity.Property(e => e.RequestHash).HasMaxLength(64);

            // The cleanup removes rows by age.
            entity.HasIndex(e => e.CreatedAt);
        });

        builder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.ToTable("refresh_token");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.TokenHash).HasMaxLength(64);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        builder.Entity<PlayerProfileEntity>(entity =>
        {
            entity.ToTable("player_profile");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.HasOne<IdentityUser>().WithOne().HasForeignKey<PlayerProfileEntity>(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AccountAccessLogEntity>(entity =>
        {
            entity.ToTable("account_access_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.Kind).HasMaxLength(32);
            entity.Property(e => e.IpAddress).HasMaxLength(64);

            // The registration limit counts the entries of one address; the admin panel reads
            // the entries of one account.
            entity.HasIndex(e => new { e.IpAddress, e.Kind, e.At });
            entity.HasIndex(e => new { e.UserId, e.At });
        });

        builder.Entity<SignInAttemptEntity>(entity =>
        {
            entity.ToTable("sign_in_attempt");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.LoginHash).HasMaxLength(64);
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.HasIndex(e => new { e.LoginHash, e.At });
            entity.HasIndex(e => new { e.IpAddress, e.At });
        });

        builder.Entity<BlockedEmailDomainEntity>(entity =>
        {
            entity.ToTable("blocked_email_domain");
            entity.HasKey(e => e.Domain);
            entity.Property(e => e.Domain).HasMaxLength(256);
        });

        builder.Entity<RestrictedWordEntity>(entity =>
        {
            entity.ToTable("restricted_word");
            entity.HasKey(e => e.Word);
            entity.Property(e => e.Word).HasMaxLength(64);
            entity.Property(e => e.Kind).HasMaxLength(32);
        });

        builder.Entity<UnconfirmedAllowedActionEntity>(entity =>
        {
            entity.ToTable("unconfirmed_allowed_action");
            entity.HasKey(e => e.ActionCode);
            entity.Property(e => e.ActionCode).HasMaxLength(128);
        });

        builder.Entity<ChangeJournalEntity>(entity =>
        {
            entity.ToTable("change_journal");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.ActorId).HasMaxLength(450);
            entity.Property(e => e.RequestId).HasMaxLength(256);
            entity.Property(e => e.ObjectType).HasMaxLength(64);
            entity.Property(e => e.ObjectId).HasMaxLength(450);
            entity.Property(e => e.Field).HasMaxLength(128);
            entity.Property(e => e.Reason).HasMaxLength(256);
            entity.HasIndex(e => new { e.ObjectType, e.ObjectId, e.At });
        });

        builder.Entity<LocalizationTextEntity>(entity =>
        {
            entity.ToTable("localization_text");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Key).HasMaxLength(256);
            entity.Property(e => e.Language).HasMaxLength(8);
            entity.Property(e => e.Text).HasMaxLength(10000);
            entity.Property(e => e.UpdatedBy).HasMaxLength(450);

            // One text per key and language; the client reads the texts of one language.
            entity.HasIndex(e => new { e.Key, e.Language }).IsUnique();
            entity.HasIndex(e => e.Language);
        });

        builder.Entity<CodeSequenceEntity>(entity =>
        {
            entity.ToTable("code_sequence");
            entity.HasKey(e => e.CodeGroup);
            entity.Property(e => e.CodeGroup).HasMaxLength(64);
            entity.ToTable(table => table.HasCheckConstraint("ck_code_sequence_last_number", "last_number >= 0"));
        });
    }

    /// <summary>
    /// Converts every table and column name to snake_case.
    /// </summary>
    /// <param name="builder">The model builder.</param>
    private static void ApplySnakeCaseNames(ModelBuilder builder)
    {
        HashSet<string> usedConstraintNames = new(StringComparer.OrdinalIgnoreCase);

        foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in builder.Model.GetEntityTypes())
        {
            string? tableName = entityType.GetTableName();

            if (tableName is not null)
            {
                entityType.SetTableName(ToSnakeCase(tableName));
            }

            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableProperty property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
            }

            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableKey key in entityType.GetKeys())
            {
                key.SetName(UniqueConstraintName(ToSnakeCase(key.GetName() ?? string.Empty), usedConstraintNames));
            }

            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableForeignKey foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.SetConstraintName(
                    UniqueConstraintName(ToSnakeCase(foreignKey.GetConstraintName() ?? string.Empty), usedConstraintNames));
            }

            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableIndex index in entityType.GetIndexes())
            {
                index.SetDatabaseName(
                    UniqueConstraintName(ToSnakeCase(index.GetDatabaseName() ?? string.Empty), usedConstraintNames));
            }
        }
    }

    /// <summary>
    /// Makes a constraint name unique within the database.
    /// </summary>
    /// <param name="name">The proposed constraint name.</param>
    /// <param name="used">The set of already used names.</param>
    /// <returns>A name that is not yet used.</returns>
    /// <remarks>
    /// The Identity tables and the admin table both inherit the <c>PK_AspNetUsers</c>
    /// constraint name, which would collide in PostgreSQL. A numeric suffix is added to the
    /// second occurrence.
    /// </remarks>
    private static string UniqueConstraintName(string name, HashSet<string> used)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        string candidate = name;

        // The first duplicate gets the suffix 2, the next one 3 and so on.
        int suffix = FirstDuplicateSuffix;

        while (!used.Add(candidate))
        {
            candidate = $"{name}_{suffix}";
            suffix++;
        }

        return candidate;
    }

    /// <summary>
    /// Converts a PascalCase name to snake_case.
    /// </summary>
    /// <param name="name">The name to convert.</param>
    /// <returns>The converted name.</returns>
    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        System.Text.StringBuilder builder = new(name.Length + 8);

        for (int index = 0; index < name.Length; index++)
        {
            char current = name[index];

            bool isUpper = char.IsUpper(current);

            if (isUpper && index > 0)
            {
                char previous = name[index - 1];

                bool previousIsLower = char.IsLower(previous) || char.IsDigit(previous);
                bool nextIsLower = index + 1 < name.Length && char.IsLower(name[index + 1]);
                bool previousIsUpper = char.IsUpper(previous);

                if (previousIsLower || (previousIsUpper && nextIsLower))
                {
                    builder.Append('_');
                }
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}
