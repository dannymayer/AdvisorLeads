using Microsoft.Data.Sqlite;
using AdvisorLeads.Models;
using System.Text;

namespace AdvisorLeads.Data;

/// <summary>
/// Raw-SQL reporting queries over the AdvisorLeads SQLite database.
/// Each method opens its own connection to keep EF Core thread-safety rules satisfied.
/// </summary>
public class ReportingRepository
{
    private readonly string _dbPath;

    public ReportingRepository(string dbPath) => _dbPath = dbPath;

    // ── Connection factory ────────────────────────────────────────────────
    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    // ── Shared helpers ────────────────────────────────────────────────────

    private static string DeriveFirmCompensationModel(bool? feeOnly, bool? commission, bool? hourly, bool? perf)
    {
        bool f = feeOnly == true, c = commission == true, h = hourly == true, p = perf == true;
        if (f && !c && !h && !p) return "Fee-Only";
        if (!f && c && !h && !p) return "Commission-Only";
        if (f && c) return "Fee & Commission";
        if (p && !f && !c && !h) return "Performance-Based";
        if (h || f || c || p) return "Mixed";
        return "Unknown";
    }

    private static decimal? SafeAumPerAdvisor(decimal? aum, int? advisorCount)
    {
        if (aum == null || advisorCount == null || advisorCount == 0) return null;
        return aum / advisorCount;
    }

    private static decimal? AverageDecimal(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? null : list.Average();
    }

    private static string? RS(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static int? RI(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i));
    private static int RIZ(SqliteDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));
    private static bool RB(SqliteDataReader r, int i) => !r.IsDBNull(i) && Convert.ToInt32(r.GetValue(i)) != 0;

    private static decimal? RD(SqliteDataReader r, int i)
    {
        if (r.IsDBNull(i)) return null;
        var v = r.GetValue(i);
        return v switch
        {
            double d => (decimal)d,
            long l => (decimal)l,
            string s => decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : null,
            _ => null
        };
    }

    private static DateTime? RDate(SqliteDataReader r, int i)
    {
        if (r.IsDBNull(i)) return null;
        var s = r.GetString(i);
        return DateTime.TryParse(s, out var d) ? d : null;
    }

    // ── Report 1 — Flight Risk Scorecard ─────────────────────────────────

    public List<FlightRiskRow> GetFlightRisk(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT a.Id,
    a.FirstName || ' ' || a.LastName AS FullName,
    a.CrdNumber, a.State, a.City, a.CurrentFirmName,
    a.YearsOfExperience, a.TotalFirmCount, a.HasDisclosures, a.Email,
    CAST(a.TotalFirmCount AS REAL) / NULLIF(a.YearsOfExperience, 0) AS FirmChangeRate,
    f.RegulatoryAum AS FirmRegulatoryAum,
    f.NumberOfAdvisors,
    CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0) AS AumPerAdvisor,
    f.BrokerProtocolMember,
    f.PriorAdvisorCount,
    CAST((julianday('now') - julianday(a.CurrentFirmStartDate)) / 365.25 AS INTEGER) AS TenureYears,
    h.RegulatoryAum AS Aum1YrAgo
FROM Advisors a
LEFT JOIN Firms f ON f.Id = a.CurrentFirmId
LEFT JOIN (
    SELECT fah.FirmCrd, fah.RegulatoryAum FROM FirmAumHistory fah
    WHERE fah.SnapshotDate = (
        SELECT MAX(SnapshotDate) FROM FirmAumHistory
        WHERE FirmCrd = fah.FirmCrd AND SnapshotDate <= date('now','-1 year')
    )
) h ON h.FirmCrd = f.CrdNumber
WHERE 1=1");

        AppendAdvisorWhere(sql, cmd, filter);

        cmd.CommandText = sql.ToString();
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize * 4); // over-fetch so we can re-sort

        var raw = new List<(int Id, string FullName, string? Crd, string? State, string? City,
            string? FirmName, int? TenureYears, int? YearsExp, int? TotalFirms,
            decimal? FirmChangeRate, decimal? FirmAum, decimal? AumPerAdvisor,
            bool BrokerProtocol, bool HasDisclosures, string? Email,
            int? CurrentAdvisors, int? PriorAdvisors, decimal? Aum1YrAgo)>();

        using (var r = cmd.ExecuteReader())
        {
            var cId = r.GetOrdinal("Id");
            var cFull = r.GetOrdinal("FullName");
            var cCrd = r.GetOrdinal("CrdNumber");
            var cState = r.GetOrdinal("State");
            var cCity = r.GetOrdinal("City");
            var cFirmName = r.GetOrdinal("CurrentFirmName");
            var cYearsExp = r.GetOrdinal("YearsOfExperience");
            var cTotalFirms = r.GetOrdinal("TotalFirmCount");
            var cHasDis = r.GetOrdinal("HasDisclosures");
            var cEmail = r.GetOrdinal("Email");
            var cFcr = r.GetOrdinal("FirmChangeRate");
            var cFirmAum = r.GetOrdinal("FirmRegulatoryAum");
            var cAdvisors = r.GetOrdinal("NumberOfAdvisors");
            var cAumPer = r.GetOrdinal("AumPerAdvisor");
            var cBp = r.GetOrdinal("BrokerProtocolMember");
            var cPrior = r.GetOrdinal("PriorAdvisorCount");
            var cTenure = r.GetOrdinal("TenureYears");
            var cAum1yr = r.GetOrdinal("Aum1YrAgo");

            while (r.Read())
            {
                raw.Add((
                    r.GetInt32(cId),
                    RS(r, cFull) ?? "",
                    RS(r, cCrd), RS(r, cState), RS(r, cCity),
                    RS(r, cFirmName),
                    RI(r, cTenure), RI(r, cYearsExp), RI(r, cTotalFirms),
                    RD(r, cFcr), RD(r, cFirmAum),
                    RD(r, cAumPer), RB(r, cBp), RB(r, cHasDis),
                    RS(r, cEmail), RI(r, cAdvisors), RI(r, cPrior), RD(r, cAum1yr)));
            }
        }

        var results = raw.Select(x =>
        {
            decimal? firmAum = x.FirmAum;
            decimal? aum1yr = x.Aum1YrAgo;
            decimal? aumChangePct = null;
            if (firmAum.HasValue && aum1yr.HasValue && aum1yr != 0)
                aumChangePct = (firmAum.Value - aum1yr.Value) / aum1yr.Value * 100m;

            int score = 0;
            if (aumChangePct.HasValue && aumChangePct < -10m) score += 25;
            if (x.TenureYears.HasValue && x.TenureYears > 10) score += 20;
            if (x.TotalFirms.HasValue && x.TotalFirms >= 4) score += 15;
            if (x.FirmChangeRate.HasValue && x.FirmChangeRate > 0.3m) score += 10;
            if (x.BrokerProtocol) score += 10;
            if (x.PriorAdvisors.HasValue && x.CurrentAdvisors.HasValue
                && x.PriorAdvisors > x.CurrentAdvisors) score += 20;

            return new FlightRiskRow(x.Id, x.FullName, x.Crd, x.State, x.City,
                x.FirmName, x.TenureYears, x.YearsExp, x.TotalFirms,
                x.FirmChangeRate, firmAum,
                x.AumPerAdvisor, aumChangePct,
                x.BrokerProtocol, x.HasDisclosures, x.Email, score);
        })
        .OrderByDescending(x => x.FlightRiskScore)
        .Take(filter.PageSize)
        .ToList();

        return results;
    }

    // ── Report 2 — High-Value Target Advisor List ─────────────────────────

    public List<HighValueTargetRow> GetHighValueTargets(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT a.Id,
    a.FirstName || ' ' || a.LastName AS FullName,
    a.CrdNumber, a.State, a.City, a.CurrentFirmName,
    a.YearsOfExperience, a.HasDisclosures, a.Email, a.Qualifications,
    f.RegulatoryAum, f.NumberOfAdvisors, f.BrokerProtocolMember,
    CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0) AS AumPerAdvisor
