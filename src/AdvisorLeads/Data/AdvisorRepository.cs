using Microsoft.Data.Sqlite;
using AdvisorLeads.Models;
using System.Text;

namespace AdvisorLeads.Data;

public class AdvisorRepository
{
    private readonly DatabaseContext _context;

    public AdvisorRepository(DatabaseContext context)
    {
        _context = context;
    }

    // ── Advisors ──────────────────────────────────────────────────────────

    public List<Advisor> GetAdvisors(SearchFilter filter)
    {
        var conn = _context.GetConnection();
        var sb = new StringBuilder(@"
            SELECT Id, CrdNumber, IapdNumber, FirstName, LastName, MiddleName,
                   Title, Email, Phone, City, State, ZipCode, Licenses, Qualifications,
                   CurrentFirmName, CurrentFirmCrd, CurrentFirmId, RegistrationStatus,
                   RegistrationDate, YearsOfExperience, HasDisclosures, DisclosureCount,
                   Source, IsExcluded, ExclusionReason, IsImportedToCrm, CrmId,
                   CreatedAt, UpdatedAt, RecordType, Suffix, IapdLink, RegAuthorities, DisclosureFlags, OtherNames
            FROM Advisors WHERE 1=1");

        var parameters = new List<(string name, object value)>();

        if (!filter.IncludeExcluded)
        {
            sb.Append(" AND IsExcluded = 0");
        }

        if (!string.IsNullOrWhiteSpace(filter.NameQuery))
        {
            sb.Append(" AND (FirstName LIKE @name OR LastName LIKE @name OR (FirstName || ' ' || LastName) LIKE @name)");
            parameters.Add(("@name", $"%{filter.NameQuery}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.State))
        {
            sb.Append(" AND State = @state");
            parameters.Add(("@state", filter.State));
        }

        if (!string.IsNullOrWhiteSpace(filter.FirmName))
        {
            sb.Append(" AND CurrentFirmName LIKE @firmName");
            parameters.Add(("@firmName", $"%{filter.FirmName}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.FirmCrd))
        {
            sb.Append(" AND CurrentFirmCrd = @firmCrd");
            parameters.Add(("@firmCrd", filter.FirmCrd));
        }

        if (!string.IsNullOrWhiteSpace(filter.CrdNumber))
        {
            sb.Append(" AND CrdNumber = @crd");
            parameters.Add(("@crd", filter.CrdNumber));
        }

        if (!string.IsNullOrWhiteSpace(filter.RegistrationStatus))
        {
            // Use LIKE so "Active" matches "Active", "Inactive" etc. correctly
            sb.Append(" AND RegistrationStatus LIKE @status");
            parameters.Add(("@status", filter.RegistrationStatus));
        }

        if (!string.IsNullOrWhiteSpace(filter.LicenseType))
        {
            sb.Append(" AND Licenses LIKE @license");
            parameters.Add(("@license", $"%{filter.LicenseType}%"));
        }

        if (filter.HasDisclosures.HasValue)
        {
            sb.Append(" AND HasDisclosures = @hasDisc");
            parameters.Add(("@hasDisc", filter.HasDisclosures.Value ? 1 : 0));
        }

        if (filter.IsImportedToCrm.HasValue)
        {
            sb.Append(" AND IsImportedToCrm = @imported");
            parameters.Add(("@imported", filter.IsImportedToCrm.Value ? 1 : 0));
        }

        if (!string.IsNullOrWhiteSpace(filter.Source))
        {
            // "Both" means the record appears in both FINRA and SEC — order may vary
            if (filter.Source.Equals("Both", StringComparison.OrdinalIgnoreCase)
                || filter.Source.Contains(','))
            {
                sb.Append(" AND Source LIKE '%FINRA%' AND Source LIKE '%SEC%'");
            }
            else
            {
                sb.Append(" AND Source LIKE @source");
                parameters.Add(("@source", $"%{filter.Source}%"));
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.RecordType))
        {
            sb.Append(" AND RecordType = @recordType");
            parameters.Add(("@recordType", filter.RecordType));
        }

        if (filter.MinYearsExperience.HasValue)
        {
            sb.Append(" AND YearsOfExperience >= @minYears");
            parameters.Add(("@minYears", filter.MinYearsExperience.Value));
        }

        if (filter.MaxYearsExperience.HasValue)
        {
            sb.Append(" AND YearsOfExperience <= @maxYears");
            parameters.Add(("@maxYears", filter.MaxYearsExperience.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            sb.Append(" AND City LIKE @city");
            parameters.Add(("@city", $"%{filter.City}%"));
        }

        if (filter.MinDisclosureCount.HasValue && filter.MinDisclosureCount.Value > 0)
        {
            sb.Append(" AND DisclosureCount >= @minDisc");
            parameters.Add(("@minDisc", filter.MinDisclosureCount.Value));
        }

        var sortCol = filter.SortBy switch
        {
            "FirstName" => "FirstName",
            "State" => "State",
            "CurrentFirmName" => "CurrentFirmName",
            "RegistrationStatus" => "RegistrationStatus",
            "RecordType" => "RecordType",
            "YearsOfExperience" => "YearsOfExperience",
            "UpdatedAt" => "UpdatedAt",
            _ => "LastName"
        };
        sb.Append($" ORDER BY {sortCol} {(filter.SortDescending ? "DESC" : "ASC")}");
        sb.Append(" LIMIT @pageSize OFFSET @offset");
        parameters.Add(("@pageSize", filter.PageSize));
        parameters.Add(("@offset", (filter.PageNumber - 1) * filter.PageSize));

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sb.ToString();
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);

        var advisors = new List<Advisor>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            advisors.Add(MapAdvisor(reader));

        return advisors;
    }

    public Advisor? GetAdvisorById(int id)
    {
        var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CrdNumber, IapdNumber, FirstName, LastName, MiddleName,
                   Title, Email, Phone, City, State, ZipCode, Licenses, Qualifications,
                   CurrentFirmName, CurrentFirmCrd, CurrentFirmId, RegistrationStatus,
                   RegistrationDate, YearsOfExperience, HasDisclosures, DisclosureCount,
                   Source, IsExcluded, ExclusionReason, IsImportedToCrm, CrmId,
                   CreatedAt, UpdatedAt, RecordType, Suffix, IapdLink, RegAuthorities, DisclosureFlags, OtherNames
            FROM Advisors WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var advisor = MapAdvisor(reader);
            reader.Close();
            LoadRelatedData(advisor);
            return advisor;
        }
        return null;
    }

    public Advisor? GetAdvisorByCrd(string crd)
    {
        var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Advisors WHERE CrdNumber = @crd";
        cmd.Parameters.AddWithValue("@crd", crd);
        var id = cmd.ExecuteScalar();
        if (id != null && id != DBNull.Value)
            return GetAdvisorById(Convert.ToInt32(id));
        return null;
    }

    public int UpsertAdvisor(Advisor advisor)
    {
        var conn = _context.GetConnection();

        // Check for existing by CRD
        int existingId = 0;
        if (!string.IsNullOrEmpty(advisor.CrdNumber))
        {
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT Id FROM Advisors WHERE CrdNumber = @crd";
            checkCmd.Parameters.AddWithValue("@crd", advisor.CrdNumber);
            var result = checkCmd.ExecuteScalar();
            if (result != null && result != DBNull.Value)
                existingId = Convert.ToInt32(result);
        }

        if (existingId > 0)
        {
            advisor.Id = existingId;
            UpdateAdvisor(advisor);
        }
        else
        {
            InsertAdvisor(advisor);
        }

        // Upsert related data
        if (advisor.EmploymentHistory.Count > 0)
            UpsertEmploymentHistory(advisor.Id, advisor.EmploymentHistory);
        if (advisor.Disclosures.Count > 0)
            UpsertDisclosures(advisor.Id, advisor.Disclosures);
        if (advisor.QualificationList.Count > 0)
            UpsertQualifications(advisor.Id, advisor.QualificationList);

        return advisor.Id;
    }

    private void InsertAdvisor(Advisor a)
    {
        var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Advisors (CrdNumber, IapdNumber, FirstName, LastName, MiddleName,
                Title, Email, Phone, City, State, ZipCode, Licenses, Qualifications,
                CurrentFirmName, CurrentFirmCrd, CurrentFirmId, RegistrationStatus,
                RegistrationDate, YearsOfExperience, HasDisclosures, DisclosureCount,
                Source, RecordType, IsExcluded, ExclusionReason, IsImportedToCrm, CrmId,
                Suffix, IapdLink, RegAuthorities, DisclosureFlags, OtherNames, UpdatedAt)
            VALUES (@crd, @iapd, @first, @last, @middle, @title, @email, @phone,
                @city, @state, @zip, @licenses, @quals, @firmName, @firmCrd, @firmId,
                @status, @regDate, @years, @discs, @discCount, @source, @recordType,
                0, NULL, 0, NULL, @suffix, @iapdLink, @regAuths, @discFlags, @otherNames, datetime('now'));
            SELECT last_insert_rowid();";
        BindAdvisorParams(cmd, a);
        a.Id = Convert.ToInt32(cmd.ExecuteScalar());
    }

    private void UpdateAdvisor(Advisor a)
    {
        var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Advisors SET
                IapdNumber          = COALESCE(NULLIF(@iapd, ''), IapdNumber),
                FirstName           = @first,
                LastName            = @last,
                MiddleName          = COALESCE(@middle, MiddleName),
                Title               = COALESCE(@title, Title),
                Email               = COALESCE(@email, Email),
                Phone               = COALESCE(@phone, Phone),
                City                = COALESCE(NULLIF(@city, ''), City),
                State               = COALESCE(NULLIF(@state, ''), State),
                ZipCode             = COALESCE(NULLIF(@zip, ''), ZipCode),
                Licenses            = COALESCE(NULLIF(@licenses, ''), Licenses),
                Qualifications      = COALESCE(NULLIF(@quals, ''), Qualifications),
                CurrentFirmName     = COALESCE(NULLIF(@firmName, ''), CurrentFirmName),
                CurrentFirmCrd      = COALESCE(NULLIF(@firmCrd, ''), CurrentFirmCrd),
                CurrentFirmId       = COALESCE(@firmId, CurrentFirmId),
                RegistrationStatus  = COALESCE(NULLIF(@status, ''), RegistrationStatus),
                RegistrationDate    = COALESCE(NULLIF(@regDate, ''), RegistrationDate),
                YearsOfExperience   = COALESCE(@years, YearsOfExperience),
                HasDisclosures      = MAX(HasDisclosures, @discs),
                DisclosureCount     = MAX(DisclosureCount, @discCount),
                Source              = CASE
                    WHEN @source IS NULL OR @source = '' THEN Source
                    WHEN Source IS NULL OR Source = '' THEN @source
                    WHEN ',' || Source || ',' LIKE '%,' || @source || ',%' THEN Source
                    ELSE Source || ',' || @source
                  END,
                RecordType          = COALESCE(NULLIF(@recordType, ''), RecordType),
                Suffix              = COALESCE(@suffix, Suffix),
                IapdLink            = COALESCE(@iapdLink, IapdLink),
                RegAuthorities      = COALESCE(NULLIF(@regAuths, ''), RegAuthorities),
                DisclosureFlags     = COALESCE(NULLIF(@discFlags, ''), DisclosureFlags),
                OtherNames          = COALESCE(NULLIF(@otherNames, ''), OtherNames),
                UpdatedAt           = datetime('now')
            WHERE Id = @id";
        BindAdvisorParams(cmd, a);
        cmd.Parameters.AddWithValue("@id", a.Id);
        cmd.ExecuteNonQuery();
    }

    private static void BindAdvisorParams(SqliteCommand cmd, Advisor a)
    {
        cmd.Parameters.AddWithValue("@crd", (object?)a.CrdNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@iapd", (object?)a.IapdNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@first", a.FirstName);
        cmd.Parameters.AddWithValue("@last", a.LastName);
        cmd.Parameters.AddWithValue("@middle", (object?)a.MiddleName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@title", (object?)a.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@email", (object?)a.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@phone", (object?)a.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@city", (object?)a.City ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@state", (object?)a.State ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@zip", (object?)a.ZipCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@licenses", (object?)a.Licenses ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@quals", (object?)a.Qualifications ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@firmName", (object?)a.CurrentFirmName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@firmCrd", (object?)a.CurrentFirmCrd ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@firmId", (object?)a.CurrentFirmId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@status", (object?)a.RegistrationStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@regDate", a.RegistrationDate.HasValue ? a.RegistrationDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@years", (object?)a.YearsOfExperience ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@discs", a.HasDisclosures ? 1 : 0);
        cmd.Parameters.AddWithValue("@discCount", a.DisclosureCount);
        cmd.Parameters.AddWithValue("@source", (object?)a.Source ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@recordType", (object?)a.RecordType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@suffix", (object?)a.Suffix ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@iapdLink", (object?)a.IapdLink ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@regAuths", (object?)a.RegAuthorities ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@discFlags", (object?)a.DisclosureFlags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@otherNames", (object?)a.OtherNames ?? DBNull.Value);
    }

    public void SetAdvisorExcluded(int id, bool excluded, string? reason = null)
    {
        var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Advisors SET IsExcluded = @excluded, ExclusionReason = @reason, UpdatedAt = datetime('now') WHERE Id = @id";
        cmd.Parameters.AddWithValue("@excluded", excluded ? 1 : 0);
        cmd.Parameters.AddWithValue("@reason", (object?)reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetAdvisorImported(int id, string? crmId)
    {
        var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Advisors SET IsImportedToCrm = 1, CrmId = @crmId, UpdatedAt = datetime('now') WHERE Id = @id";
        cmd.Parameters.AddWithValue("@crmId", (object?)crmId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    private void LoadRelatedData(Advisor advisor)
    {
        var conn = _context.GetConnection();

        using var empCmd = conn.CreateCommand();
        empCmd.CommandText = "SELECT Id, AdvisorId, FirmName, FirmCrd, StartDate, EndDate, Position, Street FROM EmploymentHistory WHERE AdvisorId = @id ORDER BY StartDate DESC";
        empCmd.Parameters.AddWithValue("@id", advisor.Id);
        using (var r = empCmd.ExecuteReader())
        {
            while (r.Read())
            {
                advisor.EmploymentHistory.Add(new EmploymentHistory
                {
                    Id = r.GetInt32(0),
                    AdvisorId = r.GetInt32(1),
                    FirmName = r.GetString(2),
                    FirmCrd = r.IsDBNull(3) ? null : r.GetString(3),
                    StartDate = r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4)),
                    EndDate = r.IsDBNull(5) ? null : DateTime.Parse(r.GetString(5)),
                    Position = r.IsDBNull(6) ? null : r.GetString(6),
                    Street = r.FieldCount > 7 && !r.IsDBNull(7) ? r.GetString(7) : null
                });
            }
        }

        using var discCmd = conn.CreateCommand();
        discCmd.CommandText = "SELECT Id, AdvisorId, Type, Description, Date, Resolution, Sanctions, Source FROM Disclosures WHERE AdvisorId = @id";
        discCmd.Parameters.AddWithValue("@id", advisor.Id);
        using (var r = discCmd.ExecuteReader())
        {
            while (r.Read())
            {
                advisor.Disclosures.Add(new Disclosure
                {
                    Id = r.GetInt32(0),
                    AdvisorId = r.GetInt32(1),
                    Type = r.GetString(2),
                    Description = r.IsDBNull(3) ? null : r.GetString(3),
                    Date = r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4)),
                    Resolution = r.IsDBNull(5) ? null : r.GetString(5),
                    Sanctions = r.IsDBNull(6) ? null : r.GetString(6),
                    Source = r.IsDBNull(7) ? null : r.GetString(7)
                });
            }
        }

        using var qualCmd = conn.CreateCommand();
        qualCmd.CommandText = "SELECT Id, AdvisorId, Name, Code, Date, Status FROM Qualifications WHERE AdvisorId = @id";
        qualCmd.Parameters.AddWithValue("@id", advisor.Id);
        using (var r = qualCmd.ExecuteReader())
        {
            while (r.Read())
            {
                advisor.QualificationList.Add(new Qualification
                {
                    Id = r.GetInt32(0),
                    AdvisorId = r.GetInt32(1),
                    Name = r.GetString(2),
                    Code = r.IsDBNull(3) ? null : r.GetString(3),
                    Date = r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4)),
                    Status = r.IsDBNull(5) ? null : r.GetString(5)
                });
            }
        }
    }

    private void UpsertEmploymentHistory(int advisorId, List<EmploymentHistory> history)
    {
        var conn = _context.GetConnection();

        // Load existing records so we can merge rather than replace
        var existing = new List<(int id, string firmName)>();
        using (var selCmd = conn.CreateCommand())
        {
            selCmd.CommandText = "SELECT Id, FirmName FROM EmploymentHistory WHERE AdvisorId = @id";
            selCmd.Parameters.AddWithValue("@id", advisorId);
            using var r = selCmd.ExecuteReader();
            while (r.Read())
                existing.Add((r.GetInt32(0), r.GetString(1)));
        }

        foreach (var h in history)
        {
            if (string.IsNullOrWhiteSpace(h.FirmName)) continue;

            var match = existing.FirstOrDefault(e =>
                string.Equals(e.firmName, h.FirmName, StringComparison.OrdinalIgnoreCase));

            if (match.id > 0)
            {
                // Update only if incoming record has richer date/position data
                bool hasNewStart = h.StartDate.HasValue;
                bool hasNewEnd = h.EndDate.HasValue && h.EndDate.Value != DateTime.MinValue;
                bool hasNewPos = !string.IsNullOrWhiteSpace(h.Position);
                bool hasNewCrd = !string.IsNullOrWhiteSpace(h.FirmCrd);

                if (hasNewStart || hasNewEnd || hasNewPos || hasNewCrd)
                {
                    using var updCmd = conn.CreateCommand();
                    updCmd.CommandText = @"
                        UPDATE EmploymentHistory SET
                            StartDate = CASE WHEN @start IS NOT NULL THEN @start ELSE StartDate END,
                            EndDate   = CASE WHEN @end   IS NOT NULL THEN @end   ELSE EndDate   END,
                            Position  = COALESCE(@pos, Position),
                            FirmCrd   = COALESCE(@crd, FirmCrd),
                            Street    = COALESCE(@street, Street)
                        WHERE Id = @id";
                    updCmd.Parameters.AddWithValue("@start", hasNewStart
                        ? h.StartDate!.Value.ToString("yyyy-MM-dd")
                        : (object)DBNull.Value);
                    updCmd.Parameters.AddWithValue("@end", hasNewEnd
                        ? h.EndDate!.Value.ToString("yyyy-MM-dd")
                        : (object)DBNull.Value);
                    updCmd.Parameters.AddWithValue("@pos", hasNewPos ? (object)h.Position! : DBNull.Value);
                    updCmd.Parameters.AddWithValue("@crd", hasNewCrd ? (object)h.FirmCrd! : DBNull.Value);
                    updCmd.Parameters.AddWithValue("@street", !string.IsNullOrWhiteSpace(h.Street) ? (object)h.Street! : DBNull.Value);
                    updCmd.Parameters.AddWithValue("@id", match.id);
                    updCmd.ExecuteNonQuery();
                }
            }
            else
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO EmploymentHistory (AdvisorId, FirmName, FirmCrd, StartDate, EndDate, Position, Street)
                    VALUES (@aid, @firm, @crd, @start, @end, @pos, @street)";
                cmd.Parameters.AddWithValue("@aid", advisorId);
                cmd.Parameters.AddWithValue("@firm", h.FirmName);
                cmd.Parameters.AddWithValue("@crd", (object?)h.FirmCrd ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@start", h.StartDate.HasValue
                    ? h.StartDate.Value.ToString("yyyy-MM-dd")
                    : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@end", (h.EndDate.HasValue && h.EndDate.Value != DateTime.MinValue)
                    ? h.EndDate.Value.ToString("yyyy-MM-dd")
                    : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@pos", (object?)h.Position ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@street", (object?)h.Street ?? DBNull.Value);
                cmd.ExecuteNonQuery();
                existing.Add((0, h.FirmName)); // prevent double-insert within same batch
            }
        }
    }

    private void UpsertDisclosures(int advisorId, List<Disclosure> disclosures)
    {
        var conn = _context.GetConnection();
        using var delCmd = conn.CreateCommand();
        delCmd.CommandText = "DELETE FROM Disclosures WHERE AdvisorId = @id";
        delCmd.Parameters.AddWithValue("@id", advisorId);
        delCmd.ExecuteNonQuery();

        foreach (var d in disclosures)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Disclosures (AdvisorId, Type, Description, Date, Resolution, Sanctions, Source)
                VALUES (@aid, @type, @desc, @date, @res, @sanc, @src)";
            cmd.Parameters.AddWithValue("@aid", advisorId);
            cmd.Parameters.AddWithValue("@type", d.Type);
            cmd.Parameters.AddWithValue("@desc", (object?)d.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@date", d.Date.HasValue ? d.Date.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@res", (object?)d.Resolution ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sanc", (object?)d.Sanctions ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@src", (object?)d.Source ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    private void UpsertQualifications(int advisorId, List<Qualification> qualifications)
    {
        var conn = _context.GetConnection();
        using var delCmd = conn.CreateCommand();
        delCmd.CommandText = "DELETE FROM Qualifications WHERE AdvisorId = @id";
        delCmd.Parameters.AddWithValue("@id", advisorId);
        delCmd.ExecuteNonQuery();

        foreach (var q in qualifications)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Qualifications (AdvisorId, Name, Code, Date, Status)
                VALUES (@aid, @name, @code, @date, @status)";
            cmd.Parameters.AddWithValue("@aid", advisorId);
            cmd.Parameters.AddWithValue("@name", q.Name);
            cmd.Parameters.AddWithValue("@code", (object?)q.Code ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@date", q.Date.HasValue ? q.Date.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@status", (object?)q.Status ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public List<string> GetCrdsNeedingEnrichment(int limit)
    {
        var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT a.CrdNumber FROM Advisors a
            WHERE a.CrdNumber IS NOT NULL
            AND a.RegistrationStatus = 'Active'
            AND NOT EXISTS (SELECT 1 FROM Qualifications q WHERE q.AdvisorId = a.Id)
            ORDER BY a.UpdatedAt ASC
            LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);
        var result = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) result.Add(r.GetString(0));
        return result;
    }

    // ── Firms ─────────────────────────────────────────────────────────────

    public List<Firm> GetFirms(FirmSearchFilter? filter = null)
    {
        filter ??= new FirmSearchFilter();

        var conn = _context.GetConnection();
        var sb = new StringBuilder(@"
            SELECT Id, CrdNumber, Name, Address, City, State, ZipCode, Phone, Website,
                   BusinessType, IsRegisteredWithSec, IsRegisteredWithFinra, NumberOfAdvisors,
                   RegistrationDate, Source, IsExcluded, CreatedAt, UpdatedAt, RecordType,
                   SECNumber, SECRegion, LegalName, FaxPhone, MailingAddress, RegistrationStatus,
                   AumDescription, StateOfOrganization
            FROM Firms WHERE IsExcluded = 0");

        var parameters = new List<(string name, object value)>();

        if (!string.IsNullOrWhiteSpace(filter.NameQuery))
        {
            sb.Append(" AND Name LIKE @name");
            parameters.Add(("@name", $"%{filter.NameQuery}%"));
        }
        if (!string.IsNullOrWhiteSpace(filter.State))
        {
            sb.Append(" AND State = @state");
            parameters.Add(("@state", filter.State));
        }
        if (!string.IsNullOrWhiteSpace(filter.RecordType))
        {
            sb.Append(" AND RecordType = @recordType");
            parameters.Add(("@recordType", filter.RecordType));
        }
        if (!string.IsNullOrWhiteSpace(filter.RegistrationStatus))
        {
            sb.Append(" AND RegistrationStatus LIKE @status");
            parameters.Add(("@status", $"%{filter.RegistrationStatus}%"));
        }
        if (filter.MinAdvisors.HasValue && filter.MinAdvisors.Value > 0)
        {
            sb.Append(" AND NumberOfAdvisors >= @minAdv");
            parameters.Add(("@minAdv", filter.MinAdvisors.Value));
        }

        var sortCol = filter.SortBy switch
        {
            "State" => "State",
            "NumberOfAdvisors" => "NumberOfAdvisors",
            "RegistrationDate" => "RegistrationDate",
            "UpdatedAt" => "UpdatedAt",
            _ => "Name"
        };
        sb.Append($" ORDER BY {sortCol} {(filter.SortDescending ? "DESC" : "ASC")}");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sb.ToString();
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);

        var firms = new List<Firm>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            firms.Add(MapFirm(reader));
        return firms;
    }

    public int UpsertFirm(Firm firm)
    {
        var conn = _context.GetConnection();

        int existingId = 0;
        if (!string.IsNullOrEmpty(firm.CrdNumber))
        {
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT Id FROM Firms WHERE CrdNumber = @crd";
            checkCmd.Parameters.AddWithValue("@crd", firm.CrdNumber);
            var result = checkCmd.ExecuteScalar();
            if (result != null && result != DBNull.Value)
                existingId = Convert.ToInt32(result);
        }

        if (existingId > 0)
        {
            firm.Id = existingId;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Firms SET Name=@name, Address=@addr, City=@city, State=@state, ZipCode=@zip,
                    Phone=@phone, Website=@web, BusinessType=@btype, IsRegisteredWithSec=@sec,
                    IsRegisteredWithFinra=@finra, NumberOfAdvisors=@numAdv, RegistrationDate=@regDate,
                    Source=@source, RecordType=@recordType,
                    SECNumber=@secNum, SECRegion=@secRgn, LegalName=@legalNm, FaxPhone=@faxPhone,
                    MailingAddress=@mailAddr, RegistrationStatus=@regStatus,
                    AumDescription=@aumDesc, StateOfOrganization=@stateOrg,
                    UpdatedAt=datetime('now')
                WHERE Id = @id";
            BindFirmParams(cmd, firm);
            cmd.Parameters.AddWithValue("@id", firm.Id);
            cmd.ExecuteNonQuery();
        }
        else
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Firms (CrdNumber, Name, Address, City, State, ZipCode, Phone, Website,
                    BusinessType, IsRegisteredWithSec, IsRegisteredWithFinra, NumberOfAdvisors,
                    RegistrationDate, Source, RecordType,
                    SECNumber, SECRegion, LegalName, FaxPhone, MailingAddress,
                    RegistrationStatus, AumDescription, StateOfOrganization, UpdatedAt)
                VALUES (@crd, @name, @addr, @city, @state, @zip, @phone, @web,
                    @btype, @sec, @finra, @numAdv, @regDate, @source, @recordType,
                    @secNum, @secRgn, @legalNm, @faxPhone, @mailAddr,
                    @regStatus, @aumDesc, @stateOrg, datetime('now'));
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@crd", (object?)firm.CrdNumber ?? DBNull.Value);
            BindFirmParams(cmd, firm);
            firm.Id = Convert.ToInt32(cmd.ExecuteScalar());
        }

        return firm.Id;
    }

    private static void BindFirmParams(SqliteCommand cmd, Firm f)
    {
        cmd.Parameters.AddWithValue("@name", f.Name);
        cmd.Parameters.AddWithValue("@addr", (object?)f.Address ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@city", (object?)f.City ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@state", (object?)f.State ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@zip", (object?)f.ZipCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@phone", (object?)f.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@web", (object?)f.Website ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@btype", (object?)f.BusinessType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sec", f.IsRegisteredWithSec ? 1 : 0);
        cmd.Parameters.AddWithValue("@finra", f.IsRegisteredWithFinra ? 1 : 0);
        cmd.Parameters.AddWithValue("@numAdv", (object?)f.NumberOfAdvisors ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@regDate", f.RegistrationDate.HasValue ? f.RegistrationDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@source", (object?)f.Source ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@recordType", (object?)f.RecordType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@secNum", (object?)f.SECNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@secRgn", (object?)f.SECRegion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@legalNm", (object?)f.LegalName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@faxPhone", (object?)f.FaxPhone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@mailAddr", (object?)f.MailingAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@regStatus", (object?)f.RegistrationStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@aumDesc", (object?)f.AumDescription ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@stateOrg", (object?)f.StateOfOrganization ?? DBNull.Value);
    }

    /// <summary>
    /// Efficiently upserts a large batch of SEC firm records using a single transaction.
    /// Uses SQLite ON CONFLICT to update existing rows without changing their Id.
    /// </summary>
    public void UpsertFirmBatch(IEnumerable<Firm> firms, IProgress<string>? progress = null)
    {
        var conn = _context.GetConnection();
        using var txn = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = txn;
        cmd.CommandText = @"
            INSERT INTO Firms (CrdNumber, Name, LegalName, SECNumber, SECRegion,
                Address, City, State, Country, ZipCode, Phone, FaxPhone, Website,
                MailingAddress, BusinessType, StateOfOrganization,
                RecordType, RegistrationStatus, RegistrationDate, LatestFilingDate,
                NumberOfAdvisors, NumberOfEmployees, AumDescription,
                IsRegisteredWithSec, Source, CreatedAt, UpdatedAt)
            VALUES (@crd, @name, @legal, @sec, @region,
                @addr, @city, @state, @country, @zip, @phone, @fax, @web,
                @mail, @btype, @stateOrg,
                @rectype, @regstatus, @regdate, @filingdate,
                @numadv, @numemp, @aum,
                1, 'SEC', datetime('now'), datetime('now'))
            ON CONFLICT(CrdNumber) DO UPDATE SET
                Name               = excluded.Name,
                LegalName          = coalesce(excluded.LegalName, LegalName),
                SECNumber          = coalesce(excluded.SECNumber, SECNumber),
                SECRegion          = coalesce(excluded.SECRegion, SECRegion),
                Address            = coalesce(excluded.Address, Address),
                City               = coalesce(excluded.City, City),
                State              = coalesce(excluded.State, State),
                Country            = coalesce(excluded.Country, Country),
                ZipCode            = coalesce(excluded.ZipCode, ZipCode),
                Phone              = coalesce(excluded.Phone, Phone),
                FaxPhone           = coalesce(excluded.FaxPhone, FaxPhone),
                Website            = coalesce(excluded.Website, Website),
                MailingAddress     = coalesce(excluded.MailingAddress, MailingAddress),
                BusinessType       = coalesce(excluded.BusinessType, BusinessType),
                StateOfOrganization = coalesce(excluded.StateOfOrganization, StateOfOrganization),
                RecordType         = excluded.RecordType,
                RegistrationStatus = coalesce(excluded.RegistrationStatus, RegistrationStatus),
                RegistrationDate   = coalesce(excluded.RegistrationDate, RegistrationDate),
                LatestFilingDate   = coalesce(excluded.LatestFilingDate, LatestFilingDate),
                NumberOfAdvisors   = coalesce(excluded.NumberOfAdvisors, NumberOfAdvisors),
                NumberOfEmployees  = coalesce(excluded.NumberOfEmployees, NumberOfEmployees),
                AumDescription     = coalesce(excluded.AumDescription, AumDescription),
                IsRegisteredWithSec = 1,
                Source             = 'SEC',
                UpdatedAt          = datetime('now')
        ";

        int count = 0;
        foreach (var f in firms)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@crd", f.CrdNumber);
            cmd.Parameters.AddWithValue("@name", f.Name);
            cmd.Parameters.AddWithValue("@legal", (object?)f.LegalName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sec", (object?)f.SECNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@region", (object?)f.SECRegion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@addr", (object?)f.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@city", (object?)f.City ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@state", (object?)f.State ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@country", (object?)f.Country ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@zip", (object?)f.ZipCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@phone", (object?)f.Phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fax", (object?)f.FaxPhone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@web", (object?)f.Website ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mail", (object?)f.MailingAddress ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@btype", (object?)f.BusinessType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@stateOrg", (object?)f.StateOfOrganization ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rectype", (object?)f.RecordType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@regstatus", (object?)f.RegistrationStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@regdate", f.RegistrationDate.HasValue
                ? (object)f.RegistrationDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
            cmd.Parameters.AddWithValue("@filingdate", (object?)f.LatestFilingDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@numadv", (object?)f.NumberOfAdvisors ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@numemp", (object?)f.NumberOfEmployees ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@aum", (object?)f.AumDescription ?? DBNull.Value);
            cmd.ExecuteNonQuery();
            count++;
            if (count % 1000 == 0)
                progress?.Report($"SEC Monthly: Saved {count:N0} firms...");
        }

        txn.Commit();
        progress?.Report($"SEC Monthly: Saved {count:N0} total firms to database.");
    }

    public List<string> GetDistinctStates()
    {
        var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT State FROM Advisors WHERE State IS NOT NULL AND IsExcluded=0 ORDER BY State";
        var states = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            states.Add(r.GetString(0));
        return states;
    }

    public List<string> GetDistinctFirmStates()
    {
        var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT State FROM Firms WHERE State IS NOT NULL AND IsExcluded=0 ORDER BY State";
        var states = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            states.Add(r.GetString(0));
        return states;
    }

    public List<string> GetDistinctFirmNames()
    {
        var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT CurrentFirmName FROM Advisors WHERE CurrentFirmName IS NOT NULL AND IsExcluded=0 ORDER BY CurrentFirmName LIMIT 200";
        var firms = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            firms.Add(r.GetString(0));
        return firms;
    }

    public int GetAdvisorCount(SearchFilter filter)
    {
        var conn = _context.GetConnection();
        var sb = new StringBuilder("SELECT COUNT(*) FROM Advisors WHERE 1=1");
        var parameters = new List<(string name, object value)>();

        if (!filter.IncludeExcluded)
            sb.Append(" AND IsExcluded = 0");

        if (!string.IsNullOrWhiteSpace(filter.NameQuery))
        {
            sb.Append(" AND (FirstName LIKE @name OR LastName LIKE @name OR (FirstName || ' ' || LastName) LIKE @name)");
            parameters.Add(("@name", $"%{filter.NameQuery}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.State))
        {
            sb.Append(" AND State = @state");
            parameters.Add(("@state", filter.State));
        }

        if (!string.IsNullOrWhiteSpace(filter.FirmName))
        {
            sb.Append(" AND CurrentFirmName LIKE @firmName");
            parameters.Add(("@firmName", $"%{filter.FirmName}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.FirmCrd))
        {
            sb.Append(" AND CurrentFirmCrd = @firmCrd");
            parameters.Add(("@firmCrd", filter.FirmCrd));
        }

        if (!string.IsNullOrWhiteSpace(filter.CrdNumber))
        {
            sb.Append(" AND CrdNumber = @crd");
            parameters.Add(("@crd", filter.CrdNumber));
        }

        if (!string.IsNullOrWhiteSpace(filter.RegistrationStatus))
        {
            sb.Append(" AND RegistrationStatus LIKE @status");
            parameters.Add(("@status", filter.RegistrationStatus));
        }

        if (!string.IsNullOrWhiteSpace(filter.LicenseType))
        {
            sb.Append(" AND Licenses LIKE @license");
            parameters.Add(("@license", $"%{filter.LicenseType}%"));
        }

        if (filter.HasDisclosures.HasValue)
        {
            sb.Append(" AND HasDisclosures = @hasDisc");
            parameters.Add(("@hasDisc", filter.HasDisclosures.Value ? 1 : 0));
        }

        if (filter.IsImportedToCrm.HasValue)
        {
            sb.Append(" AND IsImportedToCrm = @imported");
            parameters.Add(("@imported", filter.IsImportedToCrm.Value ? 1 : 0));
        }

        if (!string.IsNullOrWhiteSpace(filter.Source))
        {
            if (filter.Source.Equals("Both", StringComparison.OrdinalIgnoreCase)
                || filter.Source.Contains(','))
            {
                sb.Append(" AND Source LIKE '%FINRA%' AND Source LIKE '%SEC%'");
            }
            else
            {
                sb.Append(" AND Source LIKE @source");
                parameters.Add(("@source", $"%{filter.Source}%"));
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.RecordType))
        {
            sb.Append(" AND RecordType = @recordType");
            parameters.Add(("@recordType", filter.RecordType));
        }

        if (filter.MinYearsExperience.HasValue)
        {
            sb.Append(" AND YearsOfExperience >= @minYears");
            parameters.Add(("@minYears", filter.MinYearsExperience.Value));
        }

        if (filter.MaxYearsExperience.HasValue)
        {
            sb.Append(" AND YearsOfExperience <= @maxYears");
            parameters.Add(("@maxYears", filter.MaxYearsExperience.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            sb.Append(" AND City LIKE @city");
            parameters.Add(("@city", $"%{filter.City}%"));
        }

        if (filter.MinDisclosureCount.HasValue && filter.MinDisclosureCount.Value > 0)
        {
            sb.Append(" AND DisclosureCount >= @minDisc");
            parameters.Add(("@minDisc", filter.MinDisclosureCount.Value));
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sb.ToString();
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static Advisor MapAdvisor(SqliteDataReader r)
    {
        return new Advisor
        {
            Id = r.GetInt32(0),
            CrdNumber = r.IsDBNull(1) ? null : r.GetString(1),
            IapdNumber = r.IsDBNull(2) ? null : r.GetString(2),
            FirstName = r.GetString(3),
            LastName = r.GetString(4),
            MiddleName = r.IsDBNull(5) ? null : r.GetString(5),
            Title = r.IsDBNull(6) ? null : r.GetString(6),
            Email = r.IsDBNull(7) ? null : r.GetString(7),
            Phone = r.IsDBNull(8) ? null : r.GetString(8),
            City = r.IsDBNull(9) ? null : r.GetString(9),
            State = r.IsDBNull(10) ? null : r.GetString(10),
            ZipCode = r.IsDBNull(11) ? null : r.GetString(11),
            Licenses = r.IsDBNull(12) ? null : r.GetString(12),
            Qualifications = r.IsDBNull(13) ? null : r.GetString(13),
            CurrentFirmName = r.IsDBNull(14) ? null : r.GetString(14),
            CurrentFirmCrd = r.IsDBNull(15) ? null : r.GetString(15),
            CurrentFirmId = r.IsDBNull(16) ? null : r.GetInt32(16),
            RegistrationStatus = r.IsDBNull(17) ? null : r.GetString(17),
            RegistrationDate = r.IsDBNull(18) ? null : DateTime.Parse(r.GetString(18)),
            YearsOfExperience = r.IsDBNull(19) ? null : r.GetInt32(19),
            HasDisclosures = r.GetInt32(20) == 1,
            DisclosureCount = r.GetInt32(21),
            Source = r.IsDBNull(22) ? null : r.GetString(22),
            IsExcluded = r.GetInt32(23) == 1,
            ExclusionReason = r.IsDBNull(24) ? null : r.GetString(24),
            IsImportedToCrm = r.GetInt32(25) == 1,
            CrmId = r.IsDBNull(26) ? null : r.GetString(26),
            CreatedAt = DateTime.Parse(r.GetString(27)),
            UpdatedAt = DateTime.Parse(r.GetString(28)),
            RecordType = r.IsDBNull(29) ? null : r.GetString(29),
            Suffix = r.FieldCount > 30 && !r.IsDBNull(30) ? r.GetString(30) : null,
            IapdLink = r.FieldCount > 31 && !r.IsDBNull(31) ? r.GetString(31) : null,
            RegAuthorities = r.FieldCount > 32 && !r.IsDBNull(32) ? r.GetString(32) : null,
            DisclosureFlags = r.FieldCount > 33 && !r.IsDBNull(33) ? r.GetString(33) : null,
            OtherNames = r.FieldCount > 34 && !r.IsDBNull(34) ? r.GetString(34) : null
        };
    }

    private static Firm MapFirm(SqliteDataReader r)
    {
        return new Firm
        {
            Id = r.GetInt32(0),
            CrdNumber = r.IsDBNull(1) ? string.Empty : r.GetString(1),
            Name = r.GetString(2),
            Address = r.IsDBNull(3) ? null : r.GetString(3),
            City = r.IsDBNull(4) ? null : r.GetString(4),
            State = r.IsDBNull(5) ? null : r.GetString(5),
            ZipCode = r.IsDBNull(6) ? null : r.GetString(6),
            Phone = r.IsDBNull(7) ? null : r.GetString(7),
            Website = r.IsDBNull(8) ? null : r.GetString(8),
            BusinessType = r.IsDBNull(9) ? null : r.GetString(9),
            IsRegisteredWithSec = r.GetInt32(10) == 1,
            IsRegisteredWithFinra = r.GetInt32(11) == 1,
            NumberOfAdvisors = r.IsDBNull(12) ? null : r.GetInt32(12),
            RegistrationDate = r.IsDBNull(13) ? null : DateTime.Parse(r.GetString(13)),
            Source = r.IsDBNull(14) ? null : r.GetString(14),
            IsExcluded = r.GetInt32(15) == 1,
            CreatedAt = DateTime.Parse(r.GetString(16)),
            UpdatedAt = DateTime.Parse(r.GetString(17)),
            RecordType = r.IsDBNull(18) ? null : r.GetString(18),
            SECNumber = r.FieldCount > 19 && !r.IsDBNull(19) ? r.GetString(19) : null,
            SECRegion = r.FieldCount > 20 && !r.IsDBNull(20) ? r.GetString(20) : null,
            LegalName = r.FieldCount > 21 && !r.IsDBNull(21) ? r.GetString(21) : null,
            FaxPhone = r.FieldCount > 22 && !r.IsDBNull(22) ? r.GetString(22) : null,
            MailingAddress = r.FieldCount > 23 && !r.IsDBNull(23) ? r.GetString(23) : null,
            RegistrationStatus = r.FieldCount > 24 && !r.IsDBNull(24) ? r.GetString(24) : null,
            AumDescription = r.FieldCount > 25 && !r.IsDBNull(25) ? r.GetString(25) : null,
            StateOfOrganization = r.FieldCount > 26 && !r.IsDBNull(26) ? r.GetString(26) : null
        };
    }
}
