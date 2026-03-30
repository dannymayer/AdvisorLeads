using AdvisorLeads.Abstractions;

namespace AdvisorLeads.Services.RefreshSteps;

internal sealed class FormAdvHistoricalStep : IRefreshStep
{
    private readonly FormAdvHistoricalService _formAdvHistorical;
    private readonly Func<string, string?> _loadSetting;
    private readonly Action<string, string> _saveSetting;

    public FormAdvHistoricalStep(
        FormAdvHistoricalService formAdvHistorical,
        Func<string, string?> loadSetting,
        Action<string, string> saveSetting)
    {
        _formAdvHistorical = formAdvHistorical;
        _loadSetting = loadSetting;
        _saveSetting = saveSetting;
    }

    public string Name => "Form ADV Historical";
    public int OrderIndex => 50;

    public async Task ExecuteAsync(IProgress<string>? progress, CancellationToken token)
    {
        try
        {
            var lastImport = _loadSetting("FormAdvHistoricalLastImport");
            if (lastImport != null && DateTime.TryParse(lastImport, out var last))
            {
                // Only re-import quarterly
                if ((DateTime.UtcNow - last).TotalDays < 90) return;
            }

            progress?.Report("Discovering available Form ADV historical files...");
            var urls = await _formAdvHistorical.DiscoverAvailableFilesAsync(token);
            if (urls.Count == 0)
            {
                progress?.Report("No Form ADV historical files found.");
                return;
            }

            var url = urls.First();
            progress?.Report($"Importing Form ADV historical data from {Path.GetFileName(new Uri(url).LocalPath)}...");
            var (filings, owners) = await _formAdvHistorical.ImportHistoricalDataAsync(url, progress, token);

            _saveSetting("FormAdvHistoricalLastImport", DateTime.UtcNow.ToString("O"));

            if (filings > 0 || owners > 0)
                progress?.Report($"✓ Imported {filings} filings and {owners} owner records.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"Warning: {Name} step failed: {ex.Message}");
        }
    }
}
