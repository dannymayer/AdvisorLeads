using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Async wrapper around <see cref="ReportingRepository"/> that offloads all
/// synchronous SQLite work onto the thread pool so the WinForms UI thread stays responsive.
/// </summary>
public class ReportingService
{
    private readonly ReportingRepository _repo;

    public ReportingService(string dbPath)
    {
        _repo = new ReportingRepository(dbPath);
    }

    // ── Report 1 — Flight Risk Scorecard ─────────────────────────────────
    public Task<List<FlightRiskRow>> GetFlightRiskAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetFlightRisk(filter), ct);

    // ── Report 2 — High-Value Target Advisor List ─────────────────────────
    public Task<List<HighValueTargetRow>> GetHighValueTargetsAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetHighValueTargets(filter), ct);

    // ── Report 3 — Advisor Tenure Distribution ────────────────────────────
    public Task<List<TenureDistributionSummaryRow>> GetTenureDistributionSummaryAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetTenureDistributionSummary(filter), ct);

    public Task<List<TenureDistributionDetailRow>> GetTenureDistributionDetailAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetTenureDistributionDetail(filter), ct);

    // ── Report 4 — Serial Mover Profile ──────────────────────────────────
    public Task<List<SerialMoverRow>> GetSerialMoversAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetSerialMovers(filter), ct);

    // ── Report 5 — New Market Entrants ────────────────────────────────────
    public Task<List<NewMarketEntrantRow>> GetNewMarketEntrantsAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetNewMarketEntrants(filter), ct);

    // ── Report 6 — Firm Headcount Trend ──────────────────────────────────
    public Task<List<FirmHeadcountTrendRow>> GetFirmHeadcountTrendAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetFirmHeadcountTrend(filter), ct);

    // ── Report 7 — Broker Protocol Firm Directory ─────────────────────────
    public Task<List<BrokerProtocolFirmRow>> GetBrokerProtocolDirectoryAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetBrokerProtocolDirectory(filter), ct);

    // ── Report 8 — Firm AUM Trajectory ───────────────────────────────────
    public Task<List<FirmAumTrajectoryRow>> GetFirmAumTrajectoryAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetFirmAumTrajectory(filter), ct);

    // ── Report 9 — Competitive Landscape Dashboard ────────────────────────
    public Task<List<CompetitiveLandscapeRow>> GetCompetitiveLandscapeAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetCompetitiveLandscape(filter), ct);

    // ── Report 10 — Credential & License Frequency ───────────────────────
    public Task<List<CredentialFrequencyRow>> GetCredentialFrequencyAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetCredentialFrequency(filter), ct);

    // ── Report 11 — Geographic Advisor Density ────────────────────────────
    public Task<List<GeographicDensityRow>> GetGeographicDensityAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetGeographicDensity(filter), ct);

    // ── Report 12 — AUM Concentration by Geography ────────────────────────
    public Task<List<AumConcentrationByGeoRow>> GetAumConcentrationByGeoAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetAumConcentrationByGeo(filter), ct);

    // ── Report 13 — Disclosure Risk Profile Dashboard ─────────────────────
    public Task<DisclosureProfileSummaryRow?> GetDisclosureProfileSummaryAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetDisclosureProfileSummary(filter), ct);

    public Task<List<DisclosureProfileDetailRow>> GetDisclosureProfileDetailAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetDisclosureProfileDetail(filter), ct);

    // ── Report 14 — Clean Record Premium Advisor List ─────────────────────
    public Task<List<CleanRecordAdvisorRow>> GetCleanRecordAdvisorsAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetCleanRecordAdvisors(filter), ct);

    // ── Report 15 — Recruiting Pipeline Funnel ────────────────────────────
    public Task<PipelineFunnelSummaryRow?> GetPipelineFunnelSummaryAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetPipelineFunnelSummary(filter), ct);

    public Task<List<PipelineFunnelDetailRow>> GetPipelineFunnelDetailAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetPipelineFunnelDetail(filter), ct);

    // ── Report 16 — Contact Coverage Gap ─────────────────────────────────
    public Task<List<ContactCoverageGapRow>> GetContactCoverageGapAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetContactCoverageGap(filter), ct);

    // ── Report 17 — Firm Stability Signal ────────────────────────────────
    public Task<List<FirmStabilityRow>> GetFirmStabilitySignalAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetFirmStabilitySignal(filter), ct);

    // ── Report 18 — Compensation Model Analysis ───────────────────────────
    public Task<List<CompensationModelRow>> GetCompensationAnalysisAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetCompensationAnalysis(filter), ct);

    // ── Report 19 — High-Net-Worth Focus Firm Finder ──────────────────────
    public Task<List<HnwFirmRow>> GetHnwFocusFirmsAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetHnwFocusFirms(filter), ct);

    // ── Report 20 — Advisor Multi-State Registration Map ─────────────────
    public Task<List<MultiStateRegistrationRow>> GetMultiStateRegistrationMapAsync(ReportFilter filter, CancellationToken ct = default)
        => Task.Run(() => _repo.GetMultiStateRegistrationMap(filter), ct);
}
