using Npgsql;
using AdvisorLeads.Models;

namespace AdvisorLeads.Data;

public class ListRepository
{
    private readonly DatabaseContext _context;

    public ListRepository(DatabaseContext context)
    {
        _context = context;
    }

    public List<AdvisorList> GetAllLists()
    {
        using var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT l.Id, l.Name, l.Description, l.CreatedAt, l.UpdatedAt,
                   COUNT(m.Id) as MemberCount
            FROM AdvisorLists l
            LEFT JOIN AdvisorListMembers m ON m.ListId = l.Id
            GROUP BY l.Id
            ORDER BY l.Name ASC";
        var lists = new List<AdvisorList>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            lists.Add(new AdvisorList
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                Description = r.IsDBNull(2) ? null : r.GetString(2),
                CreatedAt = r.GetDateTime(3),
                UpdatedAt = r.GetDateTime(4),
                MemberCount = r.GetInt32(5)
            });
        }
        return lists;
    }

    public AdvisorList CreateList(string name, string? description = null)
    {
        using var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO AdvisorLists (Name, Description)
            VALUES (@name, @desc)
            RETURNING Id;";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@desc", (object?)description ?? DBNull.Value);
        var id = Convert.ToInt32(cmd.ExecuteScalar());
        return new AdvisorList { Id = id, Name = name, Description = description, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now };
    }

    public void RenameList(int listId, string newName, string? description = null)
    {
        using var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE AdvisorLists SET Name=@name, Description=@desc, UpdatedAt=NOW() WHERE Id=@id";
        cmd.Parameters.AddWithValue("@name", newName);
        cmd.Parameters.AddWithValue("@desc", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", listId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteList(int listId)
    {
        using var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM AdvisorLists WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", listId);
        cmd.ExecuteNonQuery();
    }

    public bool AddToList(int listId, int advisorId, string? notes = null)
    {
        using var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO AdvisorListMembers (ListId, AdvisorId, Notes)
            VALUES (@list, @adv, @notes)
            ON CONFLICT (ListId, AdvisorId) DO NOTHING";
        cmd.Parameters.AddWithValue("@list", listId);
        cmd.Parameters.AddWithValue("@adv", advisorId);
        cmd.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);
        return cmd.ExecuteNonQuery() > 0;
    }

    public void RemoveFromList(int listId, int advisorId)
    {
        using var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM AdvisorListMembers WHERE ListId=@list AND AdvisorId=@adv";
        cmd.Parameters.AddWithValue("@list", listId);
        cmd.Parameters.AddWithValue("@adv", advisorId);
        cmd.ExecuteNonQuery();
    }

    public List<Advisor> GetListMembers(int listId)
    {
        using var conn = _context.GetConnection();

        // ── Query 1: All advisor rows for this list in a single JOIN ──────────
        using var mainCmd = conn.CreateCommand();
        mainCmd.CommandText = @"
            SELECT a.Id, a.CrdNumber, a.IapdNumber, a.FirstName, a.LastName, a.MiddleName,
                   a.Title, a.Email, a.Phone, a.City, a.State, a.ZipCode, a.Licenses,
                   a.Qualifications, a.CurrentFirmName, a.CurrentFirmCrd, a.CurrentFirmId,
                   a.RegistrationStatus, a.RegistrationDate, a.YearsOfExperience,
                   a.HasDisclosures, a.DisclosureCount, a.Source, a.IsExcluded,
                   a.ExclusionReason, a.IsImportedToCrm, a.CrmId, a.CreatedAt, a.UpdatedAt,
                   a.RecordType, a.Suffix, a.IapdLink, a.RegAuthorities, a.DisclosureFlags,
                   a.OtherNames
            FROM AdvisorListMembers m
            JOIN Advisors a ON a.Id = m.AdvisorId
            WHERE m.ListId = @listId
            ORDER BY a.LastName, a.FirstName";
        mainCmd.Parameters.AddWithValue("@listId", listId);

        var advisors = new List<Advisor>();
        using (var r = mainCmd.ExecuteReader())
        {
            while (r.Read())
                advisors.Add(MapAdvisorRow(r));
        }

        if (advisors.Count == 0) return advisors;

        var ids = string.Join(",", advisors.Select(a => a.Id));
        var map = advisors.ToDictionary(a => a.Id);

        // ── Query 2: Employment history for all members ───────────────────────
        using var empCmd = conn.CreateCommand();
        empCmd.CommandText = $@"
            SELECT AdvisorId, FirmName, FirmCrd, StartDate, EndDate, Position, Street
            FROM EmploymentHistory
            WHERE AdvisorId IN ({ids})
            ORDER BY StartDate DESC";
        using (var er = empCmd.ExecuteReader())
        {
            while (er.Read())
            {
                int advisorId = er.GetInt32(0);
                if (map.TryGetValue(advisorId, out var a))
                    a.EmploymentHistory.Add(new EmploymentHistory
                    {
                        FirmName  = er.IsDBNull(1) ? "" : er.GetString(1),
                        FirmCrd   = er.IsDBNull(2) ? null : er.GetString(2),
                        StartDate = er.IsDBNull(3) ? null : (DateTime?)er.GetDateTime(3),
                        EndDate   = er.IsDBNull(4) ? null : (DateTime?)er.GetDateTime(4),
                        Position  = er.IsDBNull(5) ? null : er.GetString(5),
                        Street    = er.IsDBNull(6) ? null : er.GetString(6),
                    });
            }
        }

        // ── Query 3: Disclosures for all members ──────────────────────────────
        using var discCmd = conn.CreateCommand();
        discCmd.CommandText = $@"
            SELECT AdvisorId, Type, Description, Date, Resolution, Sanctions, Source
            FROM Disclosures WHERE AdvisorId IN ({ids})";
        using (var dr = discCmd.ExecuteReader())
        {
            while (dr.Read())
            {
                int advisorId = dr.GetInt32(0);
                if (map.TryGetValue(advisorId, out var a))
                    a.Disclosures.Add(new Disclosure
                    {
                        Type        = dr.IsDBNull(1) ? "" : dr.GetString(1),
                        Description = dr.IsDBNull(2) ? null : dr.GetString(2),
                        Date        = dr.IsDBNull(3) ? null : (DateTime?)dr.GetDateTime(3),
                        Resolution  = dr.IsDBNull(4) ? null : dr.GetString(4),
                        Sanctions   = dr.IsDBNull(5) ? null : dr.GetString(5),
                        Source      = dr.IsDBNull(6) ? null : dr.GetString(6),
                    });
            }
        }

        // ── Query 4: Qualifications for all members ───────────────────────────
        using var qualCmd = conn.CreateCommand();
        qualCmd.CommandText = $@"
            SELECT AdvisorId, Name, Code, Date, Status
            FROM Qualifications WHERE AdvisorId IN ({ids})";
        using (var qr = qualCmd.ExecuteReader())
        {
            while (qr.Read())
            {
                int advisorId = qr.GetInt32(0);
                if (map.TryGetValue(advisorId, out var a))
                    a.QualificationList.Add(new Qualification
                    {
                        Name   = qr.IsDBNull(1) ? "" : qr.GetString(1),
                        Code   = qr.IsDBNull(2) ? null : qr.GetString(2),
                        Date   = qr.IsDBNull(3) ? null : (DateTime?)qr.GetDateTime(3),
                        Status = qr.IsDBNull(4) ? null : qr.GetString(4),
                    });
            }
        }

        return advisors;
    }

    private static Advisor MapAdvisorRow(NpgsqlDataReader r)
    {
        return new Advisor
        {
            Id                 = r.GetInt32(0),
            CrdNumber          = r.IsDBNull(1)  ? null : r.GetString(1),
            IapdNumber         = r.IsDBNull(2)  ? null : r.GetString(2),
            FirstName          = r.GetString(3),
            LastName           = r.GetString(4),
            MiddleName         = r.IsDBNull(5)  ? null : r.GetString(5),
            Title              = r.IsDBNull(6)  ? null : r.GetString(6),
            Email              = r.IsDBNull(7)  ? null : r.GetString(7),
            Phone              = r.IsDBNull(8)  ? null : r.GetString(8),
            City               = r.IsDBNull(9)  ? null : r.GetString(9),
            State              = r.IsDBNull(10) ? null : r.GetString(10),
            ZipCode            = r.IsDBNull(11) ? null : r.GetString(11),
            Licenses           = r.IsDBNull(12) ? null : r.GetString(12),
            Qualifications     = r.IsDBNull(13) ? null : r.GetString(13),
            CurrentFirmName    = r.IsDBNull(14) ? null : r.GetString(14),
            CurrentFirmCrd     = r.IsDBNull(15) ? null : r.GetString(15),
            CurrentFirmId      = r.IsDBNull(16) ? null : (int?)r.GetInt32(16),
            RegistrationStatus = r.IsDBNull(17) ? null : r.GetString(17),
            RegistrationDate   = r.IsDBNull(18) ? null : (DateTime?)r.GetDateTime(18),
            YearsOfExperience  = r.IsDBNull(19) ? null : (int?)r.GetInt32(19),
            HasDisclosures     = !r.IsDBNull(20) && r.GetBoolean(20),
            DisclosureCount    = r.IsDBNull(21) ? 0 : r.GetInt32(21),
            Source             = r.IsDBNull(22) ? null : r.GetString(22),
            IsExcluded         = !r.IsDBNull(23) && r.GetBoolean(23),
            ExclusionReason    = r.IsDBNull(24) ? null : r.GetString(24),
            IsImportedToCrm    = !r.IsDBNull(25) && r.GetBoolean(25),
            CrmId              = r.IsDBNull(26) ? null : r.GetString(26),
            CreatedAt          = r.IsDBNull(27) ? DateTime.Now : r.GetDateTime(27),
            UpdatedAt          = r.IsDBNull(28) ? DateTime.Now : r.GetDateTime(28),
            RecordType         = r.IsDBNull(29) ? null : r.GetString(29),
            Suffix             = r.IsDBNull(30) ? null : r.GetString(30),
            IapdLink           = r.IsDBNull(31) ? null : r.GetString(31),
            RegAuthorities     = r.IsDBNull(32) ? null : r.GetString(32),
            DisclosureFlags    = r.IsDBNull(33) ? null : r.GetString(33),
            OtherNames         = r.IsDBNull(34) ? null : r.GetString(34),
        };
    }

    public List<int> GetListIdsForAdvisor(int advisorId)
    {
        using var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ListId FROM AdvisorListMembers WHERE AdvisorId=@adv";
        cmd.Parameters.AddWithValue("@adv", advisorId);
        var ids = new List<int>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) ids.Add(r.GetInt32(0));
        return ids;
    }

    public void UpdateMemberNotes(int listId, int advisorId, string? notes)
    {
        using var conn = _context.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE AdvisorListMembers SET Notes=@notes WHERE ListId=@list AND AdvisorId=@adv";
        cmd.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@list", listId);
        cmd.Parameters.AddWithValue("@adv", advisorId);
        cmd.ExecuteNonQuery();
    }
}
