using AdvisorLeads.Abstractions;

namespace AdvisorLeads.Services.RefreshSteps;

internal sealed class DisclosureScoringStep : IRefreshStep
{
    private readonly DisclosureScoringService _disclosureScorer;

    public DisclosureScoringStep(DisclosureScoringService disclosureScorer)
    {
        _disclosureScorer = disclosureScorer;
    }

    public string Name => "Disclosure Scoring";
    public int OrderIndex => 80;

    public async Task ExecuteAsync(IProgress<string>? progress, CancellationToken token)
    {
        try
        {
            await _disclosureScorer.RefreshAllScoresAsync(progress, token);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"Warning: {Name} step failed: {ex.Message}");
        }
    }
}
