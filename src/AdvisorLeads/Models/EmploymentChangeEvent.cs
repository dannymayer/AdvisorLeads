namespace AdvisorLeads.Models;

public class EmploymentChangeEvent
{
    public int Id { get; set; }
    public int AdvisorId { get; set; }
    public string AdvisorCrd { get; set; } = string.Empty;
    public string? AdvisorName { get; set; }
    public string? FromFirmName { get; set; }
    public string? FromFirmCrd { get; set; }
    public string? ToFirmName { get; set; }
    public string? ToFirmCrd { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime? EffectiveDate { get; set; }
}