FROM Advisors a
LEFT JOIN Firms f ON f.Id = a.CurrentFirmId
WHERE 1=1");

        AppendAdvisorWhere(sql, cmd, filter);
        sql.Append(" ORDER BY AumPerAdvisor DESC LIMIT @pageSize");
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize);
        cmd.CommandText = sql.ToString();

        var raw = new List<(int Id, string FullName, string? Crd, string? State, string? City,
            string? FirmName, int? YearsExp, bool HasDis, string? Email, string? Quals,
            decimal? FirmAum, int? NumAdvisors, bool Bp, decimal? AumPer)>();

        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                raw.Add((r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2), RS(r, 3), RS(r, 4),
                    RS(r, 5), RI(r, 6), RB(r, 7), RS(r, 8), RS(r, 9),
                    RD(r, 10), RI(r, 11), RB(r, 12), RD(r, 13)));
            }
        }

        // AUM per advisor quintile scoring (0,8,16,24,40 points)
        var sorted = raw.OrderBy(x => x.AumPer ?? 0).ToList();
        int total = sorted.Count;

        return raw.Select(x =>
        {
            int rank = sorted.FindIndex(s => s.Id == x.Id);
            int quintile = total > 0 ? Math.Min(4, rank * 5 / total) : 0;
            int[] quintilePoints = { 0, 8, 16, 24, 40 };
            int score = quintilePoints[quintile];
            if (x.Quals != null && (x.Quals.Contains("CFP", StringComparison.OrdinalIgnoreCase)
                || x.Quals.Contains("CFA", StringComparison.OrdinalIgnoreCase))) score += 20;
            if (x.YearsExp.HasValue && x.YearsExp >= 10) score += 20;
            if (!x.HasDis) score += 20;

            return new HighValueTargetRow(x.Id, x.FullName, x.Crd, x.State, x.City,
                x.FirmName, x.YearsExp, x.HasDis, x.Email, x.Quals,
                x.FirmAum, x.NumAdvisors, x.Bp, x.AumPer, score);
        }).ToList();
    }

    // ── Report 3 — Advisor Tenure Distribution (summary) ─────────────────

    public List<TenureDistributionSummaryRow> GetTenureDistributionSummary(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT
    CASE
        WHEN CAST((julianday('now') - julianday(a.CurrentFirmStartDate)) / 365.25 AS INTEGER) < 2 THEN '<2 Years'
        WHEN CAST((julianday('now') - julianday(a.CurrentFirmStartDate)) / 365.25 AS INTEGER) < 5 THEN '2-5 Years'
        WHEN CAST((julianday('now') - julianday(a.CurrentFirmStartDate)) / 365.25 AS INTEGER) < 10 THEN '5-10 Years'
        WHEN CAST((julianday('now') - julianday(a.CurrentFirmStartDate)) / 365.25 AS INTEGER) < 20 THEN '10-20 Years'
        ELSE '20+ Years'
    END AS TenureBucket,
    COUNT(*) AS AdvisorCount,
    AVG(CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0)) AS AvgAumPerAdvisor,
    CAST(SUM(CASE WHEN a.HasDisclosures = 1 THEN 1 ELSE 0 END) AS REAL) / COUNT(*) AS DisclosureRate
FROM Advisors a
LEFT JOIN Firms f ON f.Id = a.CurrentFirmId
WHERE a.CurrentFirmStartDate IS NOT NULL");

        AppendAdvisorWhere(sql, cmd, filter);

        sql.Append(@"
GROUP BY TenureBucket
ORDER BY
    CASE TenureBucket
        WHEN '<2 Years'   THEN 1
        WHEN '2-5 Years'  THEN 2
        WHEN '5-10 Years' THEN 3
        WHEN '10-20 Years' THEN 4
        ELSE 5
    END");

        cmd.CommandText = sql.ToString();

        var results = new List<TenureDistributionSummaryRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new TenureDistributionSummaryRow(RS(r, 0) ?? "", RIZ(r, 1), RD(r, 2), RD(r, 3)));

        return results;
    }

    // ── Report 3 — Advisor Tenure Distribution (detail) ──────────────────

    public List<TenureDistributionDetailRow> GetTenureDistributionDetail(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT a.Id,
    a.FirstName || ' ' || a.LastName AS FullName,
    a.CrdNumber, a.State, a.CurrentFirmName,
    CAST((julianday('now') - julianday(a.CurrentFirmStartDate)) / 365.25 AS INTEGER) AS TenureYears,
    a.YearsOfExperience, a.HasDisclosures,
    CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0) AS AumPerAdvisor
