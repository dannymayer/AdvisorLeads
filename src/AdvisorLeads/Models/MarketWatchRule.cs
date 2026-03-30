namespace AdvisorLeads.Models;

public class MarketWatchRule
{
    public int Id { get; set; }

    /// <summary>User-defined name for the rule (e.g., "Texas IARs 10+ Years").</summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>Two-letter state code filter. Null = all states.</summary>
    public string? State { get; set; }

    /// <summary>
    /// "Investment Advisor Representative", "Registered Representative", or null = both.
    /// </summary>
    public string? RecordType { get; set; }

    /// <summary>License substring filter (e.g., "Series 7"). Null = any.</summary>
    public string? LicenseContains { get; set; }

    /// <summary>Minimum years of experience. Null = no minimum.</summary>
    public int? MinYearsExperience { get; set; }

    /// <summary>False = rule is paused.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
