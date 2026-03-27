using Microsoft.EntityFrameworkCore;
using AdvisorLeads.Models;

namespace AdvisorLeads.Data;

public class DatabaseContext : DbContext
{
    private readonly string _dbPath;

    public DatabaseContext(string databasePath)
    {
        _dbPath = databasePath;
    }

    public DbSet<Advisor> Advisors => Set<Advisor>();
    public DbSet<Firm> Firms => Set<Firm>();
    public DbSet<EmploymentHistory> EmploymentHistory => Set<EmploymentHistory>();
    public DbSet<Disclosure> Disclosures => Set<Disclosure>();
    public DbSet<Qualification> Qualifications => Set<Qualification>();
    public DbSet<AdvisorList> AdvisorLists => Set<AdvisorList>();
    public DbSet<AdvisorListMember> AdvisorListMembers => Set<AdvisorListMember>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        // ── Firms ──
        m.Entity<Firm>(e =>
        {
            e.ToTable("Firms");
            e.HasKey(f => f.Id);
            e.HasIndex(f => f.CrdNumber).IsUnique();
            e.HasIndex(f => new { f.State, f.RegistrationStatus, f.Name });

            e.Property(f => f.IsRegisteredWithSec).HasDefaultValue(false);
            e.Property(f => f.IsRegisteredWithFinra).HasDefaultValue(false);
            e.Property(f => f.IsExcluded).HasDefaultValue(false);
            e.Property(f => f.BrokerProtocolMember).HasDefaultValue(false);
            e.Property(f => f.CreatedAt).HasDefaultValueSql("datetime('now')");
            e.Property(f => f.UpdatedAt).HasDefaultValueSql("datetime('now')");
        });

        // ── Advisors ──
        m.Entity<Advisor>(e =>
        {
            e.ToTable("Advisors");
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.CrdNumber).IsUnique();
            e.HasIndex(a => a.LastName);
            e.HasIndex(a => a.State);
            e.HasIndex(a => a.CurrentFirmCrd);
            e.HasIndex(a => a.RecordType);
            e.HasIndex(a => a.Source);
            e.HasIndex(a => new { a.State, a.RecordType, a.RegistrationStatus, a.LastName });

            e.Property(a => a.HasDisclosures).HasDefaultValue(false);
            e.Property(a => a.DisclosureCount).HasDefaultValue(0);
            e.Property(a => a.IsExcluded).HasDefaultValue(false);
            e.Property(a => a.IsFavorited).HasDefaultValue(false);
            e.Property(a => a.IsImportedToCrm).HasDefaultValue(false);
            e.Property(a => a.CreatedAt).HasDefaultValueSql("datetime('now')");
            e.Property(a => a.UpdatedAt).HasDefaultValueSql("datetime('now')");

            e.Ignore(a => a.FullName);

            e.HasOne<Firm>().WithMany()
                .HasForeignKey(a => a.CurrentFirmId)
                .IsRequired(false);

            e.HasMany(a => a.EmploymentHistory).WithOne()
                .HasForeignKey(eh => eh.AdvisorId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(a => a.Disclosures).WithOne()
                .HasForeignKey(d => d.AdvisorId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(a => a.QualificationList).WithOne()
                .HasForeignKey(q => q.AdvisorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── EmploymentHistory ──
        m.Entity<EmploymentHistory>(e =>
        {
            e.ToTable("EmploymentHistory");
            e.HasKey(eh => eh.Id);
            e.HasIndex(eh => eh.AdvisorId);
            e.Ignore(eh => eh.IsCurrent);
        });

        // ── Disclosures ──
        m.Entity<Disclosure>(e =>
        {
            e.ToTable("Disclosures");
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.AdvisorId);
            e.Property(d => d.Type).HasColumnName("Type");
        });

        // ── Qualifications ──
        m.Entity<Qualification>(e =>
        {
            e.ToTable("Qualifications");
            e.HasKey(q => q.Id);
            e.HasIndex(q => q.AdvisorId);
        });

        // ── AdvisorLists ──
        m.Entity<AdvisorList>(e =>
        {
            e.ToTable("AdvisorLists");
            e.HasKey(l => l.Id);
            e.Ignore(l => l.MemberCount);
            e.Property(l => l.CreatedAt).HasDefaultValueSql("datetime('now')");
            e.Property(l => l.UpdatedAt).HasDefaultValueSql("datetime('now')");
        });

        // ── AdvisorListMembers ──
        m.Entity<AdvisorListMember>(e =>
        {
            e.ToTable("AdvisorListMembers");
            e.HasKey(lm => lm.Id);
            e.HasIndex(lm => lm.ListId);
            e.HasIndex(lm => lm.AdvisorId);
            e.HasIndex(lm => new { lm.ListId, lm.AdvisorId }).IsUnique();
            e.Property(lm => lm.AddedAt).HasDefaultValueSql("datetime('now')");

            e.HasOne<AdvisorList>().WithMany()
                .HasForeignKey(lm => lm.ListId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(lm => lm.Advisor).WithMany()
                .HasForeignKey(lm => lm.AdvisorId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// Creates the database and all tables/indexes if they don't exist.
    /// Call once at startup.
    /// </summary>
    public void InitializeDatabase()
    {
        Database.EnsureCreated();

        // Apply SQLite performance pragmas
        Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL");
        Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON");
        Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000");
        Database.ExecuteSqlRaw("PRAGMA cache_size=-8000");
        Database.ExecuteSqlRaw("PRAGMA temp_store=MEMORY");
        Database.ExecuteSqlRaw("PRAGMA mmap_size=268435456");
    }

    /// <summary>
    /// Deletes all rows from every table and resets autoincrement counters.
    /// </summary>
    public void ClearAllData()
    {
        Qualifications.ExecuteDelete();
        Disclosures.ExecuteDelete();
        EmploymentHistory.ExecuteDelete();
        AdvisorListMembers.ExecuteDelete();
        AdvisorLists.ExecuteDelete();
        Advisors.ExecuteDelete();
        Firms.ExecuteDelete();
        Database.ExecuteSqlRaw(
            "DELETE FROM sqlite_sequence WHERE name IN ('AdvisorListMembers','AdvisorLists','Qualifications','Disclosures','EmploymentHistory','Advisors','Firms')");
    }
}
