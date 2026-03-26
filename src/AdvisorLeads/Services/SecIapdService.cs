using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// SEC IAPD service stub. The SEC does not expose a public JSON API for individual
/// adviser representative searches. The FINRA BrokerCheck API serves as the primary
/// and most reliable source for individual adviser data (including IA-registered reps).
/// This service exists to satisfy the DataSyncService interface but returns empty results.
/// </summary>
public class SecIapdService
{
    public Task<List<Advisor>> SearchAdvisorsAsync(string query, string? state = null,
        int from = 0, int size = 12, IProgress<string>? progress = null)
    {
        progress?.Report("SEC IAPD: Individual search API not publicly available. Using FINRA data.");
        return Task.FromResult(new List<Advisor>());
    }

    public Task<Advisor?> GetAdvisorDetailAsync(string crd, IProgress<string>? progress = null)
    {
        return Task.FromResult<Advisor?>(null);
    }
}

