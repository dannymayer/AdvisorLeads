using AdvisorLeads.Abstractions;

namespace AdvisorLeads.Services.RefreshSteps;

internal sealed class SecBulkSubmissionsStep : IRefreshStep
{
    private readonly SecBulkSubmissionsService _secBulkSubmissions;

    public SecBulkSubmissionsStep(SecBulkSubmissionsService secBulkSubmissions)
    {
        _secBulkSubmissions = secBulkSubmissions;
    }

    public string Name => "SEC Bulk Submissions";
    public int OrderIndex => 70;

    public async Task ExecuteAsync(IProgress<string>? progress, CancellationToken token)
    {
        try
        {
            if (_secBulkSubmissions.IsCacheValid()) return;

            var enriched = await _secBulkSubmissions.SyncFirmMetadataAsync(progress, token);
            if (enriched > 0)
                progress?.Report($"✓ Enriched {enriched} firms with EDGAR SIC/metadata.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"Warning: {Name} step failed: {ex.Message}");
        }
    }
}
