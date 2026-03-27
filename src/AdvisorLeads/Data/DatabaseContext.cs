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
    public DbSet<EdgarSearchResult> EdgarSearchResults => Set<EdgarSearchResult>();
    public DbSet<FirmFiling> FirmFilings => Set<FirmFiling>();
    public DbSet<FirmOwnership> FirmOwnership => Set<FirmOwnership>();
    public DbSet<FormAdvFiling> FormAdvFilings => Set<FormAdvFiling>();
    public DbSet<FirmFilingEvent> FirmFilingEvents => Set<FirmFilingEvent>();
    public DbSet<FirmAumHistory> FirmAumHistory => Set<FirmAumHistory>();

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
            e.Property(f => f.CompensationFeeOnly).HasDefaultValue(false);
            e.Property(f => f.CompensationCommission).HasDefaultValue(false);
            e.Property(f => f.CompensationHourly).HasDefaultValue(false);
            e.Property(f => f.CompensationPerformanceBased).HasDefaultValue(false);
            e.Property(f => f.HasCustody).HasDefaultValue(false);
            e.Property(f => f.HasDiscretionaryAuthority).HasDefaultValue(false);
            e.Property(f => f.IsBrokerDealer).HasDefaultValue(false);
            e.Property(f => f.IsInsuranceCompany).HasDefaultValue(false);
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

        // ── FirmOwnership ──
        m.Entity<FirmOwnership>(e =>
        {
            e.ToTable("FirmOwnership");
            e.HasKey(o => o.Id);
            e.HasIndex(o => o.FirmCrd);
            e.HasIndex(o => new { o.FirmCrd, o.FilingDate });
            e.Property(o => o.IsDirectOwner).HasDefaultValue(true);
            e.Property(o => o.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        // ── FormAdvFilings ──
        m.Entity<FormAdvFiling>(e =>
        {
            e.ToTable("FormAdvFilings");
            e.HasKey(f => f.Id);
            e.HasIndex(f => f.FirmCrd);
            e.HasIndex(f => new { f.FirmCrd, f.FilingDate });
            e.HasIndex(f => f.FilingDate);
            e.Property(f => f.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        // ── FirmFilingEvents ──
        m.Entity<FirmFilingEvent>(e =>
        {
            e.ToTable("FirmFilingEvents");
            e.HasKey(ev => ev.Id);
            e.HasIndex(ev => ev.FirmCrd);
            e.HasIndex(ev => ev.EventType);
            e.HasIndex(ev => ev.EventDate);
            e.HasIndex(ev => ev.Severity);
            e.HasIndex(ev => new { ev.FirmCrd, ev.EventDate });
            e.Property(ev => ev.IsReviewed).HasDefaultValue(false);
            e.Property(ev => ev.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        // ── EdgarSearchResults ──
        m.Entity<EdgarSearchResult>(e =>
        {
            e.ToTable("EdgarSearchResults");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.FirmCrd);
            e.HasIndex(r => r.AccessionNumber);
            e.HasIndex(r => new { r.AccessionNumber, r.SearchQuery });
            e.HasIndex(r => r.Category);
            e.Property(r => r.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        // ── FirmFilings ──
        m.Entity<FirmFiling>(e =>
        {
            e.ToTable("FirmFilings");
            e.HasKey(f => f.Id);
            e.HasIndex(f => f.FirmCrd);
            e.HasIndex(f => f.AccessionNumber).IsUnique();
            e.HasIndex(f => new { f.FirmCrd, f.FilingDate });
            e.Property(f => f.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        // ── FirmAumHistory ──
        m.Entity<FirmAumHistory>(e =>
        {
            e.ToTable("FirmAumHistory");
            e.HasKey(h => h.Id);
            e.HasIndex(h => h.FirmCrd);
            e.HasIndex(h => new { h.FirmCrd, h.SnapshotDate }).IsUnique();
            e.HasIndex(h => h.SnapshotDate);
            e.Property(h => h.CreatedAt).HasDefaultValueSql("datetime('now')");
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
        EdgarSearchResults.ExecuteDelete();
        FirmFilingEvents.ExecuteDelete();
        FirmFilings.ExecuteDelete();
        FirmAumHistory.ExecuteDelete();
        Firms.ExecuteDelete();
        FirmOwnership.ExecuteDelete();
        FormAdvFilings.ExecuteDelete();
        Database.ExecuteSqlRaw(
            "DELETE FROM sqlite_sequence WHERE name IN ('EdgarSearchResults','FirmFilings','FirmFilingEvents','AdvisorListMembers','AdvisorLists','Qualifications','Disclosures','EmploymentHistory','Advisors','Firms','FirmOwnership','FormAdvFilings','FirmAumHistory')");
    }
}
