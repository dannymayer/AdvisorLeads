using AdvisorLeads.Abstractions;

namespace AdvisorLeads.Services.RefreshSteps;

internal sealed class CourtListenerStep : IRefreshStep
{
    private readonly CourtListenerService _courtListener;

    public CourtListenerStep(CourtListenerService courtListener)
    {
        _courtListener = courtListener;
    }

    public string Name => "CourtListener Enrichment";
    public int OrderIndex => 220;

    public async Task ExecuteAsync(IProgress<string>? progress, CancellationToken token)
    {
        try
        {
            await _courtListener.EnrichBatchAsync(progress: progress, ct: token);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"Warning: {Name} step failed: {ex.Message}");
        }
    }
}