FROM Advisors a
LEFT JOIN Firms f ON f.Id = a.CurrentFirmId
WHERE 1=1");

        AppendAdvisorWhere(sql, cmd, filter);
        sql.Append(" ORDER BY TenureYears DESC LIMIT @pageSize");
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize);
        cmd.CommandText = sql.ToString();

        var results = new List<TenureDistributionDetailRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new TenureDistributionDetailRow(r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2),
                RS(r, 3), RS(r, 4), RI(r, 5), RI(r, 6), RB(r, 7), RD(r, 8)));

        return results;
    }

    // ── Report 4 — Serial Mover Profile ──────────────────────────────────

    public List<SerialMoverRow> GetSerialMovers(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT a.Id,
    a.FirstName || ' ' || a.LastName AS FullName,
    a.CrdNumber, a.State, a.CurrentFirmName,
    a.TotalFirmCount, a.YearsOfExperience,
    CAST(a.TotalFirmCount AS REAL) / NULLIF(a.YearsOfExperience, 0) AS FirmChangeRate,
    CAST((julianday('now') - julianday(a.CurrentFirmStartDate)) / 365.25 AS INTEGER) AS TenureYears,
    f.RegulatoryAum,
    CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0) AS AumPerAdvisor
FROM Advisors a
LEFT JOIN Firms f ON f.Id = a.CurrentFirmId
WHERE a.TotalFirmCount >= 3");

        AppendAdvisorWhere(sql, cmd, filter);
        sql.Append(" ORDER BY FirmChangeRate DESC LIMIT @pageSize");
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize);
        cmd.CommandText = sql.ToString();

        var results = new List<SerialMoverRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            int? tenure = RI(r, 8);
            bool dueForMove = tenure.HasValue && tenure >= 2 && tenure <= 7;
            results.Add(new SerialMoverRow(r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2),
                RS(r, 3), RS(r, 4), RI(r, 5), RI(r, 6), RD(r, 7),
                tenure, RD(r, 9), RD(r, 10), dueForMove));
        }

        return results;
    }

    // ── Report 5 — New Market Entrants ────────────────────────────────────

    public List<NewMarketEntrantRow> GetNewMarketEntrants(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var cutoff = (filter.CareerStartedAfter ?? DateTime.Today.AddYears(-1)).ToString("yyyy-MM-dd");

        var sql = new StringBuilder(@"
SELECT a.Id,
    a.FirstName || ' ' || a.LastName AS FullName,
    a.CrdNumber, a.State, a.CurrentFirmName,
    a.CareerStartDate, a.YearsOfExperience, a.Email, a.HasDisclosures,
    f.RegulatoryAum, f.NumberOfAdvisors,
    strftime('%Y', a.CareerStartDate) || '-Q' ||
        CASE CAST(strftime('%m', a.CareerStartDate) AS INTEGER)
            WHEN 1 THEN '1' WHEN 2 THEN '1' WHEN 3 THEN '1'
            WHEN 4 THEN '2' WHEN 5 THEN '2' WHEN 6 THEN '2'
            WHEN 7 THEN '3' WHEN 8 THEN '3' WHEN 9 THEN '3'
            ELSE '4'
        END AS CareerQuarter
FROM Advisors a
LEFT JOIN Firms f ON f.Id = a.CurrentFirmId
WHERE a.CareerStartDate >= @cutoff");

        cmd.Parameters.AddWithValue("@cutoff", cutoff);
        AppendAdvisorWhere(sql, cmd, filter);
        sql.Append(" ORDER BY a.CareerStartDate DESC LIMIT @pageSize");
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize);
        cmd.CommandText = sql.ToString();

        var results = new List<NewMarketEntrantRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new NewMarketEntrantRow(r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2),
                RS(r, 3), RS(r, 4), RDate(r, 5), RS(r, 11), RI(r, 6),
                RS(r, 7), RB(r, 8), RD(r, 9), RI(r, 10)));

        return results;
    }

    // ── Report 6 — Firm Headcount Trend ──────────────────────────────────

    public List<FirmHeadcountTrendRow> GetFirmHeadcountTrend(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT f.Id, f.Name, f.CrdNumber, f.State,
    f.NumberOfAdvisors, f.PriorAdvisorCount,
    f.NumberOfAdvisors - f.PriorAdvisorCount AS AdvisorCountChange,
    CAST(f.NumberOfAdvisors - f.PriorAdvisorCount AS REAL) / NULLIF(f.PriorAdvisorCount, 0) * 100.0 AS AdvisorCountChangePct,
    f.RegulatoryAum, f.LatestFilingDate,
    (SELECT COUNT(*) FROM Advisors a WHERE a.CurrentFirmId = f.Id AND a.IsFavorited = 1 AND a.IsExcluded = 0) AS PipelineCount
FROM Firms f
WHERE f.IsExcluded = 0 AND f.PriorAdvisorCount IS NOT NULL");

        AppendFirmWhere(sql, cmd, filter);
        sql.Append(" ORDER BY AdvisorCountChange ASC LIMIT @pageSize");
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize);
        cmd.CommandText = sql.ToString();

        var results = new List<FirmHeadcountTrendRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new FirmHeadcountTrendRow(r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2) ?? "",
                RS(r, 3), RI(r, 4), RI(r, 5), RI(r, 6), RD(r, 7),
                RD(r, 8), RS(r, 9), RIZ(r, 10)));

        return results;
    }

    // ── Report 7 — Broker Protocol Firm Directory ─────────────────────────

    public List<BrokerProtocolFirmRow> GetBrokerProtocolDirectory(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT f.Id, f.Name, f.CrdNumber, f.State, f.City, f.Phone, f.Website,
    f.NumberOfAdvisors, f.RegulatoryAum,
    CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0) AS AumPerAdvisor,
    CAST(f.ClientsHighNetWorth AS REAL) * 100.0 / NULLIF(f.NumClients, 0) AS HnwClientPct,
    f.CompensationFeeOnly, f.CompensationCommission, f.CompensationHourly, f.CompensationPerformanceBased,
    (SELECT COUNT(*) FROM Advisors a WHERE a.CurrentFirmId = f.Id AND a.IsFavorited = 1 AND a.IsExcluded = 0) AS FavoritedCount,
    (SELECT COUNT(*) FROM Advisors a WHERE a.CurrentFirmId = f.Id AND a.IsExcluded = 0) AS TotalCount
