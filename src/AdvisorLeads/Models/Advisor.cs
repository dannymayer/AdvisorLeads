namespace AdvisorLeads.Models;

public class Advisor
{
    public int Id { get; set; }
    public string? CrdNumber { get; set; }
    public string? IapdNumber { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string? MiddleName { get; set; }
    public string? OtherNames { get; set; }  // comma-joined from basicInformation.otherNames[]
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Licenses { get; set; }
    public string? Qualifications { get; set; }
    public string? CurrentFirmName { get; set; }
    public string? CurrentFirmCrd { get; set; }
    public int? CurrentFirmId { get; set; }
    public string? RegistrationStatus { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public int? YearsOfExperience { get; set; }
    public bool HasDisclosures { get; set; }
    public int DisclosureCount { get; set; }
    public string? Source { get; set; }
    // "Investment Advisor Representative", "Registered Representative", or combined
    public string? RecordType { get; set; }
    public string? Suffix { get; set; }
    public string? IapdLink { get; set; }
    public string? RegAuthorities { get; set; }  // comma-joined state codes from CrntRgstn.regAuth
    public string? DisclosureFlags { get; set; } // comma-joined Y disclosure types e.g. "Criminal,Judgment"

    // Granular disclosure type flags (populated from FINRA detail / SEC IAPD)
    public bool HasCriminalDisclosure { get; set; }
    public bool HasRegulatoryDisclosure { get; set; }
    public bool HasCivilDisclosure { get; set; }
    public bool HasCustomerComplaintDisclosure { get; set; }
    public bool HasFinancialDisclosure { get; set; }
    public bool HasTerminationDisclosure { get; set; }

    // Separate BC vs IA disclosure counts from FINRA
    public int BcDisclosureCount { get; set; }
    public int IaDisclosureCount { get; set; }

    // Scope fields: overall BC and IA registration scope status from FINRA
    public string? BcScope { get; set; }
    public string? IaScope { get; set; }

    // Career metadata
    public DateTime? CareerStartDate { get; set; }  // Earliest employment start date
    public int? TotalFirmCount { get; set; }         // Distinct firms worked at

    // Direct profile links
    public string? BrokerCheckUrl { get; set; }

    public bool IsExcluded { get; set; }
    public string? ExclusionReason { get; set; }
    public bool IsFavorited { get; set; }
    public bool IsImportedToCrm { get; set; }
    public string? CrmId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<EmploymentHistory> EmploymentHistory { get; set; } = new();
    public List<Disclosure> Disclosures { get; set; } = new();
    public List<Qualification> QualificationList { get; set; } = new();
    public List<AdvisorRegistration> Registrations { get; set; } = new();

    public override string ToString() => FullName;
}
