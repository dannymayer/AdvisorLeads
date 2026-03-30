namespace AdvisorLeads.Models;

/// <summary>
/// Filter parameters shared across all reporting queries.
/// </summary>
public class ReportFilter
{
    public string? State { get; set; }
    public string? RecordType { get; set; }       // "Investment Advisor Representative" / "Registered Representative"
    public string? FirmCrd { get; set; }
    public bool ActiveOnly { get; set; } = true;
    public bool ExcludeExcluded { get; set; } = true;
    public int? MinYearsExperience { get; set; }
    public int? MaxYearsExperience { get; set; }
    public decimal? MinRegulatoryAum { get; set; }   // firm AUM filter
    public int? MinAdvisors { get; set; }             // firm advisor count filter
    public bool BrokerProtocolOnly { get; set; }
    public bool NoDisclosuresOnly { get; set; }
    public bool FavoritedOnly { get; set; }
    public int? MinTotalFirmCount { get; set; }
    public int? MinStateRegistrationCount { get; set; }
    public int? MinHnwClientPct { get; set; }
    public string? CompensationType { get; set; }    // "FeeOnly" / "Commission" / "Both"
    public DateTime? CareerStartedAfter { get; set; }
    public int PageSize { get; set; } = 1000;
}

// ── Report 1 — Flight Risk Scorecard ───────────────────────────────────────
public record FlightRiskRow(
    int Id, string FullName, string? CrdNumber, string? State, string? City,
    string? CurrentFirmName, int? TenureAtCurrentFirmYears, int? YearsOfExperience,
    int? TotalFirmCount, decimal? FirmChangeRate, decimal? FirmRegulatoryAum,
    decimal? AumPerAdvisor, decimal? FirmAumChange1YrPct, bool BrokerProtocolMember,
    bool HasDisclosures, string? Email, int FlightRiskScore);

// ── Report 2 — High-Value Target Advisor List ───────────────────────────────
public record HighValueTargetRow(
    int Id, string FullName, string? CrdNumber, string? State, string? City,
    string? CurrentFirmName, int? YearsOfExperience, bool HasDisclosures, string? Email,
    string? Qualifications, decimal? FirmRegulatoryAum, int? NumberOfAdvisors,
    bool BrokerProtocolMember, decimal? AumPerAdvisor, int TargetScore);

// ── Report 3 — Advisor Tenure Distribution ─────────────────────────────────
public record TenureDistributionSummaryRow(
    string TenureBucket, int AdvisorCount, decimal? AvgAumPerAdvisor, decimal? DisclosureRate);

public record TenureDistributionDetailRow(
    int Id, string FullName, string? CrdNumber, string? State, string? CurrentFirmName,
    int? TenureAtCurrentFirmYears, int? YearsOfExperience, bool HasDisclosures,
    decimal? AumPerAdvisor);

// ── Report 4 — Serial Mover Profile ────────────────────────────────────────
public record SerialMoverRow(
    int Id, string FullName, string? CrdNumber, string? State, string? CurrentFirmName,
    int? TotalFirmCount, int? YearsOfExperience, decimal? FirmChangeRate,
    int? TenureAtCurrentFirmYears, decimal? FirmRegulatoryAum, decimal? AumPerAdvisor,
    bool DueForMove);

// ── Report 5 — New Market Entrants ─────────────────────────────────────────
public record NewMarketEntrantRow(
    int Id, string FullName, string? CrdNumber, string? State, string? CurrentFirmName,
    DateTime? CareerStartDate, string? CareerQuarter, int? YearsOfExperience,
    string? Email, bool HasDisclosures, decimal? FirmRegulatoryAum, int? NumberOfAdvisors);

// ── Report 6 — Firm Headcount Trend ────────────────────────────────────────
public record FirmHeadcountTrendRow(
    int Id, string Name, string CrdNumber, string? State, int? NumberOfAdvisors,
    int? PriorAdvisorCount, int? AdvisorCountChange, decimal? AdvisorCountChangePct,
    decimal? RegulatoryAum, string? LatestFilingDate, int PipelineCount);

// ── Report 7 — Broker Protocol Firm Directory ──────────────────────────────
public record BrokerProtocolFirmRow(
    int Id, string Name, string CrdNumber, string? State, string? City, string? Phone,
    string? Website, int? NumberOfAdvisors, decimal? RegulatoryAum, decimal? AumPerAdvisor,
    decimal? HnwClientPct, string CompensationModel, int FavoritedAdvisorCount,
    int TotalAdvisorCount);

// ── Report 8 — Firm AUM Trajectory ─────────────────────────────────────────
public record FirmAumTrajectoryRow(
    int Id, string Name, string CrdNumber, string? State, decimal? CurrentAum,
    int? NumberOfAdvisors, decimal? Aum1YrAgo, decimal? Aum3YrAgo, decimal? Aum5YrAgo,
    decimal? AumChange1YrPct, decimal? AumChange3YrPct, decimal? AumChange5YrPct);

