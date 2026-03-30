namespace AdvisorLeads.Models;

public static class FirmExportColumns
{
    private static string BoolStr(bool? value) =>
        value.HasValue ? (value.Value ? "Yes" : "No") : "";

    public static readonly List<ExportColumnDefinition<Firm>> All = new()
    {
        new("CrdNumber",                      "CRD #",                    f => f.CrdNumber),
        new("SECNumber",                      "SEC #",                    f => f.SECNumber),
        new("Name",                           "Firm Name",                f => f.Name),
        new("LegalName",                      "Legal Name",               f => f.LegalName),
        new("RecordType",                     "Record Type",              f => f.RecordType),
        new("BusinessType",                   "Business Type",            f => f.BusinessType),
        new("RegistrationStatus",             "Reg. Status",              f => f.RegistrationStatus),
        new("RegistrationDate",               "Reg. Date",                f => f.RegistrationDate?.ToString("yyyy-MM-dd")),
        new("Address",                        "Address",                  f => f.Address),
        new("City",                           "City",                     f => f.City),
        new("State",                          "State",                    f => f.State),
        new("ZipCode",                        "Zip",                      f => f.ZipCode),
        new("Country",                        "Country",                  f => f.Country),
        new("MailingAddress",                 "Mailing Address",          f => f.MailingAddress),
        new("StateOfOrganization",            "State of Org.",            f => f.StateOfOrganization),
        new("Phone",                          "Phone",                    f => f.Phone),
        new("FaxPhone",                       "Fax",                      f => f.FaxPhone),
        new("Website",                        "Website",                  f => f.Website),
        new("NumberOfAdvisors",               "# Advisors",               f => f.NumberOfAdvisors),
        new("NumberOfEmployees",              "# Employees",              f => f.NumberOfEmployees),
        new("NumberOfOffices",                "# Offices",                f => f.NumberOfOffices),
        new("RegulatoryAum",                  "Reg. AUM (Disc.)",         f => f.RegulatoryAum),
        new("RegulatoryAumNonDiscretionary",  "Reg. AUM (Non-Disc.)",     f => f.RegulatoryAumNonDiscretionary),
        new("AumDescription",                 "AUM Description",          f => f.AumDescription),
        new("NumClients",                     "# Clients",                f => f.NumClients),
        new("ClientsIndividuals",             "Clients (Individuals)",    f => f.ClientsIndividuals),
        new("ClientsHighNetWorth",            "Clients (HNW)",            f => f.ClientsHighNetWorth),
        new("BrokerProtocolMember",           "Broker Protocol",          f => f.BrokerProtocolMember ? "Yes" : "No"),
        new("IsRegisteredWithSec",            "SEC Registered",           f => f.IsRegisteredWithSec ? "Yes" : "No"),
        new("IsRegisteredWithFinra",          "FINRA Registered",         f => f.IsRegisteredWithFinra ? "Yes" : "No"),
        new("IsBrokerDealer",                 "Is BD",                    f => BoolStr(f.IsBrokerDealer)),
        new("IsInsuranceCompany",             "Is Insurance Co.",         f => BoolStr(f.IsInsuranceCompany)),
        new("CompensationFeeOnly",            "Fee-Only",                 f => BoolStr(f.CompensationFeeOnly)),
        new("CompensationCommission",         "Commission",               f => BoolStr(f.CompensationCommission)),
        new("CompensationHourly",             "Hourly",                   f => BoolStr(f.CompensationHourly)),
        new("CompensationPerformanceBased",   "Perf.-Based",              f => BoolStr(f.CompensationPerformanceBased)),
        new("HasCustody",                     "Has Custody",              f => BoolStr(f.HasCustody)),
        new("HasDiscretionaryAuthority",      "Discretionary",            f => BoolStr(f.HasDiscretionaryAuthority)),
        new("PrivateFundCount",               "Private Funds",            f => f.PrivateFundCount),
        new("AdvisoryActivities",             "Advisory Activities",      f => f.AdvisoryActivities),
        new("SECRegion",                      "SEC Region",               f => f.SECRegion),
        new("EdgarCik",                       "EDGAR CIK",                f => f.EdgarCik),
        new("Source",                         "Source",                   f => f.Source),
        new("LatestFilingDate",               "Latest Filing",            f => f.LatestFilingDate),
        new("UpdatedAt",                      "Last Updated",             f => f.UpdatedAt.ToString("yyyy-MM-dd")),
    };

    private static readonly List<string> _defaultKeys = new()
    {
        "Name", "CrdNumber", "SECNumber", "State", "City",
        "RecordType", "NumberOfAdvisors", "RegulatoryAum",
        "BrokerProtocolMember", "RegistrationStatus", "Source"
    };

    private static readonly List<string> _aumAnalysisKeys = new()
    {
        "Name", "CrdNumber", "State", "NumberOfAdvisors", "NumberOfEmployees",
        "RegulatoryAum", "RegulatoryAumNonDiscretionary", "NumClients",
        "ClientsIndividuals", "ClientsHighNetWorth", "PrivateFundCount", "LatestFilingDate"
    };

    public static List<ExportColumnDefinition<Firm>> GetPreset(string name)
    {
        var keys = name switch
        {
            "Default"      => _defaultKeys,
            "AUM Analysis" => _aumAnalysisKeys,
            _              => All.Select(c => c.Key).ToList()   // "Full" and any unknown
        };
        var lookup = All.ToDictionary(c => c.Key);
        return keys.Where(k => lookup.ContainsKey(k)).Select(k => lookup[k]).ToList();
    }

    public static readonly List<string> PresetNames = new()
    {
        "Default", "Full", "AUM Analysis"
    };
}