FROM Firms f
WHERE f.BrokerProtocolMember = 1 AND f.IsExcluded = 0");

        AppendFirmWhere(sql, cmd, filter);
        sql.Append(" ORDER BY f.NumberOfAdvisors DESC LIMIT @pageSize");
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize);
        cmd.CommandText = sql.ToString();

        var results = new List<BrokerProtocolFirmRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var model = DeriveFirmCompensationModel(RB(r, 11), RB(r, 12), RB(r, 13), RB(r, 14));
            results.Add(new BrokerProtocolFirmRow(r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2) ?? "",
                RS(r, 3), RS(r, 4), RS(r, 5), RS(r, 6),
                RI(r, 7), RD(r, 8), RD(r, 9), RD(r, 10),
                model, RIZ(r, 15), RIZ(r, 16)));
        }

        return results;
    }

    // ── Report 8 — Firm AUM Trajectory ───────────────────────────────────

    public List<FirmAumTrajectoryRow> GetFirmAumTrajectory(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT f.Id, f.Name, f.CrdNumber, f.State, f.RegulatoryAum, f.NumberOfAdvisors,
    h1.RegulatoryAum AS Aum1YrAgo,
    h3.RegulatoryAum AS Aum3YrAgo,
    h5.RegulatoryAum AS Aum5YrAgo,
    CAST(f.RegulatoryAum - h1.RegulatoryAum AS REAL) / NULLIF(CAST(h1.RegulatoryAum AS REAL), 0) * 100.0 AS AumChange1YrPct,
    CAST(f.RegulatoryAum - h3.RegulatoryAum AS REAL) / NULLIF(CAST(h3.RegulatoryAum AS REAL), 0) * 100.0 AS AumChange3YrPct,
    CAST(f.RegulatoryAum - h5.RegulatoryAum AS REAL) / NULLIF(CAST(h5.RegulatoryAum AS REAL), 0) * 100.0 AS AumChange5YrPct
FROM Firms f
LEFT JOIN (
    SELECT fah.FirmCrd, fah.RegulatoryAum FROM FirmAumHistory fah
    WHERE fah.SnapshotDate = (SELECT MAX(SnapshotDate) FROM FirmAumHistory WHERE FirmCrd = fah.FirmCrd AND SnapshotDate <= date('now','-1 year'))
) h1 ON h1.FirmCrd = f.CrdNumber
LEFT JOIN (
    SELECT fah.FirmCrd, fah.RegulatoryAum FROM FirmAumHistory fah
    WHERE fah.SnapshotDate = (SELECT MAX(SnapshotDate) FROM FirmAumHistory WHERE FirmCrd = fah.FirmCrd AND SnapshotDate <= date('now','-3 years'))
) h3 ON h3.FirmCrd = f.CrdNumber
LEFT JOIN (
    SELECT fah.FirmCrd, fah.RegulatoryAum FROM FirmAumHistory fah
    WHERE fah.SnapshotDate = (SELECT MAX(SnapshotDate) FROM FirmAumHistory WHERE FirmCrd = fah.FirmCrd AND SnapshotDate <= date('now','-5 years'))
) h5 ON h5.FirmCrd = f.CrdNumber
WHERE f.IsExcluded = 0 AND f.RegulatoryAum IS NOT NULL");

        AppendFirmWhere(sql, cmd, filter);
        sql.Append(" ORDER BY AumChange1YrPct ASC LIMIT @pageSize");
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize);
        cmd.CommandText = sql.ToString();

        var results = new List<FirmAumTrajectoryRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new FirmAumTrajectoryRow(r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2) ?? "",
                RS(r, 3), RD(r, 4), RI(r, 5), RD(r, 6), RD(r, 7), RD(r, 8),
                RD(r, 9), RD(r, 10), RD(r, 11)));

        return results;
    }

    // ── Report 9 — Competitive Landscape Dashboard ────────────────────────

    public List<CompetitiveLandscapeRow> GetCompetitiveLandscape(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var stateFilter = filter.State != null ? " AND a.State = @stateMarket" : "";
        if (filter.State != null) cmd.Parameters.AddWithValue("@stateMarket", filter.State);

        var firmStateFilter = filter.State != null ? " AND f.State = @firmState" : "";
        if (filter.State != null) cmd.Parameters.AddWithValue("@firmState", filter.State);

        var sql = new StringBuilder($@"
WITH MarketTotals AS (
    SELECT
        COUNT(DISTINCT a.Id) AS TotalAdvisors,
        SUM(CAST(f.RegulatoryAum AS REAL)) AS TotalAum
    FROM Advisors a
    LEFT JOIN Firms f ON f.Id = a.CurrentFirmId
    WHERE a.IsExcluded = 0 AND a.RegistrationStatus = 'Active'{stateFilter}
)
SELECT f.Id, f.Name, f.CrdNumber, f.State, f.NumberOfAdvisors, f.RegulatoryAum,
    CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0) AS AumPerAdvisor,
    CAST(f.NumberOfAdvisors AS REAL) * 100.0 / NULLIF(mt.TotalAdvisors, 0) AS AdvisorMarketSharePct,
    CAST(f.RegulatoryAum AS REAL) * 100.0 / NULLIF(mt.TotalAum, 0) AS AumMarketSharePct
FROM Firms f, MarketTotals mt
WHERE f.IsExcluded = 0{firmStateFilter}");

        AppendFirmWhere(sql, cmd, filter, skipState: true);
        sql.Append(" ORDER BY f.NumberOfAdvisors DESC LIMIT 25");
        cmd.CommandText = sql.ToString();

        var results = new List<CompetitiveLandscapeRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new CompetitiveLandscapeRow(r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2) ?? "",
                RS(r, 3), RI(r, 4), RD(r, 5), RD(r, 6), RD(r, 7), RD(r, 8)));

        return results;
    }

    // ── Report 10 — Credential & License Frequency ────────────────────────

    public List<CredentialFrequencyRow> GetCredentialFrequency(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT q.Name, q.Code,
    COUNT(DISTINCT q.AdvisorId) AS AdvisorCount,
    CAST(COUNT(DISTINCT q.AdvisorId) AS REAL) * 100.0 /
        NULLIF((SELECT COUNT(*) FROM Advisors WHERE IsExcluded = 0 AND RegistrationStatus = 'Active'), 0) AS PctOfActiveMarket
FROM Qualifications q
JOIN Advisors a ON a.Id = q.AdvisorId AND a.IsExcluded = 0 AND a.RegistrationStatus = 'Active'
WHERE 1=1");

        if (filter.State != null)
        {
            sql.Append(" AND a.State = @state");
            cmd.Parameters.AddWithValue("@state", filter.State);
        }

        sql.Append(" GROUP BY q.Name, q.Code ORDER BY AdvisorCount DESC LIMIT 50");
        cmd.CommandText = sql.ToString();

        var results = new List<CredentialFrequencyRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new CredentialFrequencyRow(RS(r, 0) ?? "", RS(r, 1), RIZ(r, 2), RD(r, 3)));

        return results;
    }

    // ── Report 11 — Geographic Advisor Density ────────────────────────────

    public List<GeographicDensityRow> GetGeographicDensity(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT a.State,
    COUNT(*) AS AdvisorCount,
    SUM(CASE WHEN a.RegistrationStatus = 'Active' THEN 1 ELSE 0 END) AS ActiveAdvisorCount,
    AVG(CAST(a.YearsOfExperience AS REAL)) AS AvgYearsExperience,
    CAST(SUM(CASE WHEN a.HasDisclosures = 1 THEN 1 ELSE 0 END) AS REAL) / COUNT(*) AS DisclosureRate,
    SUM(CASE WHEN a.IsFavorited = 1 THEN 1 ELSE 0 END) AS FavoritedCount
FROM Advisors a
WHERE a.IsExcluded = 0 AND a.State IS NOT NULL
GROUP BY a.State
ORDER BY AdvisorCount DESC");

        cmd.CommandText = sql.ToString();

        var results = new List<GeographicDensityRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new GeographicDensityRow(RS(r, 0), RIZ(r, 1), RIZ(r, 2), RD(r, 3), RD(r, 4), RIZ(r, 5)));

        return results;
    }

    // ── Report 12 — AUM Concentration by Geography ───────────────────────

    public List<AumConcentrationByGeoRow> GetAumConcentrationByGeo(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT a.State,
    COUNT(*) AS AdvisorCount,
    AVG(CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0)) AS AvgAumPerAdvisor,
    SUM(CASE WHEN CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0) >= 500000000.0 THEN 1 ELSE 0 END) AS AdvisorsAbove500M
