using AdvisorLeads.Abstractions;

namespace AdvisorLeads.Services.RefreshSteps;

internal sealed class FormAdvDeepEnrichmentStep : IRefreshStep
{
    private readonly FormAdvDeepEnrichmentService _formAdvDeep;

    public FormAdvDeepEnrichmentStep(FormAdvDeepEnrichmentService formAdvDeep)
    {
        _formAdvDeep = formAdvDeep;
    }

    public string Name => "Form ADV Deep Enrichment";
    public int OrderIndex => 230;

    public async Task ExecuteAsync(IProgress<string>? progress, CancellationToken token)
    {
        try
        {
            await _formAdvDeep.EnrichBatchAsync(progress: progress, ct: token);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"Warning: {Name} step failed: {ex.Message}");
        }
    }
}
