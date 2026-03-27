namespace AdvisorLeads.Models;

/// <summary>
/// Represents a single SEC EDGAR filing document for a firm.
/// Sourced from the EDGAR Submissions API (data.sec.gov/submissions/).
/// </summary>
public class FirmFiling
{
    public int Id { get; set; }
    /// <summary>CRD number of the firm (from our Firms table).</summary>
    public string FirmCrd { get; set; } = string.Empty;
    /// <summary>SEC CIK number (Central Index Key).</summary>
    public string? Cik { get; set; }
    /// <summary>EDGAR accession number (unique filing identifier).</summary>
    public string AccessionNumber { get; set; } = string.Empty;
    /// <summary>Form type (e.g., "ADV", "ADV/A", "ADV-W", "ADV-E").</summary>
    public string FormType { get; set; } = string.Empty;
    /// <summary>Date the filing was submitted to EDGAR.</summary>
    public DateTime FilingDate { get; set; }
    /// <summary>Date the filing was accepted by EDGAR.</summary>
    public DateTime? AcceptanceDate { get; set; }
    /// <summary>Primary document filename.</summary>
    public string? PrimaryDocument { get; set; }
    /// <summary>Filing description/title.</summary>
    public string? Description { get; set; }
    /// <summary>Direct URL to the filing document on EDGAR.</summary>
    public string? FilingUrl { get; set; }
    /// <summary>Size of the filing in bytes.</summary>
    public long? FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
}