// ── Report 9 — Competitive Landscape Dashboard ─────────────────────────────
public record CompetitiveLandscapeRow(
    int Id, string Name, string CrdNumber, string? State, int? NumberOfAdvisors,
    decimal? RegulatoryAum, decimal? AumPerAdvisor,
    decimal? AdvisorMarketSharePct, decimal? AumMarketSharePct);

// ── Report 10 — Credential & License Frequency ─────────────────────────────
public record CredentialFrequencyRow(
    string Name, string? Code, int AdvisorCount, decimal? PctOfActiveMarket);

// ── Report 11 — Geographic Advisor Density ─────────────────────────────────
public record GeographicDensityRow(
    string? State, int AdvisorCount, int ActiveAdvisorCount,
    decimal? AvgYearsExperience, decimal? DisclosureRate, int FavoritedCount);

// ── Report 12 — AUM Concentration by Geography ─────────────────────────────
public record AumConcentrationByGeoRow(
    string? State, int AdvisorCount, decimal? AvgAumPerAdvisor, int AdvisorsAbove500M);

// ── Report 13 — Disclosure Risk Profile Dashboard ──────────────────────────
public record DisclosureProfileSummaryRow(
    int TotalAdvisors, int AdvisorsWithDisclosures, decimal? DisclosureRate,
    int CriminalCount, int RegulatoryCount, int CivilCount,
    int CustomerComplaintCount, int FinancialCount, int TerminationCount);

public record DisclosureProfileDetailRow(
    int Id, string FullName, string? CrdNumber, string? State, string? CurrentFirmName,
    int DisclosureCount, bool HasCriminalDisclosure, bool HasRegulatoryDisclosure,
    bool HasCivilDisclosure, bool HasCustomerComplaintDisclosure,
    bool HasFinancialDisclosure, bool HasTerminationDisclosure,
    DateTime? MostRecentDisclosureDate, decimal? AumPerAdvisor);

// ── Report 14 — Clean Record Premium Advisor List ──────────────────────────
public record CleanRecordAdvisorRow(
    int Id, string FullName, string? CrdNumber, string? State, string? CurrentFirmName,
    int? YearsOfExperience, string? Email, decimal? AumPerAdvisor,
    bool BrokerProtocolMember, int? TenureAtCurrentFirmYears);

// ── Report 15 — Recruiting Pipeline Funnel ─────────────────────────────────
public record PipelineFunnelSummaryRow(
    int TotalActive, int Favorited, int FavoritedWithEmail, int ImportedToCrm,
    decimal? ConversionRate, int EnrichmentGap);

public record PipelineFunnelDetailRow(
    int Id, string FullName, string? CrdNumber, string? State, string? CurrentFirmName,
    string? Email, int? YearsOfExperience, bool HasDisclosures, decimal? AumPerAdvisor);

// ── Report 16 — Contact Coverage Gap ───────────────────────────────────────
public record ContactCoverageGapRow(
    int Id, string FullName, string? CrdNumber, string? State, string? CurrentFirmName,
    int? YearsOfExperience, string? FirmPhone, string? Website, decimal? AumPerAdvisor);

// ── Report 17 — Firm Stability Signal ──────────────────────────────────────
public record FirmStabilityRow(
    int Id, string Name, string CrdNumber, string? State, decimal? RegulatoryAum,
    int? NumberOfAdvisors, int? PriorAdvisorCount, string? LatestFilingDate,
    int? DaysSinceLastFiling, decimal? AumChange1YrPct, int? HeadcountChange,
    decimal? AvgAdvisorDisclosureCount, int InstabilityScore);

// ── Report 18 — Compensation Model Analysis ────────────────────────────────
public record CompensationModelRow(
    string CompensationModel, int FirmCount, int? AdvisorCount,
    decimal? AvgRegulatoryAum, decimal? AvgAumPerAdvisor, decimal? AvgHnwClientPct);

// ── Report 19 — High-Net-Worth Focus Firm Finder ───────────────────────────
public record HnwFirmRow(
    int Id, string Name, string CrdNumber, string? State, int? NumberOfAdvisors,
    decimal? RegulatoryAum, decimal? AumPerAdvisor, decimal? HnwClientPct,
    bool? CompensationFeeOnly, bool? BrokerProtocolMember, int? PrivateFundCount,
    int UpmarketScore);

// ── Report 20 — Advisor Multi-State Registration Map ───────────────────────
public record MultiStateRegistrationRow(
    int Id, string FullName, string? CrdNumber, string? State, string? CurrentFirmName,
    int? YearsOfExperience, int StateRegistrationCount, string? RegisteredStates,
    string PortabilityTier);
