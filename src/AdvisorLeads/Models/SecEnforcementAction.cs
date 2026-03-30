namespace AdvisorLeads.Models;

public class SecEnforcementAction
{
    public int Id { get; set; }
    public string? AdvisorCrd { get; set; }
    public string? FirmCrd { get; set; }
    public string? RespondentName { get; set; }
    // "AdminProceeding","LitigatedOrder","AAO"
    public string ActionType { get; set; } = string.Empty;
    public DateTime? FileDate { get; set; }
    public string? Description { get; set; }
    public string? CaseUrl { get; set; }
    public string? ReleaseNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}
