using AdvisorLeads.Models;
using AdvisorLeads.Services;

namespace AdvisorLeads.Abstractions;

public interface IDataSyncService
{
    Task<SyncResult> FetchAndSyncAsync(string query, string? state = null,
        bool includeFinra = true, bool includeSec = true,
        IProgress<string>? progress = null);
    Task<Advisor?> RefreshAdvisorAsync(string crd, IProgress<string>? progress = null);
}
