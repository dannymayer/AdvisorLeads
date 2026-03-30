using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AdvisorLeads", "advisorleads.db");
Console.WriteLine("DB Path: " + dbPath);
using var conn = new SqliteConnection("Data Source=" + dbPath + ";Mode=ReadOnly");
conn.Open();

void Query(string sql)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    using var reader = cmd.ExecuteReader();
    var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i)).ToList();
    Console.WriteLine(string.Join(" | ", cols));
    Console.WriteLine(new string('-', 120));
    while (reader.Read())
    {
        var vals = Enumerable.Range(0, reader.FieldCount)
            .Select(i => reader.IsDBNull(i) ? "NULL" : (reader.GetValue(i)?.ToString() ?? "NULL"))
            .ToList();
        Console.WriteLine(string.Join(" | ", vals));
    }
    Console.WriteLine();
}

Console.WriteLine("=== Total Advisors ===");
Query("SELECT COUNT(*) as Total FROM Advisors");

Console.WriteLine("=== Schlesser records ===");
Query("SELECT Id, FirstName, LastName, CrdNumber, Source, IsExcluded, RegistrationStatus, RecordType FROM Advisors WHERE LastName LIKE '%Schlesser%' LIMIT 20");

Console.WriteLine("=== Names starting with Sch ===");
Query("SELECT Id, FirstName, LastName, CrdNumber, Source, IsExcluded, RegistrationStatus, RecordType FROM Advisors WHERE LastName LIKE 'Sch%' LIMIT 20");

Console.WriteLine("=== Distinct RegistrationStatus values ===");
Query("SELECT RegistrationStatus, COUNT(*) as Cnt FROM Advisors GROUP BY RegistrationStatus ORDER BY Cnt DESC LIMIT 20");

Console.WriteLine("=== Distinct RecordType values ===");
Query("SELECT RecordType, COUNT(*) as Cnt FROM Advisors GROUP BY RecordType ORDER BY Cnt DESC LIMIT 20");

Console.WriteLine("=== Distinct Source values ===");
Query("SELECT Source, COUNT(*) as Cnt FROM Advisors GROUP BY Source ORDER BY Cnt DESC LIMIT 20");

Console.WriteLine("=== Excluded count ===");
Query("SELECT IsExcluded, COUNT(*) as Cnt FROM Advisors GROUP BY IsExcluded");

Console.WriteLine("=== Records per first letter of LastName ===");
Query("SELECT UPPER(SUBSTR(LastName, 1, 1)) as Letter, COUNT(*) as Cnt FROM Advisors GROUP BY Letter ORDER BY Cnt DESC");

Console.WriteLine("=== Records per Sch prefix ===");
Query("SELECT SUBSTR(LastName, 1, 4) as Prefix, COUNT(*) as Cnt FROM Advisors WHERE LastName LIKE 'Sch%' GROUP BY Prefix ORDER BY Cnt DESC LIMIT 30");

Console.WriteLine("=== All Schl names ===");
Query("SELECT DISTINCT LastName FROM Advisors WHERE LastName LIKE 'Schl%' ORDER BY LastName");

Console.WriteLine("=== YearsOfExperience null count ===");
Query("SELECT COUNT(*) as NullYears FROM Advisors WHERE YearsOfExperience IS NULL");

Console.WriteLine("=== Sample advisors with null YearsOfExperience ===");
Query("SELECT Id, LastName, FirstName, YearsOfExperience, RegistrationStatus FROM Advisors WHERE YearsOfExperience IS NULL LIMIT 5");

Console.WriteLine("=== YearsOfExperience distribution ===");
Query("SELECT YearsOfExperience, COUNT(*) as Cnt FROM Advisors GROUP BY YearsOfExperience ORDER BY YearsOfExperience LIMIT 30");
