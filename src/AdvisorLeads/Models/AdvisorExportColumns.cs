namespace AdvisorLeads.Models;

public static class AdvisorExportColumns
{
    public static readonly List<ExportColumnDefinition<Advisor>> All = new()
    {
        new("CrdNumber",             "CRD #",              a => a.CrdNumber),
        new("IapdNumber",            "IAPD #",             a => a.IapdNumber),
        new("FirstName",             "First Name",         a => a.FirstName),
        new("MiddleName",            "Middle Name",        a => a.MiddleName),
        new("LastName",              "Last Name",          a => a.LastName),
        new("Suffix",                "Suffix",             a => a.Suffix),
        new("OtherNames",            "Other Names",        a => a.OtherNames),
        new("FullName",              "Full Name",          a => a.FullName),
        new("Title",                 "Title",              a => a.Title),
        new("RecordType",            "Record Type",        a => a.RecordType),
        new("RegistrationStatus",    "Reg. Status",        a => a.RegistrationStatus),
        new("RegistrationDate",      "Reg. Date",          a => a.RegistrationDate?.ToString("yyyy-MM-dd")),
        new("CurrentFirmName",       "Current Firm",       a => a.CurrentFirmName),
        new("CurrentFirmCrd",        "Firm CRD",           a => a.CurrentFirmCrd),
        new("CurrentFirmStartDate",  "At Firm Since",      a => a.CurrentFirmStartDate?.ToString("yyyy-MM-dd")),
        new("City",                  "City",               a => a.City),
        new("State",                 "State",              a => a.State),
        new("ZipCode",               "Zip",                a => a.ZipCode),
        new("Email",                 "Email",              a => a.Email),
        new("Phone",                 "Phone",              a => a.Phone),
        new("YearsOfExperience",     "Yrs Exp",            a => a.YearsOfExperience),
        new("CareerStartDate",       "Career Start",       a => a.CareerStartDate?.ToString("yyyy-MM-dd")),
        new("TotalFirmCount",        "Total Firms",        a => a.TotalFirmCount),
        new("HasDisclosures",        "Has Disclosures",    a => a.HasDisclosures ? "Yes" : "No"),
        new("DisclosureCount",       "Disclosures",        a => a.DisclosureCount),
        new("BcDisclosureCount",     "BC Disclosures",     a => a.BcDisclosureCount),
        new("IaDisclosureCount",     "IA Disclosures",     a => a.IaDisclosureCount),
        new("DisclosureFlags",       "Disclosure Types",   a => a.DisclosureFlags),
        new("BcScope",               "BC Scope",           a => a.BcScope),
        new("IaScope",               "IA Scope",           a => a.IaScope),
        new("Licenses",              "Licenses",           a => a.Licenses),
        new("Qualifications",        "Qualifications",     a => a.Qualifications),
        new("RegAuthorities",        "Reg. Authorities",   a => a.RegAuthorities),
        new("Source",                "Source",             a => a.Source),
        new("IsFavorited",           "Favorited",          a => a.IsFavorited ? "Yes" : "No"),
        new("IsImportedToCrm",       "In CRM",             a => a.IsImportedToCrm ? "Yes" : "No"),
        new("CrmId",                 "CRM ID",             a => a.CrmId),
        new("BrokerCheckUrl",        "BrokerCheck URL",    a => a.BrokerCheckUrl),
        new("UpdatedAt",             "Last Updated",       a => a.UpdatedAt.ToString("yyyy-MM-dd")),
        new("CreatedAt",             "Added",              a => a.CreatedAt.ToString("yyyy-MM-dd")),
    };

    private static readonly List<string> _defaultKeys = new()
    {
        "FullName", "CurrentFirmName", "State", "City", "RecordType",
        "YearsOfExperience", "HasDisclosures", "DisclosureCount",
        "RegistrationStatus", "Source", "UpdatedAt"
    };

    private static readonly List<string> _crmImportKeys = new()
    {
        "FirstName", "LastName", "Email", "Phone", "CurrentFirmName",
        "City", "State", "ZipCode", "CrdNumber", "IsImportedToCrm", "CrmId"
    };

    private static readonly List<string> _disclosureReviewKeys = new()
    {
        "FullName", "CrdNumber", "CurrentFirmName", "State",
        "HasDisclosures", "DisclosureCount", "DisclosureFlags",
        "BcDisclosureCount", "IaDisclosureCount", "BcScope",
        "BrokerCheckUrl", "UpdatedAt"
    };

    public static List<ExportColumnDefinition<Advisor>> GetPreset(string name)
    {
        var keys = name switch
        {
            "Default"          => _defaultKeys,
            "CRM Import"       => _crmImportKeys,
            "Disclosure Review" => _disclosureReviewKeys,
            _                  => All.Select(c => c.Key).ToList()   // "Full" and any unknown
        };
        var lookup = All.ToDictionary(c => c.Key);
        return keys.Where(k => lookup.ContainsKey(k)).Select(k => lookup[k]).ToList();
    }

    public static readonly List<string> PresetNames = new()
    {
        "Default", "CRM Import", "Full", "Disclosure Review"
    };
}
