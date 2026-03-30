using AdvisorLeads.Models;

namespace AdvisorLeads.Abstractions;

/// <summary>
/// Strategy interface for advisor data providers (FINRA, SEC, etc.).
/// Allows DataSyncService to dispatch search and detail requests uniformly.
/// </summary>
public interface IAdvisorDataSource
{
    /// <summary>Identifies the data source (e.g., "FINRA", "SEC").</summary>
    string SourceTag { get; }

    Task<List<Advisor>> SearchAsync(string query, string? state,
        IProgress<string>? progress, CancellationToken token = default);

    Task<Advisor?> GetDetailAsync(string crd, IProgress<string>? progress = null);
}
