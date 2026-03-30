namespace AdvisorLeads.Models;

public class FinraSanction
{
    public int Id { get; set; }
    public int? AdvisorId { get; set; }
    public string? AdvisorCrd { get; set; }
    public string? FirmCrd { get; set; }
    // "Fine","Suspension","Bar","RevocationOrder","BrokerDealerSanction"
    public string SanctionType { get; set; } = string.Empty;
    // "FINRA","SEC","State","Other"
    public string? InitiatedBy { get; set; }
    public decimal? FineAmount { get; set; }
    public DateTime? SanctionDate { get; set; }
    public DateTime? SuspensionStart { get; set; }
    public DateTime? SuspensionEnd { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string? SourceUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
