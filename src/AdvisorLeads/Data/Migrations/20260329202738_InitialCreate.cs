using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvisorLeads.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdvisorCourtRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdvisorId = table.Column<int>(type: "INTEGER", nullable: true),
                    AdvisorCrd = table.Column<string>(type: "TEXT", nullable: false),
                    CaseName = table.Column<string>(type: "TEXT", nullable: false),
                    Court = table.Column<string>(type: "TEXT", nullable: true),
                    DocketNumber = table.Column<string>(type: "TEXT", nullable: true),
                    CaseType = table.Column<string>(type: "TEXT", nullable: true),
                    FilingDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: true),
                    CaseUrl = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvisorCourtRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdvisorLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvisorLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AlertType = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", nullable: false),
                    EntityCrd = table.Column<string>(type: "TEXT", nullable: false),
                    EntityName = table.Column<string>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", nullable: true),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsAcknowledged = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EdgarSearchResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirmCrd = table.Column<string>(type: "TEXT", nullable: true),
                    CompanyName = table.Column<string>(type: "TEXT", nullable: false),
                    Cik = table.Column<string>(type: "TEXT", nullable: true),
                    AccessionNumber = table.Column<string>(type: "TEXT", nullable: false),
                    FormType = table.Column<string>(type: "TEXT", nullable: true),
                    FilingDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SearchQuery = table.Column<string>(type: "TEXT", nullable: false),
                    Snippet = table.Column<string>(type: "TEXT", nullable: true),
                    FilingUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    RelevanceScore = table.Column<double>(type: "REAL", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdgarSearchResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmploymentChangeEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdvisorId = table.Column<int>(type: "INTEGER", nullable: false),
                    AdvisorCrd = table.Column<string>(type: "TEXT", nullable: false),
                    AdvisorName = table.Column<string>(type: "TEXT", nullable: true),
                    FromFirmName = table.Column<string>(type: "TEXT", nullable: true),
                    FromFirmCrd = table.Column<string>(type: "TEXT", nullable: true),
                    ToFirmName = table.Column<string>(type: "TEXT", nullable: true),
                    ToFirmCrd = table.Column<string>(type: "TEXT", nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    EffectiveDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmploymentChangeEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinraSanctions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdvisorId = table.Column<int>(type: "INTEGER", nullable: true),
                    AdvisorCrd = table.Column<string>(type: "TEXT", nullable: true),
                    FirmCrd = table.Column<string>(type: "TEXT", nullable: true),
                    SanctionType = table.Column<string>(type: "TEXT", nullable: false),
                    InitiatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    FineAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    SanctionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SuspensionStart = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SuspensionEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinraSanctions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmAumAlertRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirmCrd = table.Column<string>(type: "TEXT", nullable: false),
                    FirmName = table.Column<string>(type: "TEXT", nullable: true),
                    ThresholdType = table.Column<string>(type: "TEXT", nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    LastTriggeredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmAumAlertRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmAumHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirmCrd = table.Column<string>(type: "TEXT", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RegulatoryAum = table.Column<decimal>(type: "TEXT", nullable: true),
                    RegulatoryAumNonDiscretionary = table.Column<decimal>(type: "TEXT", nullable: true),
                    TotalAum = table.Column<decimal>(type: "TEXT", nullable: true),
                    NumberOfEmployees = table.Column<int>(type: "INTEGER", nullable: true),
                    NumberOfAdvisors = table.Column<int>(type: "INTEGER", nullable: true),
                    NumClients = table.Column<int>(type: "INTEGER", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmAumHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmFilingEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirmCrd = table.Column<string>(type: "TEXT", nullable: false),
                    EventDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    PercentChange = table.Column<double>(type: "REAL", nullable: true),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    IsReviewed = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmFilingEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmFilings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirmCrd = table.Column<string>(type: "TEXT", nullable: false),
                    Cik = table.Column<string>(type: "TEXT", nullable: true),
                    AccessionNumber = table.Column<string>(type: "TEXT", nullable: false),
                    FormType = table.Column<string>(type: "TEXT", nullable: false),
                    FilingDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AcceptanceDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PrimaryDocument = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    FilingUrl = table.Column<string>(type: "TEXT", nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmFilings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmOwnership",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirmCrd = table.Column<string>(type: "TEXT", nullable: false),
                    FilingDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OwnerName = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    OwnershipPercent = table.Column<decimal>(type: "TEXT", nullable: true),
                    IsDirectOwner = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    EntityType = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: true),
                    OwnerCrd = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmOwnership", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Firms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CrdNumber = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    ZipCode = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Website = table.Column<string>(type: "TEXT", nullable: true),
                    BusinessType = table.Column<string>(type: "TEXT", nullable: true),
                    IsRegisteredWithSec = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsRegisteredWithFinra = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    NumberOfAdvisors = table.Column<int>(type: "INTEGER", nullable: true),
                    RegistrationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: true),
                    RecordType = table.Column<string>(type: "TEXT", nullable: true),
                    SECNumber = table.Column<string>(type: "TEXT", nullable: true),
                    SECRegion = table.Column<string>(type: "TEXT", nullable: true),
                    LegalName = table.Column<string>(type: "TEXT", nullable: true),
                    FaxPhone = table.Column<string>(type: "TEXT", nullable: true),
                    MailingAddress = table.Column<string>(type: "TEXT", nullable: true),
                    RegistrationStatus = table.Column<string>(type: "TEXT", nullable: true),
                    AumDescription = table.Column<string>(type: "TEXT", nullable: true),
                    StateOfOrganization = table.Column<string>(type: "TEXT", nullable: true),
                    Country = table.Column<string>(type: "TEXT", nullable: true),
                    NumberOfEmployees = table.Column<int>(type: "INTEGER", nullable: true),
                    LatestFilingDate = table.Column<string>(type: "TEXT", nullable: true),
                    RegulatoryAum = table.Column<decimal>(type: "TEXT", nullable: true),
                    RegulatoryAumNonDiscretionary = table.Column<decimal>(type: "TEXT", nullable: true),
                    NumClients = table.Column<int>(type: "INTEGER", nullable: true),
                    BrokerProtocolMember = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    BrokerProtocolUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompensationFeeOnly = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: false),
                    CompensationCommission = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: false),
                    CompensationHourly = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: false),
                    CompensationPerformanceBased = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: false),
                    AdvisoryActivities = table.Column<string>(type: "TEXT", nullable: true),
                    ClientsIndividuals = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientsHighNetWorth = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientsBankingInstitutions = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientsInvestmentCompanies = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientsPensionPlans = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientsCharitable = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientsGovernment = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientsOther = table.Column<int>(type: "INTEGER", nullable: true),
                    HasCustody = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: false),
                    HasDiscretionaryAuthority = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: false),
                    PrivateFundCount = table.Column<int>(type: "INTEGER", nullable: true),
                    PrivateFundGrossAssets = table.Column<decimal>(type: "TEXT", nullable: true),
                    NumberOfOffices = table.Column<int>(type: "INTEGER", nullable: true),
                    IsBrokerDealer = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: false),
                    IsInsuranceCompany = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: false),
                    TotalAumRelatedPersons = table.Column<decimal>(type: "TEXT", nullable: true),
                    EdgarCik = table.Column<string>(type: "TEXT", nullable: true),
                    SicCode = table.Column<string>(type: "TEXT", nullable: true),
                    SicDescription = table.Column<string>(type: "TEXT", nullable: true),
                    FiscalYearEnd = table.Column<string>(type: "TEXT", nullable: true),
                    PriorAdvisorCount = table.Column<int>(type: "INTEGER", nullable: true),
                    PriorAdvisorCountDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HasActiveSanction = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxFineAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    SanctionType = table.Column<string>(type: "TEXT", nullable: true),
                    SanctionEnrichedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InvestmentStrategies = table.Column<string>(type: "TEXT", nullable: true),
                    WrapFeePrograms = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsDuallyRegistered = table.Column<bool>(type: "INTEGER", nullable: true),
                    CCOName = table.Column<string>(type: "TEXT", nullable: true),
                    CFOName = table.Column<string>(type: "TEXT", nullable: true),
                    SoftDollarArrangements = table.Column<bool>(type: "INTEGER", nullable: true),
                    CryptoExposure = table.Column<bool>(type: "INTEGER", nullable: true),
                    DirectIndexing = table.Column<bool>(type: "INTEGER", nullable: true),
                    MarketingArrangements = table.Column<string>(type: "TEXT", nullable: true),
                    OwnershipStructure = table.Column<string>(type: "TEXT", nullable: true),
                    FormAdvDeepEnrichedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RegistrationLevel = table.Column<string>(type: "TEXT", nullable: true),
                    HasSecEnforcementAction = table.Column<bool>(type: "INTEGER", nullable: false),
                    SecEnforcementEnrichedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AdvisorCountChange1Yr = table.Column<int>(type: "INTEGER", nullable: true),
                    IsExcluded = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsWatched = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Firms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FormAdvFilings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirmCrd = table.Column<string>(type: "TEXT", nullable: false),
                    FilingDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FilingType = table.Column<string>(type: "TEXT", nullable: true),
                    RegulatoryAum = table.Column<decimal>(type: "TEXT", nullable: true),
                    RegulatoryAumNonDiscretionary = table.Column<decimal>(type: "TEXT", nullable: true),
                    TotalAum = table.Column<decimal>(type: "TEXT", nullable: true),
                    NumberOfEmployees = table.Column<int>(type: "INTEGER", nullable: true),
                    NumberOfAdvisors = table.Column<int>(type: "INTEGER", nullable: true),
                    AdvisorCount = table.Column<int>(type: "INTEGER", nullable: true),
                    NumClients = table.Column<int>(type: "INTEGER", nullable: true),
                    RegistrationStatus = table.Column<string>(type: "TEXT", nullable: true),
                    BusinessName = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormAdvFilings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketWatchRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleName = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    RecordType = table.Column<string>(type: "TEXT", nullable: true),
                    LicenseContains = table.Column<string>(type: "TEXT", nullable: true),
                    MinYearsExperience = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketWatchRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecEnforcementActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdvisorCrd = table.Column<string>(type: "TEXT", nullable: true),
                    FirmCrd = table.Column<string>(type: "TEXT", nullable: true),
                    RespondentName = table.Column<string>(type: "TEXT", nullable: true),
                    ActionType = table.Column<string>(type: "TEXT", nullable: false),
                    FileDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CaseUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ReleaseNumber = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecEnforcementActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Advisors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CrdNumber = table.Column<string>(type: "TEXT", nullable: true),
                    IapdNumber = table.Column<string>(type: "TEXT", nullable: true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    MiddleName = table.Column<string>(type: "TEXT", nullable: true),
                    OtherNames = table.Column<string>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    ZipCode = table.Column<string>(type: "TEXT", nullable: true),
                    Licenses = table.Column<string>(type: "TEXT", nullable: true),
                    Qualifications = table.Column<string>(type: "TEXT", nullable: true),
                    CurrentFirmName = table.Column<string>(type: "TEXT", nullable: true),
                    CurrentFirmCrd = table.Column<string>(type: "TEXT", nullable: true),
                    CurrentFirmId = table.Column<int>(type: "INTEGER", nullable: true),
                    RegistrationStatus = table.Column<string>(type: "TEXT", nullable: true),
                    RegistrationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    YearsOfExperience = table.Column<int>(type: "INTEGER", nullable: true),
                    HasDisclosures = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DisclosureCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Source = table.Column<string>(type: "TEXT", nullable: true),
                    RecordType = table.Column<string>(type: "TEXT", nullable: true),
                    Suffix = table.Column<string>(type: "TEXT", nullable: true),
                    IapdLink = table.Column<string>(type: "TEXT", nullable: true),
                    RegAuthorities = table.Column<string>(type: "TEXT", nullable: true),
                    DisclosureFlags = table.Column<string>(type: "TEXT", nullable: true),
                    HasCriminalDisclosure = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasRegulatoryDisclosure = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasCivilDisclosure = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasCustomerComplaintDisclosure = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasFinancialDisclosure = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasTerminationDisclosure = table.Column<bool>(type: "INTEGER", nullable: false),
                    BcDisclosureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IaDisclosureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    BcScope = table.Column<string>(type: "TEXT", nullable: true),
                    IaScope = table.Column<string>(type: "TEXT", nullable: true),
                    CareerStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalFirmCount = table.Column<int>(type: "INTEGER", nullable: true),
                    BrokerCheckUrl = table.Column<string>(type: "TEXT", nullable: true),
                    CurrentFirmStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsExcluded = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ExclusionReason = table.Column<string>(type: "TEXT", nullable: true),
                    IsFavorited = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsWatched = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsImportedToCrm = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CrmId = table.Column<string>(type: "TEXT", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    HasRecentFirmChange = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirmChangeDetectedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastEmploymentCheckDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HasActiveSanction = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxFineAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    SanctionType = table.Column<string>(type: "TEXT", nullable: true),
                    SanctionEnrichedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RegistrationLevel = table.Column<string>(type: "TEXT", nullable: true),
                    HasSecEnforcementAction = table.Column<bool>(type: "INTEGER", nullable: false),
                    SecEnforcementUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SecEnforcementEnrichedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CourtRecordFlag = table.Column<bool>(type: "INTEGER", nullable: false),
                    CourtRecordUrl = table.Column<string>(type: "TEXT", nullable: true),
                    CourtRecordDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CourtRecordSummary = table.Column<string>(type: "TEXT", nullable: true),
                    CourtRecordEnrichedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MobilityScore = table.Column<int>(type: "INTEGER", nullable: true),
                    DisclosureSeverityScore = table.Column<int>(type: "INTEGER", nullable: true),
                    MobilityScoreUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Advisors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Advisors_Firms_CurrentFirmId",
                        column: x => x.CurrentFirmId,
                        principalTable: "Firms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AdvisorListMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ListId = table.Column<int>(type: "INTEGER", nullable: false),
                    AdvisorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvisorListMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdvisorListMembers_AdvisorLists_ListId",
                        column: x => x.ListId,
                        principalTable: "AdvisorLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdvisorListMembers_Advisors_AdvisorId",
                        column: x => x.AdvisorId,
                        principalTable: "Advisors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdvisorRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdvisorId = table.Column<int>(type: "INTEGER", nullable: false),
                    StateCode = table.Column<string>(type: "TEXT", nullable: false),
                    RegistrationCategory = table.Column<string>(type: "TEXT", nullable: true),
                    RegistrationStatus = table.Column<string>(type: "TEXT", nullable: true),
                    StatusDate = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvisorRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdvisorRegistrations_Advisors_AdvisorId",
                        column: x => x.AdvisorId,
                        principalTable: "Advisors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Disclosures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdvisorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Resolution = table.Column<string>(type: "TEXT", nullable: true),
                    Sanctions = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Disclosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Disclosures_Advisors_AdvisorId",
                        column: x => x.AdvisorId,
                        principalTable: "Advisors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmploymentHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdvisorId = table.Column<int>(type: "INTEGER", nullable: false),
                    FirmName = table.Column<string>(type: "TEXT", nullable: false),
                    FirmCrd = table.Column<string>(type: "TEXT", nullable: true),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Position = table.Column<string>(type: "TEXT", nullable: true),
                    Street = table.Column<string>(type: "TEXT", nullable: true),
                    FirmCity = table.Column<string>(type: "TEXT", nullable: true),
                    FirmState = table.Column<string>(type: "TEXT", nullable: true),
                    RegistrationCategories = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmploymentHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmploymentHistory_Advisors_AdvisorId",
                        column: x => x.AdvisorId,
                        principalTable: "Advisors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Qualifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdvisorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Qualifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Qualifications_Advisors_AdvisorId",
                        column: x => x.AdvisorId,
                        principalTable: "Advisors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorCourtRecords_AdvisorCrd",
                table: "AdvisorCourtRecords",
                column: "AdvisorCrd");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorListMembers_AdvisorId",
                table: "AdvisorListMembers",
                column: "AdvisorId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorListMembers_ListId",
                table: "AdvisorListMembers",
                column: "ListId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorListMembers_ListId_AdvisorId",
                table: "AdvisorListMembers",
                columns: new[] { "ListId", "AdvisorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorRegistrations_AdvisorId",
                table: "AdvisorRegistrations",
                column: "AdvisorId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorRegistrations_AdvisorId_StateCode_RegistrationCategory",
                table: "AdvisorRegistrations",
                columns: new[] { "AdvisorId", "StateCode", "RegistrationCategory" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_City",
                table: "Advisors",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_CrdNumber",
                table: "Advisors",
                column: "CrdNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_CurrentFirmCrd",
                table: "Advisors",
                column: "CurrentFirmCrd");

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_CurrentFirmId",
                table: "Advisors",
                column: "CurrentFirmId");

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_CurrentFirmName",
                table: "Advisors",
                column: "CurrentFirmName");

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_DisclosureCount",
                table: "Advisors",
                column: "DisclosureCount");

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_HasDisclosures",
                table: "Advisors",
                column: "HasDisclosures");

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_IsExcluded",
                table: "Advisors",
                column: "IsExcluded");

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_IsExcluded_IsFavorited",
                table: "Advisors",
                columns: new[] { "IsExcluded", "IsFavorited" });

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_IsExcluded_LastName",
                table: "Advisors",
                columns: new[] { "IsExcluded", "LastName" });

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_IsFavorited",
                table: "Advisors",
                column: "IsFavorited");

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_IsImportedToCrm",
                table: "Advisors",
                column: "IsImportedToCrm");

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_IsWatched",
                table: "Advisors",
                column: "IsWatched");

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_LastName",
                table: "Advisors",
                column: "LastName");

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_RecordType",
                table: "Advisors",
                column: "RecordType");

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_Source",
                table: "Advisors",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_State",
                table: "Advisors",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_State_RecordType_RegistrationStatus_LastName",
                table: "Advisors",
                columns: new[] { "State", "RecordType", "RegistrationStatus", "LastName" });

            migrationBuilder.CreateIndex(
                name: "IX_Advisors_YearsOfExperience",
                table: "Advisors",
                column: "YearsOfExperience");

            migrationBuilder.CreateIndex(
                name: "IX_AlertLog_AlertType",
                table: "AlertLog",
                column: "AlertType");

            migrationBuilder.CreateIndex(
                name: "IX_AlertLog_DetectedAt",
                table: "AlertLog",
                column: "DetectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AlertLog_EntityCrd",
                table: "AlertLog",
                column: "EntityCrd");

            migrationBuilder.CreateIndex(
                name: "IX_AlertLog_EntityCrd_AlertType_DetectedAt",
                table: "AlertLog",
                columns: new[] { "EntityCrd", "AlertType", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertLog_IsAcknowledged",
                table: "AlertLog",
                column: "IsAcknowledged");

            migrationBuilder.CreateIndex(
                name: "IX_AlertLog_IsRead",
                table: "AlertLog",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_Disclosures_AdvisorId",
                table: "Disclosures",
                column: "AdvisorId");

            migrationBuilder.CreateIndex(
                name: "IX_EdgarSearchResults_AccessionNumber",
                table: "EdgarSearchResults",
                column: "AccessionNumber");

            migrationBuilder.CreateIndex(
                name: "IX_EdgarSearchResults_AccessionNumber_SearchQuery",
                table: "EdgarSearchResults",
                columns: new[] { "AccessionNumber", "SearchQuery" });

            migrationBuilder.CreateIndex(
                name: "IX_EdgarSearchResults_Category",
                table: "EdgarSearchResults",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_EdgarSearchResults_FirmCrd",
                table: "EdgarSearchResults",
                column: "FirmCrd");

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentChangeEvents_AdvisorCrd",
                table: "EmploymentChangeEvents",
                column: "AdvisorCrd");

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentChangeEvents_DetectedAt",
                table: "EmploymentChangeEvents",
                column: "DetectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentHistory_AdvisorId",
                table: "EmploymentHistory",
                column: "AdvisorId");

            migrationBuilder.CreateIndex(
                name: "IX_FinraSanctions_AdvisorCrd",
                table: "FinraSanctions",
                column: "AdvisorCrd");

            migrationBuilder.CreateIndex(
                name: "IX_FinraSanctions_FirmCrd",
                table: "FinraSanctions",
                column: "FirmCrd");

            migrationBuilder.CreateIndex(
                name: "IX_FirmAumAlertRules_FirmCrd",
                table: "FirmAumAlertRules",
                column: "FirmCrd");

            migrationBuilder.CreateIndex(
                name: "IX_FirmAumAlertRules_IsActive",
                table: "FirmAumAlertRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FirmAumHistory_FirmCrd",
                table: "FirmAumHistory",
                column: "FirmCrd");

            migrationBuilder.CreateIndex(
                name: "IX_FirmAumHistory_FirmCrd_SnapshotDate",
                table: "FirmAumHistory",
                columns: new[] { "FirmCrd", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmAumHistory_SnapshotDate",
                table: "FirmAumHistory",
                column: "SnapshotDate");

            migrationBuilder.CreateIndex(
                name: "IX_FirmFilingEvents_EventDate",
                table: "FirmFilingEvents",
                column: "EventDate");

            migrationBuilder.CreateIndex(
                name: "IX_FirmFilingEvents_EventType",
                table: "FirmFilingEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_FirmFilingEvents_FirmCrd",
                table: "FirmFilingEvents",
                column: "FirmCrd");

            migrationBuilder.CreateIndex(
                name: "IX_FirmFilingEvents_FirmCrd_EventDate",
                table: "FirmFilingEvents",
                columns: new[] { "FirmCrd", "EventDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FirmFilingEvents_Severity",
                table: "FirmFilingEvents",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_FirmFilings_AccessionNumber",
                table: "FirmFilings",
                column: "AccessionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmFilings_FirmCrd",
                table: "FirmFilings",
                column: "FirmCrd");

            migrationBuilder.CreateIndex(
                name: "IX_FirmFilings_FirmCrd_FilingDate",
                table: "FirmFilings",
                columns: new[] { "FirmCrd", "FilingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FirmOwnership_FirmCrd",
                table: "FirmOwnership",
                column: "FirmCrd");

            migrationBuilder.CreateIndex(
                name: "IX_FirmOwnership_FirmCrd_FilingDate",
                table: "FirmOwnership",
                columns: new[] { "FirmCrd", "FilingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Firms_BrokerProtocolMember",
                table: "Firms",
                column: "BrokerProtocolMember");

            migrationBuilder.CreateIndex(
                name: "IX_Firms_City",
                table: "Firms",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_Firms_CrdNumber",
                table: "Firms",
                column: "CrdNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Firms_IsExcluded",
                table: "Firms",
                column: "IsExcluded");

            migrationBuilder.CreateIndex(
                name: "IX_Firms_IsExcluded_State_RecordType",
                table: "Firms",
                columns: new[] { "IsExcluded", "State", "RecordType" });

            migrationBuilder.CreateIndex(
                name: "IX_Firms_IsWatched",
                table: "Firms",
                column: "IsWatched");

            migrationBuilder.CreateIndex(
                name: "IX_Firms_Name",
                table: "Firms",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Firms_RecordType",
                table: "Firms",
                column: "RecordType");

            migrationBuilder.CreateIndex(
                name: "IX_Firms_RegulatoryAum",
                table: "Firms",
                column: "RegulatoryAum");

            migrationBuilder.CreateIndex(
                name: "IX_Firms_State_RegistrationStatus_Name",
                table: "Firms",
                columns: new[] { "State", "RegistrationStatus", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_FormAdvFilings_FilingDate",
                table: "FormAdvFilings",
                column: "FilingDate");

            migrationBuilder.CreateIndex(
                name: "IX_FormAdvFilings_FirmCrd",
                table: "FormAdvFilings",
                column: "FirmCrd");

            migrationBuilder.CreateIndex(
                name: "IX_FormAdvFilings_FirmCrd_FilingDate",
                table: "FormAdvFilings",
                columns: new[] { "FirmCrd", "FilingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketWatchRules_IsActive",
                table: "MarketWatchRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Qualifications_AdvisorId",
                table: "Qualifications",
                column: "AdvisorId");

            migrationBuilder.CreateIndex(
                name: "IX_SecEnforcementActions_AdvisorCrd",
                table: "SecEnforcementActions",
                column: "AdvisorCrd");

            migrationBuilder.CreateIndex(
                name: "IX_SecEnforcementActions_FirmCrd",
                table: "SecEnforcementActions",
                column: "FirmCrd");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdvisorCourtRecords");

            migrationBuilder.DropTable(
                name: "AdvisorListMembers");

            migrationBuilder.DropTable(
                name: "AdvisorRegistrations");

            migrationBuilder.DropTable(
                name: "AlertLog");

            migrationBuilder.DropTable(
                name: "Disclosures");

            migrationBuilder.DropTable(
                name: "EdgarSearchResults");

            migrationBuilder.DropTable(
                name: "EmploymentChangeEvents");

            migrationBuilder.DropTable(
                name: "EmploymentHistory");

            migrationBuilder.DropTable(
                name: "FinraSanctions");

            migrationBuilder.DropTable(
                name: "FirmAumAlertRules");

            migrationBuilder.DropTable(
                name: "FirmAumHistory");

            migrationBuilder.DropTable(
                name: "FirmFilingEvents");

            migrationBuilder.DropTable(
                name: "FirmFilings");

            migrationBuilder.DropTable(
                name: "FirmOwnership");

            migrationBuilder.DropTable(
                name: "FormAdvFilings");

            migrationBuilder.DropTable(
                name: "MarketWatchRules");

            migrationBuilder.DropTable(
                name: "Qualifications");

            migrationBuilder.DropTable(
                name: "SecEnforcementActions");

            migrationBuilder.DropTable(
                name: "AdvisorLists");

            migrationBuilder.DropTable(
                name: "Advisors");

            migrationBuilder.DropTable(
                name: "Firms");
        }
    }
}
