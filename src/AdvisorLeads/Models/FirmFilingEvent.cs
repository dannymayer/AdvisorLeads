namespace AdvisorLeads.Models;

/// <summary>
/// Records a significant change detected in a firm's data between two data snapshots.
/// Used for M&A signal detection, compliance monitoring, and alerts.
/// </summary>
public class FirmFilingEvent
{
    public int Id { get; set; }
    /// <summary>CRD number of the firm.</summary>
    public string FirmCrd { get; set; } = string.Empty;
    /// <summary>Date the change was detected.</summary>
    public DateTime EventDate { get; set; }
    /// <summary>Type of event: AUM_JUMP, AUM_DROP, AUM_SURGE, STATUS_CHANGE, GROWTH_SIGNAL, CLIENT_SHIFT, EMPLOYEE_CHANGE, NEW_REGISTRATION, WITHDRAWAL.</summary>
    public string EventType { get; set; } = string.Empty;
    /// <summary>Human-readable description of the change.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Previous value (for comparison context).</summary>
    public string? OldValue { get; set; }
    /// <summary>New value.</summary>
    public string? NewValue { get; set; }
    /// <summary>Percentage change (for numeric changes).</summary>
    public double? PercentChange { get; set; }
    /// <summary>Severity: HIGH, MEDIUM, LOW.</summary>
    public string Severity { get; set; } = "MEDIUM";
    /// <summary>Whether this event has been reviewed/acknowledged by the user.</summary>
    public bool IsReviewed { get; set; }
    public DateTime CreatedAt { get; set; }
}
