using Microsoft.Data.Sqlite;

namespace AdvisorLeads.Data;

public class DatabaseContext : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

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
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
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
                IsExcluded INTEGER DEFAULT 0,
                CreatedAt TEXT DEFAULT (datetime('now')),
                UpdatedAt TEXT DEFAULT (datetime('now'))
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
                IsExcluded INTEGER DEFAULT 0,
                ExclusionReason TEXT,
                IsImportedToCrm INTEGER DEFAULT 0,
                CrmId TEXT,
                CreatedAt TEXT DEFAULT (datetime('now')),
                UpdatedAt TEXT DEFAULT (datetime('now')),
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
        ";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
