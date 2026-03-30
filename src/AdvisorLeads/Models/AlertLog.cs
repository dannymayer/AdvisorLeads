namespace AdvisorLeads.Models;

public class AlertLog
{
    public int Id { get; set; }

    /// <summary>
    /// Category of alert: Disclosure, FirmChange, AumThreshold, BrokerProtocol,
    /// StatusChange, NewRegistration, LicenseChange
    /// </summary>
    public string AlertType { get; set; } = string.Empty;

    /// <summary>Severity: High, Medium, Low</summary>
    public string Severity { get; set; } = "Medium";

    /// <summary>"Advisor" or "Firm"</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>CRD number of the advisor or firm.</summary>
    public string EntityCrd { get; set; } = string.Empty;

    /// <summary>Denormalized display name (e.g., "John Smith" or "Merrill Lynch").</summary>
    public string? EntityName { get; set; }

    /// <summary>One-line human-readable summary shown in the alert feed.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Extended detail text or JSON payload for the detail pane.</summary>
    public string? Detail { get; set; }

    /// <summary>Previous value of the changed field (for diff display).</summary>
    public string? OldValue { get; set; }

    /// <summary>New value of the changed field.</summary>
    public string? NewValue { get; set; }

    /// <summary>Direct BrokerCheck or SEC IAPD URL for quick navigation.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>UTC time the alert was detected by the background service.</summary>
    public DateTime DetectedAt { get; set; }

    /// <summary>True once the user has opened or scrolled past this alert.</summary>
    public bool IsRead { get; set; }

    /// <summary>True once the user has explicitly dismissed/archived this alert.</summary>
    public bool IsAcknowledged { get; set; }

    public DateTime CreatedAt { get; set; }
}
