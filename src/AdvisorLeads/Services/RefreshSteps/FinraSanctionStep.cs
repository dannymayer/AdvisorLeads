using AdvisorLeads.Abstractions;

namespace AdvisorLeads.Services.RefreshSteps;

internal sealed class FinraSanctionStep : IRefreshStep
{
    private readonly FinraSanctionService _finraSanction;

    public FinraSanctionStep(FinraSanctionService finraSanction)
    {
        _finraSanction = finraSanction;
    }

    public string Name => "FINRA Sanction Enrichment";
    public int OrderIndex => 200;

    public async Task ExecuteAsync(IProgress<string>? progress, CancellationToken token)
    {
        try
        {
            await _finraSanction.EnrichBatchAsync(progress: progress, ct: token);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"Warning: {Name} step failed: {ex.Message}");
        }
    }
}
