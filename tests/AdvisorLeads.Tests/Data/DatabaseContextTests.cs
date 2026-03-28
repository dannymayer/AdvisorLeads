using AdvisorLeads.Data;
using AdvisorLeads.Models;
using Microsoft.EntityFrameworkCore;

namespace AdvisorLeads.Tests.Data;

/// <summary>
/// Tests that DatabaseContext schema creation and migration work correctly
/// with an in-memory SQLite database.
/// </summary>
public class DatabaseContextTests : IDisposable
{
    private readonly string _dbPath;

    public DatabaseContextTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"advisorleads_test_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public void InitializeDatabase_CreatesAllTables()
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();

        var tables = GetTableNames(ctx);

        Assert.Contains("Advisors", tables);
        Assert.Contains("Firms", tables);
        Assert.Contains("EmploymentHistory", tables);
        Assert.Contains("Disclosures", tables);
        Assert.Contains("Qualifications", tables);
        Assert.Contains("AdvisorLists", tables);
        Assert.Contains("AdvisorListMembers", tables);
        Assert.Contains("FirmAumHistory", tables);
        Assert.Contains("FirmOwnership", tables);
        Assert.Contains("FormAdvFilings", tables);
        Assert.Contains("FirmFilings", tables);
        Assert.Contains("FirmFilingEvents", tables);
        Assert.Contains("EdgarSearchResults", tables);
    }

    [Fact]
    public void InitializeDatabase_SetsWalMode()
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();

        var conn = ctx.Database.GetDbConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode";
        var mode = cmd.ExecuteScalar()?.ToString();

        Assert.Equal("wal", mode);
    }

    [Fact]
    public void InitializeDatabase_IsIdempotent()
    {
        // Calling InitializeDatabase twice should not throw
        using (var ctx1 = new DatabaseContext(_dbPath))
        {
            ctx1.InitializeDatabase();
        }
        using (var ctx2 = new DatabaseContext(_dbPath))
        {
            ctx2.InitializeDatabase();
        }

        // Verify tables still exist
        using var ctx3 = new DatabaseContext(_dbPath);
        var tables = GetTableNames(ctx3);
        Assert.Contains("Advisors", tables);
        Assert.Contains("Firms", tables);
    }

    [Fact]
    public void SchemaUpgrade_AddsMissingColumns()
    {
        // Create DB with initial schema
        using (var ctx = new DatabaseContext(_dbPath))
        {
            ctx.InitializeDatabase();
        }

        // Verify key columns exist (they were added by migration)
        using (var ctx2 = new DatabaseContext(_dbPath))
        {
            var conn = ctx2.Database.GetDbConnection();
            conn.Open();
            var advisorCols = GetColumnNames(conn, "Advisors");

            Assert.Contains("IsFavorited", advisorCols);
            Assert.Contains("IsExcluded", advisorCols);
            Assert.Contains("ExclusionReason", advisorCols);
            Assert.Contains("IsImportedToCrm", advisorCols);
            Assert.Contains("CrmId", advisorCols);
            Assert.Contains("RegAuthorities", advisorCols);
            Assert.Contains("DisclosureFlags", advisorCols);
        }
    }

    [Fact]
    public void SchemaUpgrade_AddsMissingFirmColumns()
    {
        using (var ctx = new DatabaseContext(_dbPath))
        {
            ctx.InitializeDatabase();
        }

        using var ctx2 = new DatabaseContext(_dbPath);
        var conn = ctx2.Database.GetDbConnection();
        conn.Open();
        var firmCols = GetColumnNames(conn, "Firms");

        Assert.Contains("BrokerProtocolMember", firmCols);
        Assert.Contains("CompensationFeeOnly", firmCols);
        Assert.Contains("HasCustody", firmCols);
        Assert.Contains("IsBrokerDealer", firmCols);
        Assert.Contains("NumberOfOffices", firmCols);
        Assert.Contains("RegulatoryAum", firmCols);
    }

    [Fact]
    public void ClearAllData_RemovesAllRows()
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();

        // Insert a firm and an advisor
        ctx.Firms.Add(new Firm { CrdNumber = "12345", Name = "Test Firm" });
        ctx.SaveChanges();
        ctx.Advisors.Add(new Advisor { CrdNumber = "99999", FirstName = "John", LastName = "Doe" });
        ctx.SaveChanges();

        Assert.True(ctx.Firms.Any());
        Assert.True(ctx.Advisors.Any());

        ctx.ClearAllData();

        Assert.False(ctx.Firms.Any());
        Assert.False(ctx.Advisors.Any());
    }

    [Fact]
    public void CanInsertAndQueryAdvisor()
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();

        ctx.Advisors.Add(new Advisor
        {
            CrdNumber = "100001",
            FirstName = "Jane",
            LastName = "Smith",
            State = "CA",
            IsFavorited = true,
            IsExcluded = false,
            HasDisclosures = false,
            DisclosureCount = 0,
            Source = "FINRA"
        });
        ctx.SaveChanges();

        var loaded = ctx.Advisors.AsNoTracking().First(a => a.CrdNumber == "100001");
        Assert.Equal("Jane", loaded.FirstName);
        Assert.Equal("Smith", loaded.LastName);
        Assert.True(loaded.IsFavorited);
        Assert.Equal("CA", loaded.State);
    }

    [Fact]
    public void CanInsertAndQueryFirm()
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();

        ctx.Firms.Add(new Firm
        {
            CrdNumber = "200001",
            Name = "Acme Advisory",
            State = "NY",
            RegulatoryAum = 500_000_000m,
            BrokerProtocolMember = true
        });
        ctx.SaveChanges();

        var loaded = ctx.Firms.AsNoTracking().First(f => f.CrdNumber == "200001");
        Assert.Equal("Acme Advisory", loaded.Name);
        Assert.Equal(500_000_000m, loaded.RegulatoryAum);
        Assert.True(loaded.BrokerProtocolMember);
    }

    [Fact]
    public void CrdNumber_IsUnique()
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();

        ctx.Advisors.Add(new Advisor { CrdNumber = "DUP001", FirstName = "A", LastName = "B" });
        ctx.SaveChanges();

        ctx.Advisors.Add(new Advisor { CrdNumber = "DUP001", FirstName = "C", LastName = "D" });
        Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void CascadeDelete_RemovesChildRecords()
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();

        var advisor = new Advisor
        {
            CrdNumber = "CASCADE01",
            FirstName = "Test",
            LastName = "Cascade",
            EmploymentHistory = { new EmploymentHistory { FirmName = "Old Firm" } },
            Disclosures = { new Disclosure { Type = "Regulatory" } },
            QualificationList = { new Qualification { Name = "Series 7" } }
        };
        ctx.Advisors.Add(advisor);
        ctx.SaveChanges();

        Assert.Equal(1, ctx.EmploymentHistory.Count());
        Assert.Equal(1, ctx.Disclosures.Count());
        Assert.Equal(1, ctx.Qualifications.Count());

        ctx.Advisors.Remove(advisor);
        ctx.SaveChanges();

        Assert.Equal(0, ctx.EmploymentHistory.Count());
        Assert.Equal(0, ctx.Disclosures.Count());
        Assert.Equal(0, ctx.Qualifications.Count());
    }

    [Fact]
    public void InitializeDatabase_CreatesSingleColumnIndices()
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();

        var indices = GetIndexNames(ctx);

        // Advisor single-column indices
        Assert.Contains(indices, i => i.Contains("LastName", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(indices, i => i.Contains("State", StringComparison.OrdinalIgnoreCase)
            && i.Contains("Advisor", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(indices, i => i.Contains("City", StringComparison.OrdinalIgnoreCase)
            && i.Contains("Advisor", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(indices, i => i.Contains("CurrentFirmName", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(indices, i => i.Contains("IsExcluded", StringComparison.OrdinalIgnoreCase)
            && i.Contains("Advisor", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(indices, i => i.Contains("IsFavorited", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(indices, i => i.Contains("HasDisclosures", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(indices, i => i.Contains("IsImportedToCrm", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(indices, i => i.Contains("YearsOfExperience", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(indices, i => i.Contains("DisclosureCount", StringComparison.OrdinalIgnoreCase));

        // Firm single-column indices
        Assert.Contains(indices, i => i.Contains("Name", StringComparison.OrdinalIgnoreCase)
            && i.Contains("Firm", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(indices, i => i.Contains("BrokerProtocolMember", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(indices, i => i.Contains("RegulatoryAum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InitializeDatabase_SetsSynchronousNormal()
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();

        var conn = ctx.Database.GetDbConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA synchronous";
        var result = Convert.ToInt32(cmd.ExecuteScalar());

        // NORMAL = 1
        Assert.Equal(1, result);
    }

    private static List<string> GetTableNames(DatabaseContext ctx)
    {
        var tables = new List<string>();
        var conn = ctx.Database.GetDbConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            tables.Add(reader.GetString(0));
        return tables;
    }

    private static HashSet<string> GetColumnNames(System.Data.Common.DbConnection conn, string table)
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            cols.Add(reader.GetString(1));
        return cols;
    }

    private static List<string> GetIndexNames(DatabaseContext ctx)
    {
        var indices = new List<string>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name NOT LIKE 'sqlite_%'";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            indices.Add(reader.GetString(0));
        return indices;
    }
}
