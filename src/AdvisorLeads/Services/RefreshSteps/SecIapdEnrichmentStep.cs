using AdvisorLeads.Abstractions;

namespace AdvisorLeads.Services.RefreshSteps;

internal sealed class SecIapdEnrichmentStep : IRefreshStep
{
    private readonly SecIapdEnrichmentService _secIapd;

    public SecIapdEnrichmentStep(SecIapdEnrichmentService secIapd)
    {
        _secIapd = secIapd;
    }

    public string Name => "SEC IAPD Enrichment";
    public int OrderIndex => 20;

    public async Task ExecuteAsync(IProgress<string>? progress, CancellationToken token)
    {
        try
        {
            var iapdCount = await _secIapd.EnrichBatchAsync(progress, token, 100);
            if (iapdCount > 0)
                progress?.Report($"✓ Enriched {iapdCount} advisors via SEC IAPD.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"Warning: {Name} step failed: {ex.Message}");
        }
    }
}
