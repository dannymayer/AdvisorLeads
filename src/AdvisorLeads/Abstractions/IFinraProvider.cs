using AdvisorLeads.Models;

namespace AdvisorLeads.Abstractions;

public interface IFinraProvider
{
    Task<List<Advisor>> SearchAdvisorsAsync(string query, string? state = null,
        int from = 0, int size = 100, IProgress<string>? progress = null, int maxResults = 10000);
    Task<Advisor?> GetAdvisorDetailAsync(string crd, IProgress<string>? progress = null);
    Task<List<Advisor>> SearchByFirmAsync(string firmName, IProgress<string>? progress = null);
    Task<List<Advisor>> FetchBulkAdvisorsAsync(IProgress<string>? progress = null,
        CancellationToken cancellationToken = default, int maxPagesPerLetter = 50,
        bool activeOnly = false);
    Task<List<Firm>> SearchFirmsAsync(string query, int from = 0, int size = 12,
        IProgress<string>? progress = null);
    Task<List<Firm>> FetchBulkFirmsAsync(IProgress<string>? progress = null,
        CancellationToken cancellationToken = default, int maxPagesPerLetter = 30);
}
