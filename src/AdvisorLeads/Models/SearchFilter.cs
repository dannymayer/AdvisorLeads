namespace AdvisorLeads.Models;

public class SearchFilter
{
    public string? NameQuery { get; set; }
    public string? State { get; set; }
    public string? FirmName { get; set; }
    public string? FirmCrd { get; set; }
    public string? CrdNumber { get; set; }
    public string? RegistrationStatus { get; set; }
    public string? LicenseType { get; set; }
    public bool? HasDisclosures { get; set; }
    public bool IncludeExcluded { get; set; } = false;
    public bool ShowFavoritesOnly { get; set; } = false;
    public bool? IsImportedToCrm { get; set; }
    public string? Source { get; set; }
    // "Investment Advisor Representative", "Registered Representative", "Investment Adviser", "Broker-Dealer"
    public string? RecordType { get; set; }
    public int? MinYearsExperience { get; set; }
    public int? MaxYearsExperience { get; set; }
    public string? City { get; set; }
    public int? MinDisclosureCount { get; set; }
    public string? DisclosureType { get; set; }  // "Criminal", "Regulatory", "Civil", "Customer Complaint", "Financial", "Termination"
    public string? FirmRecordType { get; set; }
    public string? SortBy { get; set; } = "LastName";
    public bool SortDescending { get; set; } = false;
    public int PageSize { get; set; } = 500;
    public int PageNumber { get; set; } = 1;

    // ── Enrichment filters ──────────────────────────────────────────────
    public bool? HasActiveSanction { get; set; }
    public bool? HasSecEnforcementAction { get; set; }
    public bool? HasRecentFirmChange { get; set; }
    public bool? HasCourtRecord { get; set; }
    public string? RegistrationLevel { get; set; }
}
