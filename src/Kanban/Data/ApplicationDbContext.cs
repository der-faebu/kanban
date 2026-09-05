using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Kanban.Data.Entities;

namespace Kanban.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardMember> BoardMembers => Set<BoardMember>();
    public DbSet<List> Lists => Set<List>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<CardAssignee> CardAssignees => Set<CardAssignee>();
    public DbSet<CardLabel> CardLabels => Set<CardLabel>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<CardProject> CardProjects => Set<CardProject>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<TimeLogEntry> TimeLogEntries => Set<TimeLogEntry>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<CardActivity> CardActivities => Set<CardActivity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(b =>
        {
            // Backfills existing rows with the migration-apply time on Up(); the app itself
            // always sets CreatedAt explicitly on insert via the C# property initializer.
            b.Property(u => u.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<IdentityUserPasskey<string>>(b =>
        {
            b.ToTable("AspNetUserPasskeys");
            b.HasKey(p => p.CredentialId);
            b.ComplexProperty(p => p.Data);
            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .IsRequired();
        });

        builder.Entity<Board>()
            .HasOne(b => b.Owner)
            .WithMany()
            .HasForeignKey(b => b.OwnerId)
            .IsRequired();

        builder.Entity<Board>()
            .HasMany(b => b.Members)
            .WithOne(bm => bm.Board)
            .HasForeignKey(bm => bm.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Board>()
            .HasMany(b => b.Lists)
            .WithOne(l => l.Board)
            .HasForeignKey(l => l.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Board>()
            .HasMany(b => b.Labels)
            .WithOne(l => l.Board)
            .HasForeignKey(l => l.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<BoardMember>()
            .HasOne(bm => bm.User)
            .WithMany()
            .HasForeignKey(bm => bm.UserId)
            .IsRequired();

        builder.Entity<BoardMember>()
            .HasIndex(bm => new { bm.BoardId, bm.UserId })
            .IsUnique();

        builder.Entity<List>()
            .HasIndex(l => new { l.BoardId, l.Position });

        builder.Entity<List>()
            .HasMany(l => l.Cards)
            .WithOne(c => c.List)
            .HasForeignKey(c => c.ListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Card>()
            .HasIndex(c => new { c.ListId, c.Position });

        builder.Entity<Card>()
            .HasMany(c => c.Assignees)
            .WithOne(ca => ca.Card)
            .HasForeignKey(ca => ca.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Card>()
            .HasMany(c => c.Labels)
            .WithOne(cl => cl.Card)
            .HasForeignKey(cl => cl.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CardAssignee>()
            .HasKey(ca => new { ca.CardId, ca.UserId });

        builder.Entity<CardAssignee>()
            .HasOne(ca => ca.User)
            .WithMany(u => u.CardAssignments)
            .HasForeignKey(ca => ca.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CardLabel>()
            .HasKey(cl => new { cl.CardId, cl.LabelId });

        builder.Entity<CardLabel>()
            .HasOne(cl => cl.Label)
            .WithMany(l => l.Cards)
            .HasForeignKey(cl => cl.LabelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Card>()
            .HasMany(c => c.Projects)
            .WithOne(cp => cp.Card)
            .HasForeignKey(cp => cp.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CardProject>()
            .HasKey(cp => new { cp.CardId, cp.ProjectId });

        builder.Entity<CardProject>()
            .HasOne(cp => cp.Project)
            .WithMany(p => p.Cards)
            .HasForeignKey(cp => cp.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Comment>()
            .HasOne(c => c.Card)
            .WithMany()
            .HasForeignKey(c => c.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Comment>()
            .HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .IsRequired();

        builder.Entity<TimeLogEntry>()
            .HasOne(t => t.Card)
            .WithMany()
            .HasForeignKey(t => t.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TimeLogEntry>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .IsRequired();

        builder.Entity<Card>()
            .HasOne(c => c.ParentCard)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentCardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChecklistItem>()
            .HasOne(i => i.Card)
            .WithMany()
            .HasForeignKey(i => i.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChecklistItem>()
            .HasIndex(i => new { i.CardId, i.Position });

        builder.Entity<Attachment>()
            .HasOne(a => a.Card)
            .WithMany()
            .HasForeignKey(a => a.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Attachment>()
            .HasOne(a => a.UploadedBy)
            .WithMany()
            .HasForeignKey(a => a.UploadedByUserId)
            .IsRequired();

        builder.Entity<CardActivity>()
            .HasOne(a => a.Card)
            .WithMany()
            .HasForeignKey(a => a.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CardActivity>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .IsRequired();
    }
}
