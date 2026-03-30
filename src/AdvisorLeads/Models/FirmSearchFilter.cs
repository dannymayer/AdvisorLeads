namespace AdvisorLeads.Models;

public class FirmSearchFilter
{
    public string? NameQuery { get; set; }
    public string? State { get; set; }
    public string? RecordType { get; set; }           // "Investment Adviser" or "Broker-Dealer"
    public string? RegistrationStatus { get; set; }
    public int? MinAdvisors { get; set; }
    public string? SortBy { get; set; } = "Name";
    public bool SortDescending { get; set; } = false;
    public bool BrokerProtocolOnly { get; set; }
    public decimal? MinRegulatoryAum { get; set; }
    public bool? HasCustody { get; set; }
    public bool? HasDiscretionaryAuthority { get; set; }
    /// <summary>"FeeOnly", "Commission", or "Both".</summary>
    public string? CompensationType { get; set; }
    public int? MinPrivateFunds { get; set; }
    /// <summary>Minimum M&A target score (0-100) to filter by.</summary>
    public int? MinMaScore { get; set; }
    /// <summary>M&A score grade filter: "A", "B", "C", "D", "F".</summary>
    public string? MaScoreGrade { get; set; }
    public int PageSize { get; set; } = 5000;
    public int PageNumber { get; set; } = 1;

    // ── Enrichment filters ──────────────────────────────────────────────
    public string? InvestmentStrategy { get; set; }
    public string? OwnershipStructure { get; set; }
    public bool? HasActiveSanction { get; set; }
    public bool? HasSecEnforcementAction { get; set; }
    public string? RegistrationLevel { get; set; }
    public bool? CryptoExposure { get; set; }
    public bool? WrapFeePrograms { get; set; }
}
