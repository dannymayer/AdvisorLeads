using AdvisorLeads.Abstractions;

namespace AdvisorLeads.Services.RefreshSteps;

internal sealed class CompetitiveIntelligenceStep : IRefreshStep
{
    private readonly CompetitiveIntelligenceService _competitiveIntel;

    public CompetitiveIntelligenceStep(CompetitiveIntelligenceService competitiveIntel)
    {
        _competitiveIntel = competitiveIntel;
    }

    public string Name => "Competitive Intelligence";
    public int OrderIndex => 100;

    public async Task ExecuteAsync(IProgress<string>? progress, CancellationToken token)
    {
        try
        {
            await _competitiveIntel.RefreshHeadcountDeltasAsync(progress);
        }
        catch (Exception ex)
        {
            progress?.Report($"Warning: {Name} step failed: {ex.Message}");
        }
    }
}
