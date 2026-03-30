using AdvisorLeads.Abstractions;

namespace AdvisorLeads.Services.RefreshSteps;

internal sealed class EdgarSearchStep : IRefreshStep
{
    private readonly EdgarSearchService _edgarSearch;

    public EdgarSearchStep(EdgarSearchService edgarSearch)
    {
        _edgarSearch = edgarSearch;
    }

    public string Name => "EDGAR M&A Search";
    public int OrderIndex => 40;

    public async Task ExecuteAsync(IProgress<string>? progress, CancellationToken token)
    {
        try
        {
            var searchCount = await _edgarSearch.RunMaSearchScanAsync(progress, token);
            if (searchCount > 0)
                progress?.Report($"✓ Found {searchCount} EDGAR M&A keyword matches.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"Warning: {Name} step failed: {ex.Message}");
        }
    }
}
