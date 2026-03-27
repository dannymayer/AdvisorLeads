using Npgsql;
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
        using var conn = _context.GetConnection();
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
            sb.Append(" AND IsExcluded = FALSE");
        }

        if (!string.IsNullOrWhiteSpace(filter.NameQuery))
        {
            sb.Append(" AND (FirstName ILIKE @name OR LastName ILIKE @name OR (FirstName || ' ' || LastName) ILIKE @name)");
            parameters.Add(("@name", $"%{filter.NameQuery}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.State))
        {
            sb.Append(" AND State = @state");
            parameters.Add(("@state", filter.State));
        }

        if (!string.IsNullOrWhiteSpace(filter.FirmName))
        {
            sb.Append(" AND CurrentFirmName ILIKE @firmName");
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
            // Use ILIKE so "Active" matches "Active", "Inactive" etc. correctly
            sb.Append(" AND RegistrationStatus ILIKE @status");
            parameters.Add(("@status", filter.RegistrationStatus));
        }

        if (!string.IsNullOrWhiteSpace(filter.LicenseType))
        {
            sb.Append(" AND Licenses ILIKE @license");
            parameters.Add(("@license", $"%{filter.LicenseType}%"));
        }

        if (filter.HasDisclosures.HasValue)
        {
            sb.Append(" AND HasDisclosures = @hasDisc");
            parameters.Add(("@hasDisc", filter.HasDisclosures.Value));
        }

        if (filter.IsImportedToCrm.HasValue)
        {
            sb.Append(" AND IsImportedToCrm = @imported");
            parameters.Add(("@imported", filter.IsImportedToCrm.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.Source))
        {
            // "Both" means the record appears in both FINRA and SEC — order may vary
            if (filter.Source.Equals("Both", StringComparison.OrdinalIgnoreCase)
                || filter.Source.Contains(','))
            {
                sb.Append(" AND Source ILIKE '%FINRA%' AND Source ILIKE '%SEC%'");
            }
            else
            {
                sb.Append(" AND Source ILIKE @source");
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
            sb.Append(" AND City ILIKE @city");
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
        using var conn = _context.GetConnection();
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
        using var conn = _context.GetConnection();
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
        using var conn = _context.GetConnection();

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
        using var conn = _context.GetConnection();
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
                FALSE, NULL, FALSE, NULL, @suffix, @iapdLink, @regAuths, @discFlags, @otherNames, NOW())
            RETURNING Id;";
        BindAdvisorParams(cmd, a);
        a.Id = Convert.ToInt32(cmd.ExecuteScalar());
    }

    private void UpdateAdvisor(Advisor a)
    {
        using var conn = _context.GetConnection();
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
                RegistrationDate    = COALESCE(@regDate, RegistrationDate),
                YearsOfExperience   = COALESCE(@years, YearsOfExperience),
                HasDisclosures      = GREATEST(HasDisclosures, @discs),
                DisclosureCount     = GREATEST(DisclosureCount, @discCount),
                Source              = CASE
                    WHEN @source IS NULL OR @source = '' THEN Source
                    WHEN Source IS NULL OR Source = '' THEN @source
                    WHEN ',' || Source || ',' ILIKE '%,' || @source || ',%' THEN Source
                    ELSE Source || ',' || @source
                  END,
                RecordType          = COALESCE(NULLIF(@recordType, ''), RecordType),
                Suffix              = COALESCE(@suffix, Suffix),
                IapdLink            = COALESCE(@iapdLink, IapdLink),
                RegAuthorities      = COALESCE(NULLIF(@regAuths, ''), RegAuthorities),
                DisclosureFlags     = COALESCE(NULLIF(@discFlags, ''), DisclosureFlags),
                OtherNames          = COALESCE(NULLIF(@otherNames, ''), OtherNames),
                UpdatedAt           = NOW()
            WHERE Id = @id";
        BindAdvisorParams(cmd, a);
        cmd.Parameters.AddWithValue("@id", a.Id);
        cmd.ExecuteNonQuery();
    }

    private static void BindAdvisorParams(NpgsqlCommand cmd, Advisor a)
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
        cmd.Parameters.AddWithValue("@regDate", a.RegistrationDate.HasValue ? a.RegistrationDate.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@years", (object?)a.YearsOfExperience ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@discs", a.HasDisclosures);
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
        using var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Advisors SET IsExcluded = @excluded, ExclusionReason = @reason, UpdatedAt = NOW() WHERE Id = @id";
        cmd.Parameters.AddWithValue("@excluded", excluded);
        cmd.Parameters.AddWithValue("@reason", (object?)reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetAdvisorImported(int id, string? crmId)
    {
        using var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Advisors SET IsImportedToCrm = TRUE, CrmId = @crmId, UpdatedAt = NOW() WHERE Id = @id";
        cmd.Parameters.AddWithValue("@crmId", (object?)crmId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    private void LoadRelatedData(Advisor advisor)
    {
        using var conn = _context.GetConnection();

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
                    StartDate = r.IsDBNull(4) ? null : r.GetDateTime(4),
                    EndDate = r.IsDBNull(5) ? null : r.GetDateTime(5),
                    Position = r.IsDBNull(6) ? null : r.GetString(6),
                    Street = !r.IsDBNull(7) ? r.GetString(7) : null
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
                    Date = r.IsDBNull(4) ? null : r.GetDateTime(4),
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
                    Date = r.IsDBNull(4) ? null : r.GetDateTime(4),
                    Status = r.IsDBNull(5) ? null : r.GetString(5)
                });
            }
        }
    }

    private void UpsertEmploymentHistory(int advisorId, List<EmploymentHistory> history)
    {
        using var conn = _context.GetConnection();

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
                        ? h.StartDate!.Value
                        : (object)DBNull.Value);
                    updCmd.Parameters.AddWithValue("@end", hasNewEnd
                        ? h.EndDate!.Value
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
                    ? h.StartDate.Value
                    : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@end", (h.EndDate.HasValue && h.EndDate.Value != DateTime.MinValue)
                    ? h.EndDate.Value
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
        using var conn = _context.GetConnection();
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
            cmd.Parameters.AddWithValue("@date", d.Date.HasValue ? d.Date.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@res", (object?)d.Resolution ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sanc", (object?)d.Sanctions ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@src", (object?)d.Source ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    private void UpsertQualifications(int advisorId, List<Qualification> qualifications)
    {
        using var conn = _context.GetConnection();
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
            cmd.Parameters.AddWithValue("@date", q.Date.HasValue ? q.Date.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@status", (object?)q.Status ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public List<string> GetCrdsNeedingEnrichment(int limit)
    {
        using var conn = _context.GetConnection();
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

    /// <summary>
    /// Returns CRD numbers for advisors that need SEC IAPD enrichment:
    /// those without qualifications stored, prioritizing SEC-sourced records
    /// and those not already enriched by FINRA detail fetch.
    /// </summary>
    public List<string> GetCrdsNeedingIapdEnrichment(int limit = 200)
    {
        using var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        // Prioritize advisors with no qualifications AND no employment history
        // (meaning FINRA detail fetch didn't work — IAPD enrichment is warranted).
        // SEC-sourced records are prioritized first.
        cmd.CommandText = @"
            SELECT a.CrdNumber
            FROM Advisors a
            WHERE a.CrdNumber IS NOT NULL
              AND a.IsExcluded = FALSE
              AND NOT EXISTS (
                  SELECT 1 FROM Qualifications q WHERE q.AdvisorId = a.Id
              )
              AND NOT EXISTS (
                  SELECT 1 FROM EmploymentHistory e WHERE e.AdvisorId = a.Id
              )
            ORDER BY
                CASE WHEN a.Source ILIKE '%SEC%' THEN 0 ELSE 1 END,
                a.UpdatedAt ASC
            LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);

        var crds = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            crds.Add(r.GetString(0));
        return crds;
    }

    // ── Firms ─────────────────────────────────────────────────────────────

    public List<Firm> GetFirms(FirmSearchFilter? filter = null)
    {
        filter ??= new FirmSearchFilter();

        using var conn = _context.GetConnection();
        var sb = new StringBuilder(@"
            SELECT Id, CrdNumber, Name, Address, City, State, ZipCode, Phone, Website,
                   BusinessType, IsRegisteredWithSec, IsRegisteredWithFinra, NumberOfAdvisors,
                   RegistrationDate, Source, IsExcluded, CreatedAt, UpdatedAt, RecordType,
                   SECNumber, SECRegion, LegalName, FaxPhone, MailingAddress, RegistrationStatus,
                   AumDescription, StateOfOrganization, Country, NumberOfEmployees, LatestFilingDate,
                   RegulatoryAum, RegulatoryAumNonDiscretionary, NumClients,
                   BrokerProtocolMember, BrokerProtocolUpdatedAt
            FROM Firms WHERE IsExcluded = FALSE");

        var parameters = new List<(string name, object value)>();

        if (!string.IsNullOrWhiteSpace(filter.NameQuery))
        {
            sb.Append(" AND Name ILIKE @name");
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
            sb.Append(" AND RegistrationStatus ILIKE @status");
            parameters.Add(("@status", $"%{filter.RegistrationStatus}%"));
        }
        if (filter.MinAdvisors.HasValue && filter.MinAdvisors.Value > 0)
        {
            sb.Append(" AND NumberOfAdvisors >= @minAdv");
            parameters.Add(("@minAdv", filter.MinAdvisors.Value));
        }
        if (filter.BrokerProtocolOnly)
        {
            sb.Append(" AND BrokerProtocolMember = TRUE");
        }
        if (filter.MinRegulatoryAum.HasValue)
        {
            sb.Append(" AND RegulatoryAum >= @minAum");
            parameters.Add(("@minAum", (double)filter.MinRegulatoryAum.Value));
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
        using var conn = _context.GetConnection();

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
                    RegulatoryAum=@regAum, RegulatoryAumNonDiscretionary=@regAumNd,
                    NumClients=@numClients, BrokerProtocolMember=@bpMember,
                    BrokerProtocolUpdatedAt=@bpUpdated,
                    UpdatedAt=NOW()
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
                    RegistrationStatus, AumDescription, StateOfOrganization,
                    RegulatoryAum, RegulatoryAumNonDiscretionary, NumClients,
                    BrokerProtocolMember, BrokerProtocolUpdatedAt, UpdatedAt)
                VALUES (@crd, @name, @addr, @city, @state, @zip, @phone, @web,
                    @btype, @sec, @finra, @numAdv, @regDate, @source, @recordType,
                    @secNum, @secRgn, @legalNm, @faxPhone, @mailAddr,
                    @regStatus, @aumDesc, @stateOrg,
                    @regAum, @regAumNd, @numClients, @bpMember, @bpUpdated, NOW())
                RETURNING Id;";
            cmd.Parameters.AddWithValue("@crd", (object?)firm.CrdNumber ?? DBNull.Value);
            BindFirmParams(cmd, firm);
            firm.Id = Convert.ToInt32(cmd.ExecuteScalar());
        }

        return firm.Id;
    }

    private static void BindFirmParams(NpgsqlCommand cmd, Firm f)
    {
        cmd.Parameters.AddWithValue("@name", f.Name);
        cmd.Parameters.AddWithValue("@addr", (object?)f.Address ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@city", (object?)f.City ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@state", (object?)f.State ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@zip", (object?)f.ZipCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@phone", (object?)f.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@web", (object?)f.Website ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@btype", (object?)f.BusinessType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sec", f.IsRegisteredWithSec);
        cmd.Parameters.AddWithValue("@finra", f.IsRegisteredWithFinra);
        cmd.Parameters.AddWithValue("@numAdv", (object?)f.NumberOfAdvisors ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@regDate", f.RegistrationDate.HasValue ? f.RegistrationDate.Value : (object)DBNull.Value);
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
        cmd.Parameters.AddWithValue("@regAum",     (object?)f.RegulatoryAum ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@regAumNd",   (object?)f.RegulatoryAumNonDiscretionary ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@numClients", (object?)f.NumClients ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@bpMember",   f.BrokerProtocolMember);
        cmd.Parameters.AddWithValue("@bpUpdated",  f.BrokerProtocolUpdatedAt.HasValue ? f.BrokerProtocolUpdatedAt.Value : (object)DBNull.Value);
    }

    /// <summary>
    /// Efficiently upserts a large batch of SEC firm records using a single transaction.
    /// Uses ON CONFLICT to update existing rows without changing their Id.
    /// </summary>
    public void UpsertFirmBatch(IEnumerable<Firm> firms, IProgress<string>? progress = null)
    {
        using var conn = _context.GetConnection();
        using var txn = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = txn;
        cmd.CommandText = @"
            INSERT INTO Firms (CrdNumber, Name, LegalName, SECNumber, SECRegion,
                Address, City, State, Country, ZipCode, Phone, FaxPhone, Website,
                MailingAddress, BusinessType, StateOfOrganization,
                RecordType, RegistrationStatus, RegistrationDate, LatestFilingDate,
                NumberOfAdvisors, NumberOfEmployees, AumDescription,
                RegulatoryAum, RegulatoryAumNonDiscretionary, NumClients,
                IsRegisteredWithSec, Source, CreatedAt, UpdatedAt)
            VALUES (@crd, @name, @legal, @sec, @region,
                @addr, @city, @state, @country, @zip, @phone, @fax, @web,
                @mail, @btype, @stateOrg,
                @rectype, @regstatus, @regdate, @filingdate,
                @numadv, @numemp, @aum,
                @regaum, @regaumnd, @numclients,
                TRUE, 'SEC', NOW(), NOW())
            ON CONFLICT(CrdNumber) DO UPDATE SET
                Name               = excluded.Name,
                LegalName          = COALESCE(excluded.LegalName, LegalName),
                SECNumber          = COALESCE(excluded.SECNumber, SECNumber),
                SECRegion          = COALESCE(excluded.SECRegion, SECRegion),
                Address            = COALESCE(excluded.Address, Address),
                City               = COALESCE(excluded.City, City),
                State              = COALESCE(excluded.State, State),
                Country            = COALESCE(excluded.Country, Country),
                ZipCode            = COALESCE(excluded.ZipCode, ZipCode),
                Phone              = COALESCE(excluded.Phone, Phone),
                FaxPhone           = COALESCE(excluded.FaxPhone, FaxPhone),
                Website            = COALESCE(excluded.Website, Website),
                MailingAddress     = COALESCE(excluded.MailingAddress, MailingAddress),
                BusinessType       = COALESCE(excluded.BusinessType, BusinessType),
                StateOfOrganization = COALESCE(excluded.StateOfOrganization, StateOfOrganization),
                RecordType         = excluded.RecordType,
                RegistrationStatus = COALESCE(excluded.RegistrationStatus, RegistrationStatus),
                RegistrationDate   = COALESCE(excluded.RegistrationDate, RegistrationDate),
                LatestFilingDate   = COALESCE(excluded.LatestFilingDate, LatestFilingDate),
                NumberOfAdvisors   = COALESCE(excluded.NumberOfAdvisors, NumberOfAdvisors),
                NumberOfEmployees  = COALESCE(excluded.NumberOfEmployees, NumberOfEmployees),
                AumDescription     = COALESCE(excluded.AumDescription, AumDescription),
                RegulatoryAum      = COALESCE(excluded.RegulatoryAum, RegulatoryAum),
                RegulatoryAumNonDiscretionary = COALESCE(excluded.RegulatoryAumNonDiscretionary, RegulatoryAumNonDiscretionary),
                NumClients         = COALESCE(excluded.NumClients, NumClients),
                IsRegisteredWithSec = TRUE,
                Source             = 'SEC',
                UpdatedAt          = NOW()
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
                ? (object)f.RegistrationDate.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@filingdate", (object?)f.LatestFilingDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@numadv", (object?)f.NumberOfAdvisors ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@numemp", (object?)f.NumberOfEmployees ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@aum", (object?)f.AumDescription ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@regaum", (object?)f.RegulatoryAum ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@regaumnd", (object?)f.RegulatoryAumNonDiscretionary ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@numclients", (object?)f.NumClients ?? DBNull.Value);
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
        using var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT State FROM Advisors WHERE State IS NOT NULL AND IsExcluded = FALSE ORDER BY State";
        var states = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            states.Add(r.GetString(0));
        return states;
    }

    public List<string> GetDistinctFirmStates()
    {
        using var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT State FROM Firms WHERE State IS NOT NULL AND IsExcluded = FALSE ORDER BY State";
        var states = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            states.Add(r.GetString(0));
        return states;
    }

    public List<string> GetDistinctFirmNames()
    {
        using var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT CurrentFirmName FROM Advisors WHERE CurrentFirmName IS NOT NULL AND IsExcluded = FALSE ORDER BY CurrentFirmName LIMIT 200";
        var firms = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            firms.Add(r.GetString(0));
        return firms;
    }

    public int GetAdvisorCount(SearchFilter filter)
    {
        using var conn = _context.GetConnection();
        var sb = new StringBuilder("SELECT COUNT(*) FROM Advisors WHERE 1=1");
        var parameters = new List<(string name, object value)>();

        if (!filter.IncludeExcluded)
            sb.Append(" AND IsExcluded = FALSE");

        if (!string.IsNullOrWhiteSpace(filter.NameQuery))
        {
            sb.Append(" AND (FirstName ILIKE @name OR LastName ILIKE @name OR (FirstName || ' ' || LastName) ILIKE @name)");
            parameters.Add(("@name", $"%{filter.NameQuery}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.State))
        {
            sb.Append(" AND State = @state");
            parameters.Add(("@state", filter.State));
        }

        if (!string.IsNullOrWhiteSpace(filter.FirmName))
        {
            sb.Append(" AND CurrentFirmName ILIKE @firmName");
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
            sb.Append(" AND RegistrationStatus ILIKE @status");
            parameters.Add(("@status", filter.RegistrationStatus));
        }

        if (!string.IsNullOrWhiteSpace(filter.LicenseType))
        {
            sb.Append(" AND Licenses ILIKE @license");
            parameters.Add(("@license", $"%{filter.LicenseType}%"));
        }

        if (filter.HasDisclosures.HasValue)
        {
            sb.Append(" AND HasDisclosures = @hasDisc");
            parameters.Add(("@hasDisc", filter.HasDisclosures.Value));
        }

        if (filter.IsImportedToCrm.HasValue)
        {
            sb.Append(" AND IsImportedToCrm = @imported");
            parameters.Add(("@imported", filter.IsImportedToCrm.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.Source))
        {
            if (filter.Source.Equals("Both", StringComparison.OrdinalIgnoreCase)
                || filter.Source.Contains(','))
            {
                sb.Append(" AND Source ILIKE '%FINRA%' AND Source ILIKE '%SEC%'");
            }
            else
            {
                sb.Append(" AND Source ILIKE @source");
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
            sb.Append(" AND City ILIKE @city");
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

    private static Advisor MapAdvisor(NpgsqlDataReader r)
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
            RegistrationDate = r.IsDBNull(18) ? null : r.GetDateTime(18),
            YearsOfExperience = r.IsDBNull(19) ? null : r.GetInt32(19),
            HasDisclosures = r.GetBoolean(20),
            DisclosureCount = r.GetInt32(21),
            Source = r.IsDBNull(22) ? null : r.GetString(22),
            IsExcluded = r.GetBoolean(23),
            ExclusionReason = r.IsDBNull(24) ? null : r.GetString(24),
            IsImportedToCrm = r.GetBoolean(25),
            CrmId = r.IsDBNull(26) ? null : r.GetString(26),
            CreatedAt = r.GetDateTime(27),
            UpdatedAt = r.GetDateTime(28),
            RecordType = r.IsDBNull(29) ? null : r.GetString(29),
            Suffix = !r.IsDBNull(30) ? r.GetString(30) : null,
            IapdLink = !r.IsDBNull(31) ? r.GetString(31) : null,
            RegAuthorities = !r.IsDBNull(32) ? r.GetString(32) : null,
            DisclosureFlags = !r.IsDBNull(33) ? r.GetString(33) : null,
            OtherNames = !r.IsDBNull(34) ? r.GetString(34) : null
        };
    }

    private static Firm MapFirm(NpgsqlDataReader r)
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
            IsRegisteredWithSec = r.GetBoolean(10),
            IsRegisteredWithFinra = r.GetBoolean(11),
            NumberOfAdvisors = r.IsDBNull(12) ? null : r.GetInt32(12),
            RegistrationDate = r.IsDBNull(13) ? null : r.GetDateTime(13),
            Source = r.IsDBNull(14) ? null : r.GetString(14),
            IsExcluded = r.GetBoolean(15),
            CreatedAt = r.GetDateTime(16),
            UpdatedAt = r.GetDateTime(17),
            RecordType = r.IsDBNull(18) ? null : r.GetString(18),
            SECNumber = !r.IsDBNull(19) ? r.GetString(19) : null,
            SECRegion = !r.IsDBNull(20) ? r.GetString(20) : null,
            LegalName = !r.IsDBNull(21) ? r.GetString(21) : null,
            FaxPhone = !r.IsDBNull(22) ? r.GetString(22) : null,
            MailingAddress = !r.IsDBNull(23) ? r.GetString(23) : null,
            RegistrationStatus = !r.IsDBNull(24) ? r.GetString(24) : null,
            AumDescription = !r.IsDBNull(25) ? r.GetString(25) : null,
            StateOfOrganization = !r.IsDBNull(26) ? r.GetString(26) : null,
            Country = !r.IsDBNull(27) ? r.GetString(27) : null,
            NumberOfEmployees = !r.IsDBNull(28) ? r.GetInt32(28) : null,
            LatestFilingDate = !r.IsDBNull(29) ? r.GetString(29) : null,
            RegulatoryAum = !r.IsDBNull(30) ? (decimal?)r.GetDouble(30) : null,
            RegulatoryAumNonDiscretionary = !r.IsDBNull(31) ? (decimal?)r.GetDouble(31) : null,
            NumClients = !r.IsDBNull(32) ? r.GetInt32(32) : null,
            BrokerProtocolMember = !r.IsDBNull(33) && r.GetBoolean(33),
            BrokerProtocolUpdatedAt = !r.IsDBNull(34) ? r.GetDateTime(34) : null
        };
    }

    /// <summary>
    /// Marks firms as Broker Protocol members by matching against a list of member names.
    /// Clears old memberships first, then sets new ones using fuzzy name matching.
    /// </summary>
    public int UpdateBrokerProtocolStatus(List<string> memberNames, DateTime fetchedAt)
    {
        using var conn = _context.GetConnection();
        int updated = 0;

        // Clear all existing memberships
        using (var clearCmd = conn.CreateCommand())
        {
            clearCmd.CommandText = "UPDATE Firms SET BrokerProtocolMember = FALSE";
            clearCmd.ExecuteNonQuery();
        }

        if (memberNames.Count == 0) return 0;

        // Load all firm names for fuzzy matching
        var firms = new List<(int Id, string Name)>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, Name FROM Firms WHERE Name IS NOT NULL";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                firms.Add((r.GetInt32(0), r.GetString(1)));
        }

        // Normalize a name for matching: lowercase, strip legal suffixes, remove punctuation
        static string Normalize(string s) => System.Text.RegularExpressions.Regex.Replace(
            s.ToLowerInvariant()
             .Replace(" llc", "").Replace(" inc", "").Replace(" corp", "")
             .Replace(" lp", "").Replace(" ltd", "").Replace(" co.", "")
             .Replace(",", "").Replace(".", "").Replace("&", "and"),
            @"\s+", " ").Trim();

        var memberNormalized = memberNames.Select(Normalize).ToHashSet();

        var toUpdate = new List<int>();
        foreach (var (id, name) in firms)
        {
            var norm = Normalize(name);
            if (memberNormalized.Contains(norm) ||
                memberNormalized.Any(m => norm.Contains(m) || m.Contains(norm)))
            {
                toUpdate.Add(id);
            }
        }

        if (toUpdate.Count > 0)
        {
            using var updateCmd = conn.CreateCommand();
            var idList = string.Join(",", toUpdate);
            updateCmd.CommandText = $"UPDATE Firms SET BrokerProtocolMember = TRUE, BrokerProtocolUpdatedAt = @ts WHERE Id IN ({idList})";
            updateCmd.Parameters.AddWithValue("@ts", fetchedAt);
            updated = updateCmd.ExecuteNonQuery();
        }

        return updated;
    }
}