FROM Advisors a
LEFT JOIN Firms f ON f.Id = a.CurrentFirmId
WHERE a.IsExcluded = 0 AND a.State IS NOT NULL");

        if (filter.ActiveOnly) sql.Append(" AND a.RegistrationStatus = 'Active'");

        sql.Append(" GROUP BY a.State ORDER BY AvgAumPerAdvisor DESC");
        cmd.CommandText = sql.ToString();

        var results = new List<AumConcentrationByGeoRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new AumConcentrationByGeoRow(RS(r, 0), RIZ(r, 1), RD(r, 2), RIZ(r, 3)));

        return results;
    }

    // ── Report 13 — Disclosure Risk Profile (summary) ────────────────────

    public DisclosureProfileSummaryRow? GetDisclosureProfileSummary(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT
    COUNT(*) AS TotalAdvisors,
    SUM(CASE WHEN a.HasDisclosures = 1 THEN 1 ELSE 0 END) AS AdvisorsWithDisclosures,
    CAST(SUM(CASE WHEN a.HasDisclosures = 1 THEN 1 ELSE 0 END) AS REAL) / NULLIF(COUNT(*), 0) AS DisclosureRate,
    SUM(CASE WHEN a.HasCriminalDisclosure = 1 THEN 1 ELSE 0 END) AS CriminalCount,
    SUM(CASE WHEN a.HasRegulatoryDisclosure = 1 THEN 1 ELSE 0 END) AS RegulatoryCount,
    SUM(CASE WHEN a.HasCivilDisclosure = 1 THEN 1 ELSE 0 END) AS CivilCount,
    SUM(CASE WHEN a.HasCustomerComplaintDisclosure = 1 THEN 1 ELSE 0 END) AS CustomerComplaintCount,
    SUM(CASE WHEN a.HasFinancialDisclosure = 1 THEN 1 ELSE 0 END) AS FinancialCount,
    SUM(CASE WHEN a.HasTerminationDisclosure = 1 THEN 1 ELSE 0 END) AS TerminationCount
FROM Advisors a
WHERE 1=1");

        if (filter.ExcludeExcluded) sql.Append(" AND a.IsExcluded = 0");
        if (filter.ActiveOnly) sql.Append(" AND a.RegistrationStatus = 'Active'");
        if (filter.State != null) { sql.Append(" AND a.State = @state"); cmd.Parameters.AddWithValue("@state", filter.State); }
        if (filter.RecordType != null) { sql.Append(" AND a.RecordType = @rt"); cmd.Parameters.AddWithValue("@rt", filter.RecordType); }
        cmd.CommandText = sql.ToString();

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new DisclosureProfileSummaryRow(RIZ(r, 0), RIZ(r, 1), RD(r, 2),
            RIZ(r, 3), RIZ(r, 4), RIZ(r, 5), RIZ(r, 6), RIZ(r, 7), RIZ(r, 8));
    }

    // ── Report 13 — Disclosure Risk Profile (detail) ─────────────────────

    public List<DisclosureProfileDetailRow> GetDisclosureProfileDetail(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT a.Id,
    a.FirstName || ' ' || a.LastName AS FullName,
    a.CrdNumber, a.State, a.CurrentFirmName,
    a.DisclosureCount,
    a.HasCriminalDisclosure, a.HasRegulatoryDisclosure, a.HasCivilDisclosure,
    a.HasCustomerComplaintDisclosure, a.HasFinancialDisclosure, a.HasTerminationDisclosure,
    (SELECT MAX(d.Date) FROM Disclosures d WHERE d.AdvisorId = a.Id) AS MostRecentDisclosureDate,
    CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0) AS AumPerAdvisor
FROM Advisors a
LEFT JOIN Firms f ON f.Id = a.CurrentFirmId
WHERE a.HasDisclosures = 1");

        if (filter.ExcludeExcluded) sql.Append(" AND a.IsExcluded = 0");
        if (filter.ActiveOnly) sql.Append(" AND a.RegistrationStatus = 'Active'");
        if (filter.State != null) { sql.Append(" AND a.State = @state"); cmd.Parameters.AddWithValue("@state", filter.State); }
        sql.Append(" ORDER BY a.DisclosureCount DESC LIMIT @pageSize");
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize);
        cmd.CommandText = sql.ToString();

        var results = new List<DisclosureProfileDetailRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new DisclosureProfileDetailRow(r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2),
                RS(r, 3), RS(r, 4), RIZ(r, 5),
                RB(r, 6), RB(r, 7), RB(r, 8), RB(r, 9), RB(r, 10), RB(r, 11),
                RDate(r, 12), RD(r, 13)));

        return results;
    }

    // ── Report 14 — Clean Record Premium Advisor List ────────────────────

    public List<CleanRecordAdvisorRow> GetCleanRecordAdvisors(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT a.Id,
    a.FirstName || ' ' || a.LastName AS FullName,
    a.CrdNumber, a.State, a.CurrentFirmName, a.YearsOfExperience, a.Email,
    CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0) AS AumPerAdvisor,
    f.BrokerProtocolMember,
    CAST((julianday('now') - julianday(a.CurrentFirmStartDate)) / 365.25 AS INTEGER) AS TenureYears
