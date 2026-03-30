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
    // "Investment Adviser" (SEC RIA) or "Broker-Dealer" (FINRA)
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
    // ── Compensation types (Form ADV Item 5E / 6A) ──

    /// <summary>Whether the firm charges fee-only compensation (CSV column "5E(a)" / "6A(1)").</summary>
    public bool? CompensationFeeOnly { get; set; }

    /// <summary>Whether the firm earns commission-based compensation (CSV column "5E(b)" / "6A(2)").</summary>
    public bool? CompensationCommission { get; set; }

    /// <summary>Whether the firm charges hourly fees (CSV column "5E(c)" / "6A(3)").</summary>
    public bool? CompensationHourly { get; set; }

    /// <summary>Whether the firm charges performance-based fees (CSV column "5E(d)" / "6A(4)").</summary>
    public bool? CompensationPerformanceBased { get; set; }

    // ── Advisory activities ──

    /// <summary>Composite description of advisory service types offered (derived from Item 6 columns).</summary>
    public string? AdvisoryActivities { get; set; }

    // ── Client type breakdowns (Form ADV Item 5D sub-columns) ──

    /// <summary>Number of individual clients (CSV column "5D(1)" / "5D(a)").</summary>
    public int? ClientsIndividuals { get; set; }

    /// <summary>Number of high-net-worth individual clients (CSV column "5D(2)" / "5D(b)").</summary>
    public int? ClientsHighNetWorth { get; set; }

    /// <summary>Number of banking/thrift institution clients (CSV column "5D(3)" / "5D(c)").</summary>
    public int? ClientsBankingInstitutions { get; set; }

    /// <summary>Number of investment company clients (CSV column "5D(4)" / "5D(d)").</summary>
    public int? ClientsInvestmentCompanies { get; set; }

    /// <summary>Number of pension/profit-sharing plan clients (CSV column "5D(5)" / "5D(e)").</summary>
    public int? ClientsPensionPlans { get; set; }

    /// <summary>Number of charitable organization clients (CSV column "5D(6)" / "5D(f)").</summary>
    public int? ClientsCharitable { get; set; }

    /// <summary>Number of state/municipal government clients (CSV column "5D(7)" / "5D(g)").</summary>
    public int? ClientsGovernment { get; set; }

    /// <summary>Number of other client types (CSV column "5D(8)" / "5D(13)" / "5D(h)").</summary>
    public int? ClientsOther { get; set; }

    // ── Custody and discretion ──

    /// <summary>Whether the firm has custody of client assets (CSV column "9A" / "9A(1)").</summary>
    public bool? HasCustody { get; set; }

    /// <summary>Whether the firm exercises discretionary authority (CSV column "5F(1)").</summary>
    public bool? HasDiscretionaryAuthority { get; set; }

    // ── Private fund data ──

    /// <summary>Number of private funds managed by the firm (CSV column "7B" / "7B(1)").</summary>
    public int? PrivateFundCount { get; set; }

    /// <summary>Gross asset value of private funds in USD (CSV column "7B(1)" / "7B(2)").</summary>
    public decimal? PrivateFundGrossAssets { get; set; }

    // ── Other business ──

    /// <summary>Number of offices/locations operated by the firm (CSV column "1F" / "1.F").</summary>
    public int? NumberOfOffices { get; set; }

    /// <summary>Whether the firm is also registered as a broker-dealer (CSV column "1I" / "7A(1)").</summary>
    public bool? IsBrokerDealer { get; set; }

    /// <summary>Whether the firm is also an insurance company or agency (CSV column "7A(2)" / "7A(8)").</summary>
    public bool? IsInsuranceCompany { get; set; }

    /// <summary>Total AUM of related persons/affiliates in USD (CSV column "5F(2)(c)" / "5F(3)").</summary>
    public decimal? TotalAumRelatedPersons { get; set; }

    // ── EDGAR bulk metadata (from submissions.zip) ──

    /// <summary>EDGAR CIK number, padded to 10 digits in EDGAR but stored without leading zeros.</summary>
    public string? EdgarCik { get; set; }

    /// <summary>Standard Industry Classification code (e.g., "6282" = Investment Advice).</summary>
    public string? SicCode { get; set; }

    /// <summary>Human-readable SIC description (e.g., "Investment Advice").</summary>
    public string? SicDescription { get; set; }

    /// <summary>Fiscal year end in MMDD format (e.g., "1231" = December 31).</summary>
    public string? FiscalYearEnd { get; set; }

    /// <summary>Advisor headcount from the prior ADV filing (snapshot for headcount trend tracking).</summary>
    public int? PriorAdvisorCount { get; set; }
    /// <summary>When the PriorAdvisorCount snapshot was taken (ISO date string).</summary>
    public DateTime? PriorAdvisorCountDate { get; set; }

    // ── Feature 2: FINRA Sanctions ────────────────────────────────────────
    public bool HasActiveSanction { get; set; }
    public decimal? MaxFineAmount { get; set; }
    public string? SanctionType { get; set; }
    public DateTime? SanctionEnrichedAt { get; set; }

    // ── Feature 3: Form ADV Deep Enrichment ──────────────────────────────
    public string? InvestmentStrategies { get; set; }
    public bool? WrapFeePrograms { get; set; }
    public bool? IsDuallyRegistered { get; set; }
    public string? CCOName { get; set; }
    public string? CFOName { get; set; }
    public bool? SoftDollarArrangements { get; set; }
    public bool? CryptoExposure { get; set; }
    public bool? DirectIndexing { get; set; }
    public string? MarketingArrangements { get; set; }
    // "Individual-Owned","PE-Backed","RIA-Rollup","Bank-Owned","Other"
    public string? OwnershipStructure { get; set; }
    public DateTime? FormAdvDeepEnrichedAt { get; set; }

    // ── Feature 4: Registration Level ─────────────────────────────────────
    public string? RegistrationLevel { get; set; }

    // ── Feature 5: SEC Enforcement ────────────────────────────────────────
    public bool HasSecEnforcementAction { get; set; }
    public DateTime? SecEnforcementEnrichedAt { get; set; }

    public int? AdvisorCountChange1Yr { get; set; }

    public bool IsExcluded { get; set; }
    public bool IsWatched { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public override string ToString() => Name;
}
