using AdvisorLeads.Abstractions;

namespace AdvisorLeads.Services.RefreshSteps;

internal sealed class MobilityScoreStep : IRefreshStep
{
    private readonly MobilityScoreService _mobilityScorer;

    public MobilityScoreStep(MobilityScoreService mobilityScorer)
    {
        _mobilityScorer = mobilityScorer;
    }

    public string Name => "Mobility Scoring";
    public int OrderIndex => 90;

    public async Task ExecuteAsync(IProgress<string>? progress, CancellationToken token)
    {
        try
        {
            await _mobilityScorer.RefreshAllScoresAsync(progress, token);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"Warning: {Name} step failed: {ex.Message}");
        }
    }
}
