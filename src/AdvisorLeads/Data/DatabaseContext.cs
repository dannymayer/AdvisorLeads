using Npgsql;

namespace AdvisorLeads.Data;

public class DatabaseContext : IDisposable
{
    private readonly string _connectionString;
    private readonly NpgsqlDataSource _dataSource;

    public DatabaseContext(string connectionString)
    {
        _connectionString = connectionString;
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _dataSource = builder.Build();
    }

    public NpgsqlConnection GetConnection()
    {
        var conn = _dataSource.OpenConnection();
        return conn;
    }

    public void InitializeDatabase()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Firms (
                Id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                CrdNumber TEXT UNIQUE,
                Name TEXT NOT NULL,
                Address TEXT,
                City TEXT,
                State TEXT,
                ZipCode TEXT,
                Phone TEXT,
                Website TEXT,
                BusinessType TEXT,
                IsRegisteredWithSec BOOLEAN DEFAULT FALSE,
                IsRegisteredWithFinra BOOLEAN DEFAULT FALSE,
                NumberOfAdvisors INTEGER,
                RegistrationDate DATE,
                Source TEXT,
                RecordType TEXT,
                IsExcluded BOOLEAN DEFAULT FALSE,
                CreatedAt TIMESTAMP DEFAULT NOW(),
                UpdatedAt TIMESTAMP DEFAULT NOW(),
                SECNumber TEXT,
                SECRegion TEXT,
                LegalName TEXT,
                FaxPhone TEXT,
                MailingAddress TEXT,
                RegistrationStatus TEXT,
                AumDescription TEXT,
                StateOfOrganization TEXT,
                Country TEXT,
                NumberOfEmployees INTEGER,
                LatestFilingDate TEXT,
                RegulatoryAum NUMERIC,
                RegulatoryAumNonDiscretionary NUMERIC,
                NumClients INTEGER,
                BrokerProtocolMember BOOLEAN DEFAULT FALSE,
                BrokerProtocolUpdatedAt TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS Advisors (
                Id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
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
                CurrentFirmId INTEGER REFERENCES Firms(Id),
                RegistrationStatus TEXT,
                RegistrationDate DATE,
                YearsOfExperience INTEGER,
                HasDisclosures BOOLEAN DEFAULT FALSE,
                DisclosureCount INTEGER DEFAULT 0,
                Source TEXT,
                RecordType TEXT,
                IsExcluded BOOLEAN DEFAULT FALSE,
                ExclusionReason TEXT,
                IsImportedToCrm BOOLEAN DEFAULT FALSE,
                CrmId TEXT,
                CreatedAt TIMESTAMP DEFAULT NOW(),
                UpdatedAt TIMESTAMP DEFAULT NOW(),
                Suffix TEXT,
                IapdLink TEXT,
                RegAuthorities TEXT,
                DisclosureFlags TEXT,
                OtherNames TEXT
            );

            CREATE TABLE IF NOT EXISTS EmploymentHistory (
                Id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                AdvisorId INTEGER NOT NULL REFERENCES Advisors(Id) ON DELETE CASCADE,
                FirmName TEXT NOT NULL,
                FirmCrd TEXT,
                StartDate DATE,
                EndDate DATE,
                Position TEXT,
                Street TEXT
            );

            CREATE TABLE IF NOT EXISTS Disclosures (
                Id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                AdvisorId INTEGER NOT NULL REFERENCES Advisors(Id) ON DELETE CASCADE,
                Type TEXT NOT NULL,
                Description TEXT,
                Date DATE,
                Resolution TEXT,
                Sanctions TEXT,
                Source TEXT
            );

            CREATE TABLE IF NOT EXISTS Qualifications (
                Id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                AdvisorId INTEGER NOT NULL REFERENCES Advisors(Id) ON DELETE CASCADE,
                Name TEXT NOT NULL,
                Code TEXT,
                Date DATE,
                Status TEXT
            );

            CREATE TABLE IF NOT EXISTS AdvisorLists (
                Id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT,
                CreatedAt TIMESTAMP DEFAULT NOW(),
                UpdatedAt TIMESTAMP DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS AdvisorListMembers (
                Id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                ListId INTEGER NOT NULL REFERENCES AdvisorLists(Id) ON DELETE CASCADE,
                AdvisorId INTEGER NOT NULL REFERENCES Advisors(Id) ON DELETE CASCADE,
                Notes TEXT,
                AddedAt TIMESTAMP DEFAULT NOW(),
                UNIQUE(ListId, AdvisorId)
            );
        ";
        cmd.ExecuteNonQuery();

        CreateIndexes(conn);
    }

    private static void CreateIndexes(NpgsqlConnection conn)
    {
        var indexes = new[]
        {
            "CREATE INDEX IF NOT EXISTS idx_advisors_lastname ON Advisors(LastName)",
            "CREATE INDEX IF NOT EXISTS idx_advisors_state ON Advisors(State)",
            "CREATE INDEX IF NOT EXISTS idx_advisors_firm ON Advisors(CurrentFirmCrd)",
            "CREATE INDEX IF NOT EXISTS idx_advisors_crd ON Advisors(CrdNumber)",
            "CREATE INDEX IF NOT EXISTS idx_advisors_recordtype ON Advisors(RecordType)",
            "CREATE INDEX IF NOT EXISTS idx_adv_state_type_status_name ON Advisors(State, RecordType, RegistrationStatus, LastName) WHERE IsExcluded = FALSE",
            "CREATE INDEX IF NOT EXISTS idx_adv_source ON Advisors(Source)",
            "CREATE INDEX IF NOT EXISTS idx_adv_disclosures ON Advisors(HasDisclosures) WHERE HasDisclosures = TRUE",
            "CREATE INDEX IF NOT EXISTS idx_adv_crm ON Advisors(IsImportedToCrm) WHERE IsImportedToCrm = FALSE",
            "CREATE INDEX IF NOT EXISTS idx_adv_active_state_type ON Advisors(State, RecordType, LastName) WHERE IsExcluded = FALSE",
            "CREATE INDEX IF NOT EXISTS idx_emp_advisor ON EmploymentHistory(AdvisorId)",
            "CREATE INDEX IF NOT EXISTS idx_disc_advisor ON Disclosures(AdvisorId)",
            "CREATE INDEX IF NOT EXISTS idx_qual_advisor ON Qualifications(AdvisorId)",
            "CREATE INDEX IF NOT EXISTS idx_firms_state_status ON Firms(State, RegistrationStatus, Name)",
            "CREATE INDEX IF NOT EXISTS idx_listmembers_list ON AdvisorListMembers(ListId)",
            "CREATE INDEX IF NOT EXISTS idx_listmembers_advisor ON AdvisorListMembers(AdvisorId)"
        };

        foreach (var sql in indexes)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        _dataSource.Dispose();
    }

    /// <summary>
    /// Executes a synchronous database operation using a pooled connection.
    /// </summary>
    public T Execute<T>(Func<NpgsqlConnection, T> work)
    {
        using var conn = GetConnection();
        return work(conn);
    }

    /// <summary>
    /// Executes an async database operation using a pooled connection.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<NpgsqlConnection, T> work)
    {
        await using var conn = await _dataSource.OpenConnectionAsync().ConfigureAwait(false);
        return work(conn);
    }

    /// <summary>
    /// Deletes all rows from every table and resets identity sequences.
    /// Used by the debug reset flow.
    /// </summary>
    public void ClearAllData()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            TRUNCATE TABLE Qualifications, Disclosures, EmploymentHistory,
                           AdvisorListMembers, AdvisorLists, Advisors, Firms
            RESTART IDENTITY CASCADE;
        ";
        cmd.ExecuteNonQuery();
    }
}
