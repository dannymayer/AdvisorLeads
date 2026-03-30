namespace AdvisorLeads.Models;

public class AdvisorCourtRecord
{
    public int Id { get; set; }
    public int? AdvisorId { get; set; }
    public string AdvisorCrd { get; set; } = string.Empty;
    public string CaseName { get; set; } = string.Empty;
    public string? Court { get; set; }
    public string? DocketNumber { get; set; }
    // "SecuritiesViolation","Bankruptcy","CivilFraud","OtherCivil"
    public string? CaseType { get; set; }
    public DateTime? FilingDate { get; set; }
    // "Open","Closed"
    public string? Status { get; set; }
    public string? CaseUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
