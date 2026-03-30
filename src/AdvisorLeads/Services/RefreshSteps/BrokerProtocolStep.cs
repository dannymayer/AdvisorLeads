using AdvisorLeads.Abstractions;

namespace AdvisorLeads.Services.RefreshSteps;

internal sealed class BrokerProtocolStep : IRefreshStep
{
    private readonly BrokerProtocolService _brokerProtocol;
    private readonly IAdvisorRepository _repo;

    public BrokerProtocolStep(BrokerProtocolService brokerProtocol, IAdvisorRepository repo)
    {
        _brokerProtocol = brokerProtocol;
        _repo = repo;
    }

    public string Name => "Broker Protocol";
    public int OrderIndex => 60;

    public async Task ExecuteAsync(IProgress<string>? progress, CancellationToken token)
    {
        try
        {
            var names = await _brokerProtocol.FetchMemberNamesAsync(token);
            if (names.Count < 5) return;

            var updated = _repo.UpdateBrokerProtocolStatus(names, DateTime.UtcNow);
            if (updated > 0)
                progress?.Report($"✓ Marked {updated} firms as Broker Protocol members.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"Warning: {Name} step failed: {ex.Message}");
        }
    }
}
