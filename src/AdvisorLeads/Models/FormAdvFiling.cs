namespace AdvisorLeads.Models;

/// <summary>
/// Represents a single Form ADV filing record from the SEC historical CSV data.
/// Captures the firm's state at a point in time for trend analysis.
/// </summary>
public class FormAdvFiling
{
    public int Id { get; set; }
    /// <summary>CRD number of the firm.</summary>
    public string FirmCrd { get; set; } = string.Empty;
    /// <summary>Date the Form ADV was filed.</summary>
    public DateTime FilingDate { get; set; }
    /// <summary>Filing type: "Initial", "Annual", "Amendment", "Other".</summary>
    public string? FilingType { get; set; }
    /// <summary>Discretionary Regulatory AUM at filing time.</summary>
    public decimal? RegulatoryAum { get; set; }
    /// <summary>Non-discretionary Regulatory AUM at filing time.</summary>
    public decimal? RegulatoryAumNonDiscretionary { get; set; }
    /// <summary>Total AUM (discretionary + non-discretionary).</summary>
    public decimal? TotalAum { get; set; }
    /// <summary>Total number of employees at filing time.</summary>
    public int? NumberOfEmployees { get; set; }
    /// <summary>Number of investment adviser representatives.</summary>
    public int? NumberOfAdvisors { get; set; }
    /// <summary>Advisor headcount from this filing (populated from reporting pipeline).</summary>
    public int? AdvisorCount { get; set; }
    /// <summary>Number of clients at filing time.</summary>
    public int? NumClients { get; set; }
    /// <summary>Registration status at filing time.</summary>
    public string? RegistrationStatus { get; set; }
    /// <summary>Primary business name at filing time.</summary>
    public string? BusinessName { get; set; }
    public DateTime CreatedAt { get; set; }
}
