namespace AdvisorLeads.Models;

/// <summary>
/// Represents a single search result from the EDGAR Full-Text Search (EFTS) API.
/// These are filing documents that match specific M&A or business keywords.
/// </summary>
public class EdgarSearchResult
{
    public int Id { get; set; }
    /// <summary>CRD number of the matched firm (if we can resolve it).</summary>
    public string? FirmCrd { get; set; }
    /// <summary>Company name from EDGAR.</summary>
    public string CompanyName { get; set; } = string.Empty;
    /// <summary>CIK number from EDGAR.</summary>
    public string? Cik { get; set; }
    /// <summary>EDGAR accession number.</summary>
    public string AccessionNumber { get; set; } = string.Empty;
    /// <summary>Form type (e.g., "10-K", "ADV", "8-K").</summary>
    public string? FormType { get; set; }
    /// <summary>Filing date.</summary>
    public DateTime? FilingDate { get; set; }
    /// <summary>The search query that produced this match.</summary>
    public string SearchQuery { get; set; } = string.Empty;
    /// <summary>Text snippet showing the matched context.</summary>
    public string? Snippet { get; set; }
    /// <summary>URL to the filing on EDGAR.</summary>
    public string? FilingUrl { get; set; }
    /// <summary>Category of the search (e.g., "M&A Signal", "Succession", "Compliance").</summary>
    public string? Category { get; set; }
    /// <summary>Relevance score from the search engine.</summary>
    public double? RelevanceScore { get; set; }
    public DateTime CreatedAt { get; set; }
}
