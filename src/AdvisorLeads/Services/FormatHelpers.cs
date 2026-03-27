namespace AdvisorLeads.Services;

/// <summary>
/// Shared formatting helpers used across multiple services.
/// </summary>
public static class FormatHelpers
{
    /// <summary>Formats a decimal AUM value as a human-readable string (e.g. $1.2B, $350M, $42K).</summary>
    public static string FormatAum(decimal aum)
    {
        if (aum >= 1_000_000_000) return $"${aum / 1_000_000_000:F1}B";
        if (aum >= 1_000_000) return $"${aum / 1_000_000:F1}M";
        if (aum >= 1_000) return $"${aum / 1_000:F0}K";
        return $"${aum:F0}";
    }
}