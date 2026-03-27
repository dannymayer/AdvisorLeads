using Microsoft.Data.Sqlite;

namespace AdvisorLeads.Data;

public class DatabaseContext : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;
    private readonly SemaphoreSlim _dbLock = new SemaphoreSlim(1, 1);

    public DatabaseContext(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}";
    }

    public SqliteConnection GetConnection()
    {
        if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode=WAL;
                PRAGMA foreign_keys=ON;
                PRAGMA busy_timeout=5000;
                PRAGMA cache_size=-8000;
                PRAGMA temp_store=MEMORY;
                PRAGMA mmap_size=268435456;";
            cmd.ExecuteNonQuery();
        }
        return _connection;
    }

    public void InitializeDatabase()
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Firms (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CrdNumber TEXT UNIQUE,
                Name TEXT NOT NULL,
                Address TEXT,
                City TEXT,
                State TEXT,
                ZipCode TEXT,
                Phone TEXT,
                Website TEXT,
                BusinessType TEXT,
                IsRegisteredWithSec INTEGER DEFAULT 0,
                IsRegisteredWithFinra INTEGER DEFAULT 0,
                NumberOfAdvisors INTEGER,
                RegistrationDate TEXT,
                Source TEXT,
                RecordType TEXT,
                IsExcluded INTEGER DEFAULT 0,
                CreatedAt TEXT DEFAULT (datetime('now')),
                UpdatedAt TEXT DEFAULT (datetime('now')),
                SECNumber TEXT,
                SECRegion TEXT,
                LegalName TEXT,
                FaxPhone TEXT,
                MailingAddress TEXT,
                RegistrationStatus TEXT,
                AumDescription TEXT,
                StateOfOrganization TEXT
            );

            CREATE TABLE IF NOT EXISTS Advisors (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CrdNumber TEXT UNIQUE,
                IapdNumber TEXT,
                FirstName TEXT NOT NULL,
                LastName TEXT NOT NULL,
                MiddleName TEXT,
                Title TEXT,
                Email TEXT,
                Phone TEXT,
                City TEXT,
                State TEXT,
                ZipCode TEXT,
                Licenses TEXT,
                Qualifications TEXT,
                CurrentFirmName TEXT,
                CurrentFirmCrd TEXT,
                CurrentFirmId INTEGER,
                RegistrationStatus TEXT,
                RegistrationDate TEXT,
                YearsOfExperience INTEGER,
                HasDisclosures INTEGER DEFAULT 0,
                DisclosureCount INTEGER DEFAULT 0,
                Source TEXT,
                RecordType TEXT,
                IsExcluded INTEGER DEFAULT 0,
                ExclusionReason TEXT,
                IsImportedToCrm INTEGER DEFAULT 0,
                CrmId TEXT,
                CreatedAt TEXT DEFAULT (datetime('now')),
                UpdatedAt TEXT DEFAULT (datetime('now')),
                Suffix TEXT,
                IapdLink TEXT,
                RegAuthorities TEXT,
                DisclosureFlags TEXT,
                FOREIGN KEY (CurrentFirmId) REFERENCES Firms(Id)
            );

            CREATE TABLE IF NOT EXISTS EmploymentHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AdvisorId INTEGER NOT NULL,
                FirmName TEXT NOT NULL,
                FirmCrd TEXT,
                StartDate TEXT,
                EndDate TEXT,
                Position TEXT,
                FOREIGN KEY (AdvisorId) REFERENCES Advisors(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Disclosures (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AdvisorId INTEGER NOT NULL,
                Type TEXT NOT NULL,
                Description TEXT,
                Date TEXT,
                Resolution TEXT,
                Sanctions TEXT,
                Source TEXT,
                FOREIGN KEY (AdvisorId) REFERENCES Advisors(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Qualifications (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AdvisorId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Code TEXT,
                Date TEXT,
                Status TEXT,
                FOREIGN KEY (AdvisorId) REFERENCES Advisors(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_advisors_lastname ON Advisors(LastName);
            CREATE INDEX IF NOT EXISTS idx_advisors_state ON Advisors(State);
            CREATE INDEX IF NOT EXISTS idx_advisors_firm ON Advisors(CurrentFirmCrd);
            CREATE INDEX IF NOT EXISTS idx_advisors_crd ON Advisors(CrdNumber);

            CREATE TABLE IF NOT EXISTS AdvisorLists (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT,
                CreatedAt TEXT DEFAULT (datetime('now')),
                UpdatedAt TEXT DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS AdvisorListMembers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ListId INTEGER NOT NULL,
                AdvisorId INTEGER NOT NULL,
                Notes TEXT,
                AddedAt TEXT DEFAULT (datetime('now')),
                FOREIGN KEY (ListId) REFERENCES AdvisorLists(Id) ON DELETE CASCADE,
                FOREIGN KEY (AdvisorId) REFERENCES Advisors(Id) ON DELETE CASCADE,
                UNIQUE(ListId, AdvisorId)
            );
            CREATE INDEX IF NOT EXISTS idx_listmembers_list ON AdvisorListMembers(ListId);
            CREATE INDEX IF NOT EXISTS idx_listmembers_advisor ON AdvisorListMembers(AdvisorId);
        ";
        cmd.ExecuteNonQuery();

        MigrateDatabase();
    }

    // Adds columns introduced after initial release to existing databases,
    // then creates any indices that depend on those new columns.
    private void MigrateDatabase()
    {
        var conn = GetConnection();
        TryAddColumn(conn, "Advisors", "RecordType", "TEXT");
        TryAddColumn(conn, "Firms", "RecordType", "TEXT");

        // Advisor new columns
        TryAddColumn(conn, "Advisors", "Suffix", "TEXT");
        TryAddColumn(conn, "Advisors", "IapdLink", "TEXT");
        TryAddColumn(conn, "Advisors", "RegAuthorities", "TEXT");
        TryAddColumn(conn, "Advisors", "DisclosureFlags", "TEXT");
        TryAddColumn(conn, "Advisors", "OtherNames", "TEXT");

        // EmploymentHistory new columns
        TryAddColumn(conn, "EmploymentHistory", "Street", "TEXT");

        // Firm new columns
        TryAddColumn(conn, "Firms", "SECNumber", "TEXT");
        TryAddColumn(conn, "Firms", "SECRegion", "TEXT");
        TryAddColumn(conn, "Firms", "LegalName", "TEXT");
        TryAddColumn(conn, "Firms", "FaxPhone", "TEXT");
        TryAddColumn(conn, "Firms", "MailingAddress", "TEXT");
        TryAddColumn(conn, "Firms", "RegistrationStatus", "TEXT");
        TryAddColumn(conn, "Firms", "AumDescription", "TEXT");
        TryAddColumn(conn, "Firms", "StateOfOrganization", "TEXT");
        TryAddColumn(conn, "Firms", "Country", "TEXT");
        TryAddColumn(conn, "Firms", "NumberOfEmployees", "INTEGER");
        TryAddColumn(conn, "Firms", "LatestFilingDate", "TEXT");

        // Create the RecordType index now that the column is guaranteed to exist.
        TryCreateIndex(conn, "idx_advisors_recordtype", "CREATE INDEX IF NOT EXISTS idx_advisors_recordtype ON Advisors(RecordType)");

        // Composite covering index for the most common recruiter query:
        // State + RecordType + RegistrationStatus + LastName
        TryCreateIndex(conn, "idx_adv_state_type_status_name",
            "CREATE INDEX IF NOT EXISTS idx_adv_state_type_status_name ON Advisors(State, RecordType, RegistrationStatus, LastName) WHERE IsExcluded = 0");

        // Source filter (normalised searches benefit from this)
        TryCreateIndex(conn, "idx_adv_source",
            "CREATE INDEX IF NOT EXISTS idx_adv_source ON Advisors(Source)");

        // Partial index for advisors with disclosures (low cardinality, high selectivity)
        TryCreateIndex(conn, "idx_adv_disclosures",
            "CREATE INDEX IF NOT EXISTS idx_adv_disclosures ON Advisors(HasDisclosures) WHERE HasDisclosures = 1");

        // Partial index for advisors not yet imported to CRM
        TryCreateIndex(conn, "idx_adv_crm",
            "CREATE INDEX IF NOT EXISTS idx_adv_crm ON Advisors(IsImportedToCrm) WHERE IsImportedToCrm = 0");

        // Covering index for active-only queries (skips excluded rows at index level)
        TryCreateIndex(conn, "idx_adv_active_state_type",
            "CREATE INDEX IF NOT EXISTS idx_adv_active_state_type ON Advisors(State, RecordType, LastName) WHERE IsExcluded = 0");

        // Related data lookup indices (used by N+1 fix and detail view)
        TryCreateIndex(conn, "idx_emp_advisor",
            "CREATE INDEX IF NOT EXISTS idx_emp_advisor ON EmploymentHistory(AdvisorId)");
        TryCreateIndex(conn, "idx_disc_advisor",
            "CREATE INDEX IF NOT EXISTS idx_disc_advisor ON Disclosures(AdvisorId)");
        TryCreateIndex(conn, "idx_qual_advisor",
            "CREATE INDEX IF NOT EXISTS idx_qual_advisor ON Qualifications(AdvisorId)");

        // Firm lookup index
        TryCreateIndex(conn, "idx_firms_state_status",
            "CREATE INDEX IF NOT EXISTS idx_firms_state_status ON Firms(State, RegistrationStatus, Name)");

        // List member lookups
        TryCreateIndex(conn, "idx_listmembers_list",
            "CREATE INDEX IF NOT EXISTS idx_listmembers_list ON AdvisorListMembers(ListId)");
        TryCreateIndex(conn, "idx_listmembers_advisor",
            "CREATE INDEX IF NOT EXISTS idx_listmembers_advisor ON AdvisorListMembers(AdvisorId)");
    }

    private static void TryAddColumn(SqliteConnection conn, string table, string column, string type)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {type}";
            cmd.ExecuteNonQuery();
        }
        catch { /* column already exists — ignore */ }
    }

    private static void TryCreateIndex(SqliteConnection conn, string indexName, string createSql)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = createSql;
            cmd.ExecuteNonQuery();
        }
        catch { /* index already exists or column missing — ignore */ }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _dbLock.Dispose();
    }

    /// <summary>
    /// Executes a synchronous database operation under the DB lock, preventing
    /// concurrent access from the UI thread and background service threads.
    /// </summary>
    public T Execute<T>(Func<SqliteConnection, T> work)
    {
        _dbLock.Wait();
        try { return work(GetConnection()); }
        finally { _dbLock.Release(); }
    }

    /// <summary>
    /// Async version — awaits the lock so the calling thread is not blocked.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<SqliteConnection, T> work)
    {
        await _dbLock.WaitAsync().ConfigureAwait(false);
        try { return work(GetConnection()); }
        finally { _dbLock.Release(); }
    }

    /// <summary>
    /// Deletes all rows from every table and resets autoincrement counters.
    /// Used by the debug reset flow.
    /// </summary>
    public void ClearAllData()
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM Qualifications;
            DELETE FROM Disclosures;
            DELETE FROM EmploymentHistory;
            DELETE FROM Advisors;
            DELETE FROM Firms;
            DELETE FROM AdvisorListMembers;
            DELETE FROM AdvisorLists;
            DELETE FROM sqlite_sequence WHERE name IN ('AdvisorListMembers','AdvisorLists','Qualifications','Disclosures','EmploymentHistory','Advisors','Firms');
        ";
        cmd.ExecuteNonQuery();
    }
}
