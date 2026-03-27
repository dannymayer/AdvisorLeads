namespace AdvisorLeads.Models;

/// <summary>
/// Monthly snapshot of a firm's key financial metrics.
/// One row is created per firm per month when the SEC monthly CSV is processed.
/// Used for AUM growth tracking, trend analysis, and peer comparison.
/// </summary>
public class FirmAumHistory
{
    public int Id { get; set; }
    /// <summary>CRD number of the firm.</summary>
    public string FirmCrd { get; set; } = string.Empty;
    /// <summary>Snapshot date (first of the month, e.g. 2024-03-01).</summary>
    public DateTime SnapshotDate { get; set; }
    /// <summary>Discretionary Regulatory AUM in USD.</summary>
    public decimal? RegulatoryAum { get; set; }
    /// <summary>Non-discretionary Regulatory AUM in USD.</summary>
    public decimal? RegulatoryAumNonDiscretionary { get; set; }
    /// <summary>Total AUM (discretionary + non-discretionary).</summary>
    public decimal? TotalAum { get; set; }
    /// <summary>Total number of employees.</summary>
    public int? NumberOfEmployees { get; set; }
    /// <summary>Number of investment adviser representatives.</summary>
    public int? NumberOfAdvisors { get; set; }
    /// <summary>Approximate number of clients.</summary>
    public int? NumClients { get; set; }
    /// <summary>Data source identifier (e.g. "SEC_MONTHLY_202403").</summary>
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; }
}
