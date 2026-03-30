using AdvisorLeads.Models;

namespace AdvisorLeads.Abstractions;

public interface ICrmProvider
{
    Task<string?> ImportAdvisorAsync(Advisor advisor, IProgress<string>? progress = null);
    Task<Dictionary<int, string?>> ImportAdvisorsAsync(
        IEnumerable<Advisor> advisors,
        IProgress<string>? progress = null);
    Task<bool> ValidateTokenAsync();
}