FROM Advisors a
LEFT JOIN Firms f ON f.Id = a.CurrentFirmId
WHERE a.HasDisclosures = 0");

        AppendAdvisorWhere(sql, cmd, filter);
        sql.Append(" ORDER BY AumPerAdvisor DESC LIMIT @pageSize");
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize);
        cmd.CommandText = sql.ToString();

        var results = new List<CleanRecordAdvisorRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new CleanRecordAdvisorRow(r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2),
                RS(r, 3), RS(r, 4), RI(r, 5), RS(r, 6), RD(r, 7), RB(r, 8), RI(r, 9)));

        return results;
    }

    // ── Report 15 — Recruiting Pipeline Funnel (summary) ─────────────────

    public PipelineFunnelSummaryRow? GetPipelineFunnelSummary(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
SELECT
    (SELECT COUNT(*) FROM Advisors WHERE IsExcluded = 0 AND RegistrationStatus = 'Active') AS TotalActive,
    (SELECT COUNT(*) FROM Advisors WHERE IsFavorited = 1 AND IsExcluded = 0) AS Favorited,
    (SELECT COUNT(*) FROM Advisors WHERE IsFavorited = 1 AND IsExcluded = 0 AND Email IS NOT NULL AND TRIM(Email) <> '') AS FavoritedWithEmail,
    (SELECT COUNT(*) FROM Advisors WHERE IsImportedToCrm = 1 AND IsExcluded = 0) AS ImportedToCrm";

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        int totalActive = RIZ(r, 0);
        int favorited = RIZ(r, 1);
        int favWithEmail = RIZ(r, 2);
        int imported = RIZ(r, 3);
        decimal? conversionRate = favorited > 0 ? (decimal)imported / favorited : null;
        int enrichmentGap = favorited - favWithEmail;

        return new PipelineFunnelSummaryRow(totalActive, favorited, favWithEmail, imported, conversionRate, enrichmentGap);
    }

    // ── Report 15 — Recruiting Pipeline Funnel (detail) ──────────────────

    public List<PipelineFunnelDetailRow> GetPipelineFunnelDetail(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT a.Id,
    a.FirstName || ' ' || a.LastName AS FullName,
    a.CrdNumber, a.State, a.CurrentFirmName, a.Email,
    a.YearsOfExperience, a.HasDisclosures,
    CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0) AS AumPerAdvisor
FROM Advisors a
LEFT JOIN Firms f ON f.Id = a.CurrentFirmId
WHERE a.IsFavorited = 1 AND a.IsImportedToCrm = 0 AND a.IsExcluded = 0");

        if (filter.State != null) { sql.Append(" AND a.State = @state"); cmd.Parameters.AddWithValue("@state", filter.State); }
        sql.Append(" ORDER BY AumPerAdvisor DESC LIMIT @pageSize");
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize);
        cmd.CommandText = sql.ToString();

        var results = new List<PipelineFunnelDetailRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new PipelineFunnelDetailRow(r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2),
                RS(r, 3), RS(r, 4), RS(r, 5), RI(r, 6), RB(r, 7), RD(r, 8)));

        return results;
    }

    // ── Report 16 — Contact Coverage Gap ─────────────────────────────────

    public List<ContactCoverageGapRow> GetContactCoverageGap(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT a.Id,
    a.FirstName || ' ' || a.LastName AS FullName,
    a.CrdNumber, a.State, a.CurrentFirmName, a.YearsOfExperience,
    f.Phone AS FirmPhone, f.Website,
    CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0) AS AumPerAdvisor
FROM Advisors a
LEFT JOIN Firms f ON f.Id = a.CurrentFirmId
WHERE (a.Email IS NULL OR TRIM(a.Email) = '') AND a.IsExcluded = 0");

        if (filter.ActiveOnly) sql.Append(" AND a.RegistrationStatus = 'Active'");
        if (filter.State != null) { sql.Append(" AND a.State = @state"); cmd.Parameters.AddWithValue("@state", filter.State); }
        if (filter.RecordType != null) { sql.Append(" AND a.RecordType = @rt"); cmd.Parameters.AddWithValue("@rt", filter.RecordType); }
        sql.Append(" ORDER BY AumPerAdvisor DESC LIMIT @pageSize");
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize);
        cmd.CommandText = sql.ToString();

        var results = new List<ContactCoverageGapRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new ContactCoverageGapRow(r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2),
                RS(r, 3), RS(r, 4), RI(r, 5), RS(r, 6), RS(r, 7), RD(r, 8)));

        return results;
    }

    // ── Report 17 — Firm Stability Signal ────────────────────────────────

    public List<FirmStabilityRow> GetFirmStabilitySignal(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT f.Id, f.Name, f.CrdNumber, f.State, f.RegulatoryAum,
    f.NumberOfAdvisors, f.PriorAdvisorCount, f.LatestFilingDate,
    CAST(julianday('now') - julianday(f.LatestFilingDate) AS INTEGER) AS DaysSinceLastFiling,
    h1.RegulatoryAum AS Aum1YrAgo,
    CAST(f.RegulatoryAum - h1.RegulatoryAum AS REAL) / NULLIF(CAST(h1.RegulatoryAum AS REAL), 0) * 100.0 AS AumChange1YrPct,
    f.NumberOfAdvisors - f.PriorAdvisorCount AS HeadcountChange,
    (SELECT AVG(CAST(a.DisclosureCount AS REAL)) FROM Advisors a WHERE a.CurrentFirmId = f.Id AND a.IsExcluded = 0) AS AvgAdvisorDisclosureCount
FROM Firms f
LEFT JOIN (
    SELECT fah.FirmCrd, fah.RegulatoryAum FROM FirmAumHistory fah
    WHERE fah.SnapshotDate = (SELECT MAX(SnapshotDate) FROM FirmAumHistory WHERE FirmCrd = fah.FirmCrd AND SnapshotDate <= date('now','-1 year'))
) h1 ON h1.FirmCrd = f.CrdNumber
WHERE f.IsExcluded = 0");

        AppendFirmWhere(sql, cmd, filter);
        sql.Append(" ORDER BY DaysSinceLastFiling DESC LIMIT @pageSize");
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize);
        cmd.CommandText = sql.ToString();

        var raw = new List<(int Id, string Name, string Crd, string? State,
            decimal? Aum, int? NumAdvisors, int? PriorAdvisors, string? FilingDate,
            int? DaysSinceFiling, decimal? AumChange1Yr, int? HeadcountChange,
            decimal? AvgDis)>();

        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
                raw.Add((r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2) ?? "", RS(r, 3),
                    RD(r, 4), RI(r, 5), RI(r, 6), RS(r, 7),
                    RI(r, 8), RD(r, 10), RI(r, 11), RD(r, 12)));
        }

        return raw.Select(x =>
        {
            int score = 0;
            if (x.DaysSinceFiling.HasValue && x.DaysSinceFiling > 365) score += 25;
            if (x.AumChange1Yr.HasValue && x.AumChange1Yr < -10m) score += 25;
            if (x.HeadcountChange.HasValue && x.HeadcountChange < 0) score += 20;
            if (x.AvgDis.HasValue && x.AvgDis > 0.5m) score += 20;
            if (x.PriorAdvisors.HasValue && x.NumAdvisors.HasValue
                && x.PriorAdvisors > x.NumAdvisors) score += 10;

            return new FirmStabilityRow(x.Id, x.Name, x.Crd, x.State, x.Aum,
                x.NumAdvisors, x.PriorAdvisors, x.FilingDate,
                x.DaysSinceFiling, x.AumChange1Yr, x.HeadcountChange, x.AvgDis, score);
        })
        .OrderByDescending(x => x.InstabilityScore)
        .Take(filter.PageSize)
        .ToList();
    }

    // ── Report 18 — Compensation Model Analysis ───────────────────────────

    public List<CompensationModelRow> GetCompensationAnalysis(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT
    f.CompensationFeeOnly, f.CompensationCommission,
    f.CompensationHourly, f.CompensationPerformanceBased,
    COUNT(*) AS FirmCount,
    SUM(f.NumberOfAdvisors) AS AdvisorCount,
    AVG(CAST(f.RegulatoryAum AS REAL)) AS AvgRegulatoryAum,
    AVG(CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0)) AS AvgAumPerAdvisor,
    AVG(CAST(f.ClientsHighNetWorth AS REAL) * 100.0 / NULLIF(f.NumClients, 0)) AS AvgHnwClientPct
