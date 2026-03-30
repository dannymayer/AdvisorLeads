using AdvisorLeads.Abstractions;

namespace AdvisorLeads.Services.RefreshSteps;

internal sealed class EdgarSubmissionsStep : IRefreshStep
{
    private readonly EdgarSubmissionsService _edgarSubmissions;

    public EdgarSubmissionsStep(EdgarSubmissionsService edgarSubmissions)
    {
        _edgarSubmissions = edgarSubmissions;
    }

    public string Name => "EDGAR Submissions";
    public int OrderIndex => 30;

    public async Task ExecuteAsync(IProgress<string>? progress, CancellationToken token)
    {
        try
        {
            var filingCount = await _edgarSubmissions.FetchFilingsBatchAsync(100, progress, token);
            if (filingCount > 0)
                progress?.Report($"✓ Fetched {filingCount} new EDGAR filings.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"Warning: {Name} step failed: {ex.Message}");
        }
    }
}
