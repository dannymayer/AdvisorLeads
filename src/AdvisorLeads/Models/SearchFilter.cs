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
    public bool? IsImportedToCrm { get; set; }
    public string? Source { get; set; }
    public int? MinYearsExperience { get; set; }
    public int? MaxYearsExperience { get; set; }
    public string? SortBy { get; set; } = "LastName";
    public bool SortDescending { get; set; } = false;
}