FROM Firms f
WHERE f.IsExcluded = 0");

        AppendFirmWhere(sql, cmd, filter);
        sql.Append(" GROUP BY f.CompensationFeeOnly, f.CompensationCommission, f.CompensationHourly, f.CompensationPerformanceBased");
        cmd.CommandText = sql.ToString();

        var results = new List<CompensationModelRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            bool? fo = r.IsDBNull(0) ? null : RB(r, 0);
            bool? co = r.IsDBNull(1) ? null : RB(r, 1);
            bool? ho = r.IsDBNull(2) ? null : RB(r, 2);
            bool? pb = r.IsDBNull(3) ? null : RB(r, 3);
            var model = DeriveFirmCompensationModel(fo, co, ho, pb);
            results.Add(new CompensationModelRow(model, RIZ(r, 4), RI(r, 5), RD(r, 6), RD(r, 7), RD(r, 8)));
        }

        // Merge rows that share the same derived label
        return results
            .GroupBy(x => x.CompensationModel)
            .Select(g => new CompensationModelRow(
                g.Key,
                g.Sum(x => x.FirmCount),
                g.Sum(x => x.AdvisorCount),
                AverageDecimal(g.Where(x => x.AvgRegulatoryAum.HasValue).Select(x => x.AvgRegulatoryAum!.Value)),
                AverageDecimal(g.Where(x => x.AvgAumPerAdvisor.HasValue).Select(x => x.AvgAumPerAdvisor!.Value)),
                AverageDecimal(g.Where(x => x.AvgHnwClientPct.HasValue).Select(x => x.AvgHnwClientPct!.Value))))
            .OrderByDescending(x => x.FirmCount)
            .ToList();
    }

    // ── Report 19 — High-Net-Worth Focus Firm Finder ──────────────────────

    public List<HnwFirmRow> GetHnwFocusFirms(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        int minHnw = filter.MinHnwClientPct ?? 30;

        var sql = new StringBuilder(@"
SELECT f.Id, f.Name, f.CrdNumber, f.State, f.NumberOfAdvisors,
    f.RegulatoryAum,
    CAST(f.RegulatoryAum AS REAL) / NULLIF(f.NumberOfAdvisors, 0) AS AumPerAdvisor,
    CAST(f.ClientsHighNetWorth AS REAL) * 100.0 / NULLIF(f.NumClients, 0) AS HnwClientPct,
    f.CompensationFeeOnly, f.BrokerProtocolMember, f.PrivateFundCount
FROM Firms f
WHERE f.IsExcluded = 0
    AND f.NumClients > 0
    AND CAST(f.ClientsHighNetWorth AS REAL) * 100.0 / NULLIF(f.NumClients, 0) >= @minHnw");

        cmd.Parameters.AddWithValue("@minHnw", minHnw);
        AppendFirmWhere(sql, cmd, filter, skipHnw: true);
        sql.Append(" ORDER BY HnwClientPct DESC LIMIT @pageSize");
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize);
        cmd.CommandText = sql.ToString();

        var results = new List<HnwFirmRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            decimal? hnw = RD(r, 7);
            decimal? aumPer = RD(r, 6);
            bool? feeOnly = r.IsDBNull(8) ? null : RB(r, 8);
            bool? bp = r.IsDBNull(9) ? null : RB(r, 9);
            int? pfc = RI(r, 10);

            int score = 0;
            if (hnw.HasValue && hnw >= 80) score += 40;
            else if (hnw.HasValue && hnw >= 60) score += 25;
            else if (hnw.HasValue && hnw >= 40) score += 15;
            if (aumPer.HasValue && aumPer >= 1_000_000m) score += 30;
            else if (aumPer.HasValue && aumPer >= 500_000m) score += 15;
            if (feeOnly == true) score += 20;
            if (bp == true) score += 10;
            if (pfc.HasValue && pfc > 0) score += 10;

            results.Add(new HnwFirmRow(r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2) ?? "",
                RS(r, 3), RI(r, 4), RD(r, 5), aumPer, hnw, feeOnly, bp, pfc, score));
        }

        return results.OrderByDescending(x => x.UpmarketScore).ToList();
    }

    // ── Report 20 — Advisor Multi-State Registration Map ─────────────────

    public List<MultiStateRegistrationRow> GetMultiStateRegistrationMap(ReportFilter filter)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
