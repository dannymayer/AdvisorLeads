using AdvisorLeads.Abstractions;

namespace AdvisorLeads.Services.RefreshSteps;

internal sealed class SecEnforcementStep : IRefreshStep
{
    private readonly SecEnforcementService _secEnforcement;

    public SecEnforcementStep(SecEnforcementService secEnforcement)
    {
        _secEnforcement = secEnforcement;
    }

    public string Name => "SEC Enforcement Enrichment";
    public int OrderIndex => 210;

    public async Task ExecuteAsync(IProgress<string>? progress, CancellationToken token)
    {
        try
        {
            await _secEnforcement.EnrichBatchAsync(progress: progress, ct: token);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"Warning: {Name} step failed: {ex.Message}");
        }
    }
}
