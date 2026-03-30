namespace AdvisorLeads.Models;

public class FirmAumAlertRule
{
    public int Id { get; set; }

    /// <summary>CRD of the firm being monitored.</summary>
    public string FirmCrd { get; set; } = string.Empty;

    /// <summary>Denormalized firm name for display without joins.</summary>
    public string? FirmName { get; set; }

    /// <summary>"CrossAbove" or "CrossBelow"</summary>
    public string ThresholdType { get; set; } = string.Empty;

    /// <summary>AUM threshold in USD (e.g., 500_000_000 = $500M).</summary>
    public decimal ThresholdAmount { get; set; }

    /// <summary>False = rule already triggered and awaiting reset; True = actively monitoring.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC time the threshold was last triggered (null = never triggered).</summary>
    public DateTime? LastTriggeredAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
