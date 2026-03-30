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
    public DbSet<AdvisorRegistration> AdvisorRegistrations => Set<AdvisorRegistration>();
    public DbSet<AlertLog> AlertLog => Set<AlertLog>();
    public DbSet<FirmAumAlertRule> FirmAumAlertRules => Set<FirmAumAlertRule>();
    public DbSet<MarketWatchRule> MarketWatchRules => Set<MarketWatchRule>();
    public DbSet<EmploymentChangeEvent> EmploymentChangeEvents => Set<EmploymentChangeEvent>();
    public DbSet<FinraSanction> FinraSanctions => Set<FinraSanction>();
    public DbSet<SecEnforcementAction> SecEnforcementActions => Set<SecEnforcementAction>();
    public DbSet<AdvisorCourtRecord> AdvisorCourtRecords => Set<AdvisorCourtRecord>();

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
            e.HasIndex(f => f.Name);
            e.HasIndex(f => f.City);
            e.HasIndex(f => f.RecordType);
            e.HasIndex(f => f.BrokerProtocolMember);
            e.HasIndex(f => f.IsExcluded);
            e.HasIndex(f => f.IsWatched);
            e.HasIndex(f => f.RegulatoryAum);
            e.HasIndex(f => new { f.State, f.RegistrationStatus, f.Name });
            e.HasIndex(f => new { f.IsExcluded, f.State, f.RecordType });

            e.Property(f => f.IsRegisteredWithSec).HasDefaultValue(false);
            e.Property(f => f.IsRegisteredWithFinra).HasDefaultValue(false);
            e.Property(f => f.IsExcluded).HasDefaultValue(false);
            e.Property(f => f.IsWatched).HasDefaultValue(false);
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
            e.HasIndex(a => a.City);
            e.HasIndex(a => a.CurrentFirmName);
            e.HasIndex(a => a.IsExcluded);
            e.HasIndex(a => a.IsFavorited);
            e.HasIndex(a => a.IsWatched);
            e.HasIndex(a => a.HasDisclosures);
            e.HasIndex(a => a.IsImportedToCrm);
            e.HasIndex(a => a.YearsOfExperience);
            e.HasIndex(a => a.DisclosureCount);
            e.HasIndex(a => new { a.State, a.RecordType, a.RegistrationStatus, a.LastName });
            e.HasIndex(a => new { a.IsExcluded, a.IsFavorited });
            e.HasIndex(a => new { a.IsExcluded, a.LastName });

            e.Property(a => a.HasDisclosures).HasDefaultValue(false);
            e.Property(a => a.DisclosureCount).HasDefaultValue(0);
            e.Property(a => a.IsExcluded).HasDefaultValue(false);
            e.Property(a => a.IsFavorited).HasDefaultValue(false);
            e.Property(a => a.IsWatched).HasDefaultValue(false);
            e.Property(a => a.IsWatched).HasDefaultValue(false);
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

            e.HasMany(a => a.Registrations).WithOne()
                .HasForeignKey(r => r.AdvisorId)
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

        // ── AdvisorRegistrations ──
        m.Entity<AdvisorRegistration>(e =>
        {
            e.ToTable("AdvisorRegistrations");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.AdvisorId);
            e.HasIndex(r => r.RegistrationType);
            // Uniqueness removed — UpsertRegistrations deletes-all-then-reinserts, so no duplicates.
            e.HasIndex(r => new { r.AdvisorId, r.RegistrationType, r.StateCode, r.RegistrationCategory });
        });

        // ── AlertLog ──
        m.Entity<AlertLog>(e =>
        {
            e.ToTable("AlertLog");
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.EntityCrd);
            e.HasIndex(a => a.AlertType);
            e.HasIndex(a => a.IsRead);
            e.HasIndex(a => a.IsAcknowledged);
            e.HasIndex(a => a.DetectedAt);
            e.HasIndex(a => new { a.EntityCrd, a.AlertType, a.DetectedAt });
            e.Property(a => a.IsRead).HasDefaultValue(false);
            e.Property(a => a.IsAcknowledged).HasDefaultValue(false);
            e.Property(a => a.DetectedAt).HasDefaultValueSql("datetime('now')");
            e.Property(a => a.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        // ── FirmAumAlertRules ──
        m.Entity<FirmAumAlertRule>(e =>
        {
            e.ToTable("FirmAumAlertRules");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.FirmCrd);
            e.HasIndex(r => r.IsActive);
            e.Property(r => r.IsActive).HasDefaultValue(true);
            e.Property(r => r.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        // ── MarketWatchRules ──
        m.Entity<MarketWatchRule>(e =>
        {
            e.ToTable("MarketWatchRules");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.IsActive);
            e.Property(r => r.IsActive).HasDefaultValue(true);
            e.Property(r => r.CreatedAt).HasDefaultValueSql("datetime('now')");
            e.Property(r => r.UpdatedAt).HasDefaultValueSql("datetime('now')");
        });

        // ── EmploymentChangeEvents ──
        m.Entity<EmploymentChangeEvent>(e =>
        {
            e.ToTable("EmploymentChangeEvents");
            e.HasKey(ev => ev.Id);
            e.HasIndex(ev => ev.AdvisorCrd);
            e.HasIndex(ev => ev.DetectedAt);
            e.Property(ev => ev.DetectedAt).HasDefaultValueSql("datetime('now')");
        });

        // ── FinraSanctions ──
        m.Entity<FinraSanction>(e =>
        {
            e.ToTable("FinraSanctions");
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.AdvisorCrd);
            e.HasIndex(s => s.FirmCrd);
            e.Property(s => s.IsActive).HasDefaultValue(false);
            e.Property(s => s.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        // ── SecEnforcementActions ──
        m.Entity<SecEnforcementAction>(e =>
        {
            e.ToTable("SecEnforcementActions");
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.AdvisorCrd);
            e.HasIndex(a => a.FirmCrd);
            e.Property(a => a.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        // ── AdvisorCourtRecords ──
        m.Entity<AdvisorCourtRecord>(e =>
        {
            e.ToTable("AdvisorCourtRecords");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.AdvisorCrd);
            e.Property(r => r.CreatedAt).HasDefaultValueSql("datetime('now')");
        });
    }

    /// <summary>
    /// Creates the database and all tables/indexes if they don't exist,
    /// then applies any pending EF Core migrations. Safe to call on both
    /// fresh databases and pre-existing production databases.
    /// Call once at startup.
    /// </summary>
    public void InitializeDatabase()
    {
        // Apply SQLite performance pragmas
        Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL");
        Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON");
        Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000");
        Database.ExecuteSqlRaw("PRAGMA cache_size=-8000");
        Database.ExecuteSqlRaw("PRAGMA temp_store=MEMORY");
        Database.ExecuteSqlRaw("PRAGMA mmap_size=268435456");
        Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL");

        // For existing pre-migration databases: stamp the baseline migration
        // so EF Core does not try to re-run InitialCreate DDL against tables
        // that already exist. Fresh databases go through normal Migrate() flow.
        StampBaselineIfNeeded();

        // Apply all pending migrations (no-op if already up to date)
        Database.Migrate();

        Database.ExecuteSqlRaw("PRAGMA optimize");
    }

    /// <summary>
    /// Detects pre-migration databases (tables exist but no migration history)
    /// and stamps them as already at the InitialCreate baseline so Migrate()
    /// does not attempt to recreate existing tables.
    /// </summary>
    private void StampBaselineIfNeeded()
    {
        var historyExists = false;
        try
        {
            Database.ExecuteSqlRaw("SELECT COUNT(*) FROM \"__EFMigrationsHistory\" LIMIT 1");
            historyExists = true;
        }
        catch { /* table does not exist */ }

        if (historyExists) return;

        var hasExistingData = false;
        try
        {
            Database.ExecuteSqlRaw("SELECT COUNT(*) FROM \"Advisors\" LIMIT 1");
            hasExistingData = true;
        }
        catch { /* fresh database — let Migrate() handle everything */ }

        if (!hasExistingData) return;

        // Existing DB without migration history — create the history table and stamp migrations.
        Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                ""MigrationId"" TEXT NOT NULL CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY,
                ""ProductVersion"" TEXT NOT NULL
            )");

        // Always stamp InitialCreate — EnsureCreated() built the schema from this migration's model.
        Database.ExecuteSqlRaw(@"
            INSERT OR IGNORE INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20260329202738_InitialCreate', '8.0.11')");

        // Only stamp AddBrokerCheckSroAndPdfFields if its DDL was already applied.
        // The old ApplySchemaUpgrades() added BrokerCheckReportPdfUrl to Advisors; if present,
        // the migration's schema is already in place and must not be run again.
        // If the column is absent, leave this migration unstamped so Database.Migrate() applies it.
        var advisorCols = GetExistingColumns("Advisors");
        if (advisorCols.Contains("BrokerCheckReportPdfUrl"))
        {
            Database.ExecuteSqlRaw(@"
                INSERT OR IGNORE INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                VALUES ('20260329210742_AddBrokerCheckSroAndPdfFields', '8.0.11')");
        }
    }

    /// <summary>
    /// Ensures that columns/tables added after initial database creation are present.
    /// EnsureCreated() only creates the schema when the DB file is brand-new; this
    /// method handles incremental upgrades for existing databases.
    /// </summary>
    private void ApplySchemaUpgrades()
    {
        // Create any new tables that don't exist yet
        var createScript = Database.GenerateCreateScript();
        foreach (var rawStmt in createScript.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var stmt = rawStmt.Trim();
            if (string.IsNullOrEmpty(stmt)) continue;

            if (stmt.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
            {
                var safe = stmt.Replace("CREATE TABLE", "CREATE TABLE IF NOT EXISTS");
                try { Database.ExecuteSqlRaw(safe); } catch { /* table already exists */ }
            }
            else if (stmt.StartsWith("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase))
            {
                var safe = stmt.Replace("CREATE UNIQUE INDEX", "CREATE UNIQUE INDEX IF NOT EXISTS");
                try { Database.ExecuteSqlRaw(safe); } catch { /* index already exists */ }
            }
            else if (stmt.StartsWith("CREATE INDEX", StringComparison.OrdinalIgnoreCase))
            {
                var safe = stmt.Replace("CREATE INDEX", "CREATE INDEX IF NOT EXISTS");
                try { Database.ExecuteSqlRaw(safe); } catch { /* index already exists */ }
            }
        }

        // Add any missing columns to existing tables
        AddMissingColumns("Advisors", new Dictionary<string, string>
        {
            ["IapdNumber"] = "TEXT",
            ["MiddleName"] = "TEXT",
            ["OtherNames"] = "TEXT",
            ["Suffix"] = "TEXT",
            ["IapdLink"] = "TEXT",
            ["RegAuthorities"] = "TEXT",
            ["DisclosureFlags"] = "TEXT",
            ["ExclusionReason"] = "TEXT",
            ["CrmId"] = "TEXT",
            ["Qualifications"] = "TEXT",
            ["IsFavorited"] = "INTEGER NOT NULL DEFAULT 0",
            ["IsWatched"] = "INTEGER NOT NULL DEFAULT 0",
            ["IsExcluded"] = "INTEGER NOT NULL DEFAULT 0",
            ["IsImportedToCrm"] = "INTEGER NOT NULL DEFAULT 0",
            ["HasCriminalDisclosure"] = "INTEGER NOT NULL DEFAULT 0",
            ["HasRegulatoryDisclosure"] = "INTEGER NOT NULL DEFAULT 0",
            ["HasCivilDisclosure"] = "INTEGER NOT NULL DEFAULT 0",
            ["HasCustomerComplaintDisclosure"] = "INTEGER NOT NULL DEFAULT 0",
            ["HasFinancialDisclosure"] = "INTEGER NOT NULL DEFAULT 0",
            ["HasTerminationDisclosure"] = "INTEGER NOT NULL DEFAULT 0",
            ["BcDisclosureCount"] = "INTEGER NOT NULL DEFAULT 0",
            ["IaDisclosureCount"] = "INTEGER NOT NULL DEFAULT 0",
            ["BcScope"] = "TEXT",
            ["IaScope"] = "TEXT",
            ["CareerStartDate"] = "TEXT",
            ["TotalFirmCount"] = "INTEGER",
            ["BrokerCheckUrl"] = "TEXT",
            ["CurrentFirmStartDate"] = "TEXT",
            ["FirstSeenAt"] = "TEXT",
            // Feature 1: Employment Delta
            ["HasRecentFirmChange"] = "INTEGER NOT NULL DEFAULT 0",
            ["FirmChangeDetectedAt"] = "TEXT",
            ["LastEmploymentCheckDate"] = "TEXT",
            // Feature 2: FINRA Sanctions
            ["HasActiveSanction"] = "INTEGER NOT NULL DEFAULT 0",
            ["MaxFineAmount"] = "REAL",
            ["SanctionType"] = "TEXT",
            ["SanctionEnrichedAt"] = "TEXT",
            // Feature 4: Registration Level
            ["RegistrationLevel"] = "TEXT",
            // Feature 5: SEC Enforcement
            ["HasSecEnforcementAction"] = "INTEGER NOT NULL DEFAULT 0",
            ["SecEnforcementUrl"] = "TEXT",
            ["SecEnforcementEnrichedAt"] = "TEXT",
            // Feature 8: Court Records
            ["CourtRecordFlag"] = "INTEGER NOT NULL DEFAULT 0",
            ["CourtRecordUrl"] = "TEXT",
            ["CourtRecordDate"] = "TEXT",
            ["CourtRecordSummary"] = "TEXT",
            ["CourtRecordEnrichedAt"] = "TEXT",
            ["MobilityScore"] = "INTEGER",
            ["DisclosureSeverityScore"] = "INTEGER",
            ["MobilityScoreUpdatedAt"] = "TEXT",
            ["BrokerCheckReportPdfUrl"] = "TEXT",
        });

        AddMissingColumns("Firms", new Dictionary<string, string>
        {
            ["SECNumber"] = "TEXT",
            ["SECRegion"] = "TEXT",
            ["LegalName"] = "TEXT",
            ["FaxPhone"] = "TEXT",
            ["MailingAddress"] = "TEXT",
            ["RegistrationStatus"] = "TEXT",
            ["AumDescription"] = "TEXT",
            ["StateOfOrganization"] = "TEXT",
            ["Country"] = "TEXT",
            ["LatestFilingDate"] = "TEXT",
            ["AdvisoryActivities"] = "TEXT",
            ["NumberOfEmployees"] = "INTEGER",
            ["NumClients"] = "INTEGER",
            ["ClientsIndividuals"] = "INTEGER",
            ["ClientsHighNetWorth"] = "INTEGER",
            ["ClientsBankingInstitutions"] = "INTEGER",
            ["ClientsInvestmentCompanies"] = "INTEGER",
            ["ClientsPensionPlans"] = "INTEGER",
            ["ClientsCharitable"] = "INTEGER",
            ["ClientsGovernment"] = "INTEGER",
            ["ClientsOther"] = "INTEGER",
            ["PrivateFundCount"] = "INTEGER",
            ["NumberOfOffices"] = "INTEGER",
            ["RegulatoryAum"] = "TEXT",
            ["RegulatoryAumNonDiscretionary"] = "TEXT",
            ["PrivateFundGrossAssets"] = "TEXT",
            ["TotalAumRelatedPersons"] = "TEXT",
            ["BrokerProtocolMember"] = "INTEGER NOT NULL DEFAULT 0",
            ["BrokerProtocolUpdatedAt"] = "TEXT",
            ["CompensationFeeOnly"] = "INTEGER",
            ["CompensationCommission"] = "INTEGER",
            ["CompensationHourly"] = "INTEGER",
            ["CompensationPerformanceBased"] = "INTEGER",
            ["HasCustody"] = "INTEGER",
            ["HasDiscretionaryAuthority"] = "INTEGER",
            ["IsBrokerDealer"] = "INTEGER",
            ["IsInsuranceCompany"] = "INTEGER",
            ["IsExcluded"] = "INTEGER NOT NULL DEFAULT 0",
            ["EdgarCik"] = "TEXT",
            ["SicCode"] = "TEXT",
            ["SicDescription"] = "TEXT",
            ["FiscalYearEnd"] = "TEXT",
            ["PriorAdvisorCount"] = "INTEGER",
            ["PriorAdvisorCountDate"] = "TEXT",
            ["IsWatched"] = "INTEGER NOT NULL DEFAULT 0",
            // Feature 2: FINRA Sanctions
            ["HasActiveSanction"] = "INTEGER NOT NULL DEFAULT 0",
            ["MaxFineAmount"] = "REAL",
            ["SanctionType"] = "TEXT",
            ["SanctionEnrichedAt"] = "TEXT",
            // Feature 3: Form ADV Deep
            ["InvestmentStrategies"] = "TEXT",
            ["WrapFeePrograms"] = "INTEGER",
            ["IsDuallyRegistered"] = "INTEGER",
            ["CCOName"] = "TEXT",
            ["CFOName"] = "TEXT",
            ["SoftDollarArrangements"] = "INTEGER",
            ["CryptoExposure"] = "INTEGER",
            ["DirectIndexing"] = "INTEGER",
            ["MarketingArrangements"] = "TEXT",
            ["OwnershipStructure"] = "TEXT",
            ["FormAdvDeepEnrichedAt"] = "TEXT",
            // Feature 4: Registration Level
            ["RegistrationLevel"] = "TEXT",
            // Feature 5: SEC Enforcement
            ["HasSecEnforcementAction"] = "INTEGER NOT NULL DEFAULT 0",
            ["SecEnforcementEnrichedAt"] = "TEXT",
            ["AdvisorCountChange1Yr"] = "INTEGER",
        });

        AddMissingColumns("FormAdvFilings", new Dictionary<string, string>
        {
            ["AdvisorCount"] = "INTEGER",
        });

        AddMissingColumns("EmploymentHistory", new Dictionary<string, string>
        {
            ["FirmCity"] = "TEXT",
            ["FirmState"] = "TEXT",
            ["RegistrationCategories"] = "TEXT",
        });

        AddMissingColumns("AdvisorRegistrations", new Dictionary<string, string>
        {
            ["RegistrationType"] = "TEXT NOT NULL DEFAULT 'State'",
            ["SroName"] = "TEXT",
        });
    }

    private void AddMissingColumns(string tableName, Dictionary<string, string> expectedColumns)
    {
        var existing = GetExistingColumns(tableName);
        foreach (var (column, definition) in expectedColumns)
        {
            if (!existing.Contains(column))
            {
                // Table/column names are from hardcoded dictionaries, not user input
                var sql = string.Concat("ALTER TABLE \"", tableName, "\" ADD COLUMN \"", column, "\" ", definition);
                try { Database.ExecuteSqlRaw(sql); }
                catch { /* column may already exist */ }
            }
        }
    }

    private HashSet<string> GetExistingColumns(string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conn = Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\")";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            columns.Add(reader.GetString(1)); // index 1 = column name
        return columns;
    }

    /// <summary>
    /// Deletes all rows from every table and resets autoincrement counters.
    /// </summary>
    public void ClearAllData()
    {
        // Child/dependent tables first
        EmploymentChangeEvents.ExecuteDelete();
        FinraSanctions.ExecuteDelete();
        SecEnforcementActions.ExecuteDelete();
        AdvisorCourtRecords.ExecuteDelete();
        AlertLog.ExecuteDelete();
        FirmAumAlertRules.ExecuteDelete();
        MarketWatchRules.ExecuteDelete();
        Qualifications.ExecuteDelete();
        Disclosures.ExecuteDelete();
        EmploymentHistory.ExecuteDelete();
        AdvisorRegistrations.ExecuteDelete();
        AdvisorListMembers.ExecuteDelete();
        AdvisorLists.ExecuteDelete();
        EdgarSearchResults.ExecuteDelete();
        FirmFilingEvents.ExecuteDelete();
        FirmFilings.ExecuteDelete();
        FirmAumHistory.ExecuteDelete();
        FirmOwnership.ExecuteDelete();
        FormAdvFilings.ExecuteDelete();
        // Parent tables last
        Advisors.ExecuteDelete();
        Firms.ExecuteDelete();
        Database.ExecuteSqlRaw(
            "DELETE FROM sqlite_sequence WHERE name IN ('AlertLog','FirmAumAlertRules','MarketWatchRules','EdgarSearchResults','FirmFilings','FirmFilingEvents','AdvisorListMembers','AdvisorLists','Qualifications','Disclosures','EmploymentHistory','AdvisorRegistrations','Advisors','Firms','FirmOwnership','FormAdvFilings','FirmAumHistory','EmploymentChangeEvents','FinraSanctions','SecEnforcementActions','AdvisorCourtRecords')");
    }
}
