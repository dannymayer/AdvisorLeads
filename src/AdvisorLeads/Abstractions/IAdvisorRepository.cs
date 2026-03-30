using AdvisorLeads.Models;
using AdvisorLeads.Services;

namespace AdvisorLeads.Abstractions;

public interface IAdvisorRepository
{
    List<Advisor> GetAdvisors(SearchFilter filter);
    (List<Advisor> Advisors, int TotalCount) GetAdvisorsWithCount(SearchFilter filter);
    Advisor? GetAdvisorById(int id);
    Advisor? GetAdvisorByCrd(string crd);
    int UpsertAdvisor(Advisor advisor, out bool wasNew, WatchListMonitorService? watchMonitor = null);
    int UpsertAdvisor(Advisor advisor);
    void SetAdvisorExcluded(int id, bool excluded, string? reason = null);
    void SetAdvisorImported(int id, string? crmId);
    void SetAdvisorFavorited(int id, bool favorited);
    void SetAdvisorWatched(int id, bool watched);
    List<Advisor> GetWatchedAdvisors();
    List<string> GetWatchedAdvisorCrds();
    int GetWatchedCount();
    void SetFirmWatched(int id, bool watched);
    List<Firm> GetWatchedFirms();
    HashSet<string> GetBrokerProtocolMemberCrds();
    List<string> GetCrdsNeedingEnrichment(int limit);
    List<string> GetCrdsNeedingIapdEnrichment(int limit = 200);
    List<Firm> GetFirms(FirmSearchFilter? filter = null);
    (List<Firm> Firms, int TotalCount) GetFirmsWithCount(FirmSearchFilter? filter = null);
    int UpsertFirm(Firm firm);
    void UpsertFirmBatch(IEnumerable<Firm> firms, IProgress<string>? progress = null);
    List<string> GetDistinctStates();
    List<string> GetDistinctFirmStates();
    List<string> GetDistinctFirmNames();
    int GetAdvisorCount(SearchFilter filter);
    int UpdateBrokerProtocolStatus(List<string> memberNames, DateTime fetchedAt);
    (int Total, int WithDisclosures, double DisclosurePercent, int Finra, int Sec, int Favorites, int InCrm) GetAdvisorStats();
    (int Total, int InvestmentAdvisor, int BrokerDealer) GetFirmStats();
    (int TotalAdvisors, int TotalFirms, int Favorites, int InCrm,
        int WithDisclosures, int FavsNotInCrm, int UpdatedToday) GetDashboardStats();
    List<Firm> GetTopMaFirms(int limit = 10);
    List<Advisor> GetAdvisorsByFirmCrd(string firmCrd, int limit = 100);
    Firm? GetFirmByCrd(string crd);
    int UpdateAdvisorEmails(IEnumerable<(int AdvisorId, string Email)> updates);
    List<EmploymentChangeEvent> GetRecentEmploymentChanges(int days = 90);
    List<Advisor> GetAdvisorsForSanctionEnrichment(int limit);
    List<Firm> GetFirmsForSanctionEnrichment(int limit);
    void UpsertAdvisorSanctions(string crd, List<FinraSanction> sanctions);
    void UpdateAdvisorSanctionFlags(string crd, bool hasActive, decimal? maxFine,
        string? sanctionType, DateTime enrichedAt);
    List<(string Crd, string First, string Last)> GetAdvisorsForSecEnforcementCheck(int limit);
    void UpsertSecEnforcementActions(string crd, List<SecEnforcementAction> actions);
    void UpdateAdvisorSecEnforcementFlags(string crd, bool hasAction, DateTime enrichedAt);
    List<(string Crd, int Id, string First, string Last, string? State)>
        GetAdvisorsForCourtRecordCheck(int limit);
    void UpsertAdvisorCourtRecords(string crd, int? advisorId, List<AdvisorCourtRecord> records);
    void UpdateAdvisorCourtRecordFlags(string crd, bool hasFlag, string? url,
        DateTime? date, string? summary, DateTime enrichedAt);
    List<AdvisorCourtRecord> GetCourtRecordsByCrd(string crd);
    void ClassifyRegistrationLevels();
    void ResolveAdvisorFirmLinks();
    int UpdateFirmAdvisorCounts();
    List<Firm> GetFirmsForIntelligence(string? state = null, int limit = 200);
    decimal? GetFirmAum1YearAgo(string firmCrd);
    void UpdateFirmAdvisorCountChange(int firmId, int? delta);
    List<Advisor> GetAdvisorsWithDisclosures(int limit = 1000);
    void UpdateDisclosureSeverityScore(int advisorId, int score);
    void UpdateFirmEnrichmentData(Firm firm);
    List<Advisor> GetAdvisorsForGeography(bool activeOnly = true);
    List<Firm> GetFirmsForGeography(bool activeOnly = true);
    List<Advisor> GetActiveAdvisors(int limit = 5000);
    void UpdateMobilityScore(int advisorId, int score, DateTime updatedAt);
}
