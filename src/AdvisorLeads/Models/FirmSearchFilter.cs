namespace AdvisorLeads.Models;

public class FirmSearchFilter
{
    public string? NameQuery { get; set; }
    public string? State { get; set; }
    public string? RecordType { get; set; }           // "Investment Advisor" or "Broker-Dealer"
    public string? RegistrationStatus { get; set; }
    public int? MinAdvisors { get; set; }
    public string? SortBy { get; set; } = "Name";
    public bool SortDescending { get; set; } = false;
    public bool BrokerProtocolOnly { get; set; }
    public decimal? MinRegulatoryAum { get; set; }
}