SELECT a.Id,
    a.FirstName || ' ' || a.LastName AS FullName,
    a.CrdNumber, a.State, a.CurrentFirmName, a.YearsOfExperience,
    COUNT(DISTINCT ar.StateCode) AS StateRegistrationCount,
    GROUP_CONCAT(DISTINCT ar.StateCode) AS RegisteredStates
FROM Advisors a
LEFT JOIN AdvisorRegistrations ar ON ar.AdvisorId = a.Id
WHERE 1=1");

        AppendAdvisorWhere(sql, cmd, filter);

        sql.Append(" GROUP BY a.Id");

        if (filter.MinStateRegistrationCount.HasValue)
        {
            sql.Append(" HAVING COUNT(DISTINCT ar.StateCode) >= @minStateReg");
            cmd.Parameters.AddWithValue("@minStateReg", filter.MinStateRegistrationCount.Value);
        }

        sql.Append(" ORDER BY StateRegistrationCount DESC LIMIT @pageSize");
        cmd.Parameters.AddWithValue("@pageSize", filter.PageSize);
        cmd.CommandText = sql.ToString();

        var results = new List<MultiStateRegistrationRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            int stateCount = RIZ(r, 6);
            string tier = stateCount <= 1 ? "Single-State" : stateCount <= 4 ? "Regional" : "National";
            results.Add(new MultiStateRegistrationRow(r.GetInt32(0), RS(r, 1) ?? "", RS(r, 2),
                RS(r, 3), RS(r, 4), RI(r, 5), stateCount, RS(r, 7), tier));
        }

        return results;
    }

    // ── Private filter helpers ────────────────────────────────────────────

    /// <summary>Appends advisor-level WHERE conditions (excluding the leading WHERE keyword).</summary>
    private static void AppendAdvisorWhere(StringBuilder sql, SqliteCommand cmd, ReportFilter f,
        string a = "a", string firm = "f")
    {
        if (f.ExcludeExcluded) sql.Append($" AND {a}.IsExcluded = 0");
        if (f.ActiveOnly) sql.Append($" AND {a}.RegistrationStatus = 'Active'");
        if (f.State != null) { sql.Append($" AND {a}.State = @state"); cmd.Parameters.AddWithValue("@state", f.State); }
        if (f.RecordType != null) { sql.Append($" AND {a}.RecordType = @rt"); cmd.Parameters.AddWithValue("@rt", f.RecordType); }
        if (f.FirmCrd != null) { sql.Append($" AND {a}.CurrentFirmCrd = @firmCrd"); cmd.Parameters.AddWithValue("@firmCrd", f.FirmCrd); }
        if (f.MinYearsExperience.HasValue) { sql.Append($" AND {a}.YearsOfExperience >= @minYrs"); cmd.Parameters.AddWithValue("@minYrs", f.MinYearsExperience.Value); }
        if (f.MaxYearsExperience.HasValue) { sql.Append($" AND {a}.YearsOfExperience <= @maxYrs"); cmd.Parameters.AddWithValue("@maxYrs", f.MaxYearsExperience.Value); }
        if (f.NoDisclosuresOnly) sql.Append($" AND {a}.HasDisclosures = 0");
        if (f.FavoritedOnly) sql.Append($" AND {a}.IsFavorited = 1");
        if (f.MinTotalFirmCount.HasValue) { sql.Append($" AND {a}.TotalFirmCount >= @minFirms"); cmd.Parameters.AddWithValue("@minFirms", f.MinTotalFirmCount.Value); }
        if (f.CareerStartedAfter.HasValue) { sql.Append($" AND {a}.CareerStartDate >= @careerAfter"); cmd.Parameters.AddWithValue("@careerAfter", f.CareerStartedAfter.Value.ToString("yyyy-MM-dd")); }
        if (f.BrokerProtocolOnly) sql.Append($" AND {firm}.BrokerProtocolMember = 1");
        if (f.MinRegulatoryAum.HasValue) { sql.Append($" AND CAST({firm}.RegulatoryAum AS REAL) >= @minAum"); cmd.Parameters.AddWithValue("@minAum", (double)f.MinRegulatoryAum.Value); }
        if (f.MinAdvisors.HasValue) { sql.Append($" AND {firm}.NumberOfAdvisors >= @minAdv"); cmd.Parameters.AddWithValue("@minAdv", f.MinAdvisors.Value); }
        if (f.MinHnwClientPct.HasValue) { sql.Append($" AND CAST({firm}.ClientsHighNetWorth AS REAL) * 100.0 / NULLIF({firm}.NumClients, 0) >= @minHnwAdv"); cmd.Parameters.AddWithValue("@minHnwAdv", f.MinHnwClientPct.Value); }
        if (f.CompensationType != null)
        {
            switch (f.CompensationType)
            {
                case "FeeOnly": sql.Append($" AND {firm}.CompensationFeeOnly = 1"); break;
                case "Commission": sql.Append($" AND {firm}.CompensationCommission = 1"); break;
                case "Both": sql.Append($" AND {firm}.CompensationFeeOnly = 1 AND {firm}.CompensationCommission = 1"); break;
            }
        }
    }

    /// <summary>Appends firm-level WHERE conditions for firm-only queries.</summary>
    private static void AppendFirmWhere(StringBuilder sql, SqliteCommand cmd, ReportFilter f,
        bool skipState = false, bool skipHnw = false)
    {
        if (!skipState && f.State != null) { sql.Append(" AND f.State = @state"); cmd.Parameters.AddWithValue("@state", f.State); }
        if (f.MinRegulatoryAum.HasValue) { sql.Append(" AND CAST(f.RegulatoryAum AS REAL) >= @minAum"); cmd.Parameters.AddWithValue("@minAum", (double)f.MinRegulatoryAum.Value); }
        if (f.MinAdvisors.HasValue) { sql.Append(" AND f.NumberOfAdvisors >= @minAdv"); cmd.Parameters.AddWithValue("@minAdv", f.MinAdvisors.Value); }
        if (!skipHnw && f.MinHnwClientPct.HasValue) { sql.Append(" AND CAST(f.ClientsHighNetWorth AS REAL) * 100.0 / NULLIF(f.NumClients, 0) >= @minHnw"); cmd.Parameters.AddWithValue("@minHnw", f.MinHnwClientPct.Value); }
        if (f.CompensationType != null)
        {
            switch (f.CompensationType)
            {
                case "FeeOnly": sql.Append(" AND f.CompensationFeeOnly = 1"); break;
                case "Commission": sql.Append(" AND f.CompensationCommission = 1"); break;
                case "Both": sql.Append(" AND f.CompensationFeeOnly = 1 AND f.CompensationCommission = 1"); break;
            }
        }
    }
}
