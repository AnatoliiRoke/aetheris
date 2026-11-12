using Aetheris.Mail.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Mail.Data;

/// <summary>
/// The tables of the mail module: the letters, the boxes of the players, the drafts and the
/// complaints (task 00060).
/// </summary>
/// <remarks>
/// <para>
/// The letters live in the tables of this module, and the boxes carry only the identifiers of the
/// letters and the flags of the player. The tables carry no foreign keys into the tables of the
/// other modules: the players are named by their identifier, and the texts of the letters of the
/// game live in the common table of the texts (documentation/tech/08, rule 5).
/// </para>
/// <para>
/// Names are snake_case, and a reference to one row ends with <c>_id</c>
/// (documentation/tech/08, rule 3).
/// </para>
/// </remarks>
public sealed class MailDbContext : DbContext
{
    /// <summary>
    /// The length of the identifier of a player.
    /// </summary>
    private const int PlayerIdLength = 450;

    /// <summary>
    /// The length of a code.
    /// </summary>
    private const int CodeLength = 64;

    /// <summary>
    /// The length of a key of a text.
    /// </summary>
    private const int TextKeyLength = 128;

    /// <summary>
    /// The length of the subject of a letter.
    /// </summary>
    private const int SubjectLength = 200;

    /// <summary>
    /// The length of the text of a letter. The limit of the game is read from the configuration,
    /// and the column is a little longer, so a change of the limit does not need a migration.
    /// </summary>
    private const int BodyLength = 4000;

    /// <summary>
    /// The length of the reason of a complaint.
    /// </summary>
    private const int ReasonLength = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailDbContext"/> class.
    /// </summary>
    /// <param name="options">The database options.</param>
    public MailDbContext(DbContextOptions<MailDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the categories of the letters (task 00060).</summary>
    public DbSet<MailCategoryEntity> MailCategories => Set<MailCategoryEntity>();

    /// <summary>Gets the letters (task 00060).</summary>
    public DbSet<MailMessageEntity> MailMessages => Set<MailMessageEntity>();

    /// <summary>Gets the boxes of the players (task 00060).</summary>
    public DbSet<MailBoxEntity> MailBoxes => Set<MailBoxEntity>();

    /// <summary>Gets the drafts of the letters (task 00060).</summary>
    public DbSet<MailDraftEntity> MailDrafts => Set<MailDraftEntity>();

    /// <summary>Gets the complaints about the letters (task 00060).</summary>
    public DbSet<MailComplaintEntity> MailComplaints => Set<MailComplaintEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureGameTables(modelBuilder);
        SnakeCaseNames.Apply(modelBuilder);
    }

    private static void ConfigureGameTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MailCategoryEntity>(entity =>
        {
            entity.ToTable("mail_category");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(TextKeyLength);
            entity.Property(e => e.DescriptionKey).HasMaxLength(TextKeyLength);
            entity.Property(e => e.ImageKey).HasMaxLength(TextKeyLength);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => new { e.SortOrder, e.Id });
        });

        modelBuilder.Entity<MailMessageEntity>(entity =>
        {
            entity.ToTable("mail_message");
            entity.Property(e => e.SenderPlayerId).HasMaxLength(PlayerIdLength);
            entity.Property(e => e.ReceiverPlayerId).HasMaxLength(PlayerIdLength);
            entity.Property(e => e.KindCode).HasMaxLength(CodeLength);
            entity.Property(e => e.SystemKindCode).HasMaxLength(CodeLength);
            entity.Property(e => e.Subject).HasMaxLength(SubjectLength);
            entity.Property(e => e.Body).HasMaxLength(BodyLength);
            entity.Property(e => e.TextKey).HasMaxLength(TextKeyLength);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ReceiverPlayerId);
            entity.HasIndex(e => e.SenderPlayerId);
            entity.HasIndex(e => e.CategoryId);
            entity.HasOne<MailCategoryEntity>().WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MailBoxEntity>(entity =>
        {
            entity.ToTable("mail_box");
            entity.Property(e => e.PlayerId).HasMaxLength(PlayerIdLength);
            entity.Property(e => e.FolderCode).HasMaxLength(CodeLength);

            // The list of the letters is read by the player, the folder and the time, so the index
            // serves the selection of the screen (task 00060, section 3.1, point 4).
            entity.HasIndex(e => new { e.PlayerId, e.FolderCode, e.CreatedAt });
            entity.HasIndex(e => new { e.PlayerId, e.IsRead });
            entity.HasIndex(e => new { e.PlayerId, e.IsDeleted, e.DeletedAt });
            entity.HasIndex(e => e.MessageId);
            entity.HasOne<MailMessageEntity>().WithMany().HasForeignKey(e => e.MessageId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MailDraftEntity>(entity =>
        {
            entity.ToTable("mail_draft");
            entity.Property(e => e.PlayerId).HasMaxLength(PlayerIdLength);
            entity.Property(e => e.Subject).HasMaxLength(SubjectLength);
            entity.Property(e => e.Body).HasMaxLength(BodyLength);
            entity.HasIndex(e => e.PlayerId);
            entity.HasIndex(e => new { e.PlayerId, e.UpdatedAt });
        });

        modelBuilder.Entity<MailComplaintEntity>(entity =>
        {
            entity.ToTable("mail_complaint");
            entity.Property(e => e.PlayerId).HasMaxLength(PlayerIdLength);
            entity.Property(e => e.Reason).HasMaxLength(ReasonLength);
            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => new { e.IsReviewed, e.CreatedAt });
            entity.HasOne<MailMessageEntity>().WithMany().HasForeignKey(e => e.MessageId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
