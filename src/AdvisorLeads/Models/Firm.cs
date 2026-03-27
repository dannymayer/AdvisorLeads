namespace AdvisorLeads.Models;

public class Firm
{
    public int Id { get; set; }
    public string CrdNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? BusinessType { get; set; }
    public bool IsRegisteredWithSec { get; set; }
    public bool IsRegisteredWithFinra { get; set; }
    public int? NumberOfAdvisors { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public string? Source { get; set; }
    // "Investment Advisor" (SEC RIA) or "Broker-Dealer" (FINRA)
    public string? RecordType { get; set; }
    public string? SECNumber { get; set; }        // SECNb
    public string? SECRegion { get; set; }        // SECRgnCD
    public string? LegalName { get; set; }        // LegalNm
    public string? FaxPhone { get; set; }         // FaxNb
    public string? MailingAddress { get; set; }   // composite of MailingAddr attrs
    public string? RegistrationStatus { get; set; } // Rgstn.St
    public string? AumDescription { get; set; }  // Item1.Q1ODesc
    public string? StateOfOrganization { get; set; } // Item3C.StateCD
    public string? Country { get; set; }             // Main Office Country
    public int? NumberOfEmployees { get; set; }      // 5A total employees
    public string? LatestFilingDate { get; set; }    // Latest ADV Filing Date
    /// <summary>Discretionary RAUM in USD (parsed from CSV column 5F(2)(a)).</summary>
    public decimal? RegulatoryAum { get; set; }
    /// <summary>Non-discretionary RAUM in USD (parsed from CSV column 5F(2)(b)).</summary>
    public decimal? RegulatoryAumNonDiscretionary { get; set; }
    /// <summary>Approximate number of clients (parsed from CSV column 5D or 5E.1).</summary>
    public int? NumClients { get; set; }
    /// <summary>True if this firm is a member of the Broker Protocol.</summary>
    public bool BrokerProtocolMember { get; set; }
    /// <summary>When the Broker Protocol membership was last confirmed/updated.</summary>
    public DateTime? BrokerProtocolUpdatedAt { get; set; }
    public bool IsExcluded { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public override string ToString() => Name;
}
