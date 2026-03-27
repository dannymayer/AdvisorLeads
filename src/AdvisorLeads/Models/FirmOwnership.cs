namespace AdvisorLeads.Models;

/// <summary>
/// Represents an ownership record from Form ADV Schedule A (direct owners)
/// or Schedule B (indirect owners). Tracks who owns/controls an RIA firm.
/// </summary>
public class FirmOwnership
{
    public int Id { get; set; }
    /// <summary>CRD number of the firm.</summary>
    public string FirmCrd { get; set; } = string.Empty;
    /// <summary>Date of the Form ADV filing this ownership was reported in.</summary>
    public DateTime FilingDate { get; set; }
    /// <summary>Name of the owner or control person.</summary>
    public string OwnerName { get; set; } = string.Empty;
    /// <summary>Title or role (e.g. "CEO", "Managing Member", "Director").</summary>
    public string? Title { get; set; }
    /// <summary>Ownership percentage (0-100). Null if not reported.</summary>
    public decimal? OwnershipPercent { get; set; }
    /// <summary>True = Schedule A (direct owner), False = Schedule B (indirect owner).</summary>
    public bool IsDirectOwner { get; set; }
    /// <summary>Entity type: "Individual", "Corporation", "LLC", "Partnership", etc.</summary>
    public string? EntityType { get; set; }
    /// <summary>Status: "Current", "Former".</summary>
    public string? Status { get; set; }
    /// <summary>CRD number of the owner if they are a registered entity.</summary>
    public string? OwnerCrd { get; set; }
    public DateTime CreatedAt { get; set; }
}
