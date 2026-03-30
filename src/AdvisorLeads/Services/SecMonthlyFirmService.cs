using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Downloads and parses the SEC monthly "Registered Investment Advisers" CSV file.
/// Source: https://www.sec.gov/data-research/sec-markets-data/information-about-registered-investment-advisers-exempt-reporting-advisers
/// Files are published monthly as ZIP archives containing a CSV derived from Form ADV.
/// </summary>
public class SecMonthlyFirmService
{
    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    private const string IndexPageUrl =
        "https://www.sec.gov/data-research/sec-markets-data/information-about-registered-investment-advisers-exempt-reporting-advisers";
    private const string SecBaseUrl = "https://www.sec.gov";
    private readonly string _cacheDirectory;

    static SecMonthlyFirmService()
    {
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.5");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.sec.gov/");
    }

    public SecMonthlyFirmService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _cacheDirectory = Path.Combine(appData, "AdvisorLeads", "SecFirmCache");
        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <summary>
    /// Scrapes the SEC index page to find the URL of the most recent
    /// "Registered Investment Advisers" ZIP file (excludes Exempt Reporting Advisers).
    /// Returns null if not found.
    /// </summary>
    public async Task<string?> GetLatestRegisteredAdvisersUrlAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        string html;
        try
        {
            html = await _http.GetStringAsync(IndexPageUrl, ct);
        }
        catch
        {
            return null;
        }

        // Primary: find href="...*.zip" where the path does NOT contain "exempt"
        var matches = Regex.Matches(html,
            @"href=""(/files/investment/data/[^""]+\.zip)""",
            RegexOptions.IgnoreCase);

        foreach (Match m in matches)
        {
            var href = m.Groups[1].Value;
            if (!href.Contains("exempt", StringComparison.OrdinalIgnoreCase))
                return SecBaseUrl + href;
        }

        // Fallback 1: broader path pattern
        matches = Regex.Matches(html,
            @"href=""(/(?:files|cgi-bin)/[^""]+\.zip)""",
            RegexOptions.IgnoreCase);

        foreach (Match m in matches)
        {
            var href = m.Groups[1].Value;
            if (!href.Contains("exempt", StringComparison.OrdinalIgnoreCase))
                return SecBaseUrl + href;
        }

        // Fallback 2: full absolute URLs embedded in the page
        var fullUrlMatches = Regex.Matches(html,
            @"""(https?://www\.sec\.gov[^""]+\.zip)""",
            RegexOptions.IgnoreCase);

        foreach (Match m in fullUrlMatches)
        {
            var url = m.Groups[1].Value;
            if (!url.Contains("exempt", StringComparison.OrdinalIgnoreCase))
                return url;
        }

        progress?.Report("Warning: Could not find SEC monthly firm ZIP URL. SEC page structure may have changed.");
        return null;
    }

    /// <summary>
    /// Downloads the ZIP, extracts the CSV, and parses firm records.
    /// The ZIP is cached locally in %APPDATA%\AdvisorLeads\SecFirmCache\.
    /// </summary>
    public async Task<List<Firm>> DownloadAndParseFirmsAsync(
        string zipUrl,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var fileName = Path.GetFileName(new Uri(zipUrl).LocalPath);
        var localZipPath = Path.Combine(_cacheDirectory, fileName);

        if (!File.Exists(localZipPath))
        {
            progress?.Report($"SEC Monthly: Downloading {fileName}...");
            var bytes = await _http.GetByteArrayAsync(zipUrl, ct);
            await File.WriteAllBytesAsync(localZipPath, bytes, ct);
        }
        else
        {
            progress?.Report($"SEC Monthly: Using cached {fileName}...");
        }

        progress?.Report("SEC Monthly: Parsing CSV...");
        return ParseZipCsv(localZipPath, progress);
    }

    private List<Firm> ParseZipCsv(string zipPath, IProgress<string>? progress)
    {
        var firms = new List<Firm>();
        using var archive = ZipFile.OpenRead(zipPath);

        var csvEntry = archive.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

        if (csvEntry == null)
        {
            progress?.Report("SEC Monthly: No CSV found in ZIP.");
            return firms;
        }

        using var stream = csvEntry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var headerLine = reader.ReadLine();
        if (headerLine == null) return firms;

        var headers = ParseCsvLine(headerLine);
        var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
            colIndex[headers[i].Trim()] = i;

        int count = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseCsvLine(line);
            var firm = MapRowToFirm(cols, colIndex);
            if (firm != null) firms.Add(firm);
            count++;
            if (count % 5000 == 0)
                progress?.Report($"SEC Monthly: Parsed {count:N0} firms...");
        }

        progress?.Report($"SEC Monthly: Parsed {firms.Count:N0} total firms.");
        return firms;
    }

    private static Firm? MapRowToFirm(List<string> cols, Dictionary<string, int> idx)
    {
        string Get(string key)
            => idx.TryGetValue(key, out var i) && i < cols.Count ? cols[i].Trim() : string.Empty;

        var crd = Get("Organization CRD#");
        if (string.IsNullOrWhiteSpace(crd)) return null;

        var name = Get("Primary Business Name");
        if (string.IsNullOrWhiteSpace(name)) return null;

        // Address
        var addr1 = Get("Main Office Street Address 1");
        var addr2 = Get("Main Office Street Address 2");
        var fullAddr = string.IsNullOrWhiteSpace(addr2) ? addr1
            : addr1 + ", " + addr2;

        // Mailing address (compose from parts)
        var mailStreet1 = Get("Mail Office Street Address 1");
        var mailStreet2 = Get("Mail Office Street Address 2");
        var mailCity    = Get("Mail Office City");
        var mailState   = Get("Mail Office State");
        var mailZip     = Get("Mail Office Postal Code");
        var mailingParts = new[] { mailStreet1, mailStreet2, mailCity, mailState, mailZip }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var mailingAddress = string.Join(", ", mailingParts);

        // Registration date
        DateTime? regDate = null;
        var regDateStr = Get("SEC Status Effective Date");
        if (DateTime.TryParse(regDateStr, out var d)) regDate = d;

        // AUM: 5F(2)(a) = Discretionary Regulatory AUM
        var aumRaw = Get("5F(2)(a)");
        decimal? regulatoryAum = null;
        if (!string.IsNullOrWhiteSpace(aumRaw) &&
            decimal.TryParse(aumRaw.Replace(",", ""), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedAum))
            regulatoryAum = parsedAum;

        decimal? regulatoryAumNd = null;
        var aumNdRaw = Get("5F(2)(b)");
        if (!string.IsNullOrWhiteSpace(aumNdRaw) &&
            decimal.TryParse(aumNdRaw.Replace(",", ""), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedAumNd))
            regulatoryAumNd = parsedAumNd;

        int? numClients = null;
        foreach (var clientCol in new[] { "5D", "5D(a)", "5D1" })
        {
            var cv = Get(clientCol);
            if (!string.IsNullOrWhiteSpace(cv) && int.TryParse(cv.Replace(",", ""), out var nc))
            {
                numClients = nc;
                break;
            }
        }

        var aumDescription = regulatoryAum.HasValue ? FormatAum(regulatoryAum.Value) : NullIfEmpty(aumRaw.Replace(",", "").Trim());

        // Number of employees (5A = total), investment adviser reps (5B(1))
        int? numEmployees = null;
        var numEmpStr = Get("5A");
        if (int.TryParse(numEmpStr.Replace(",", ""), out var ne) && ne > 0)
            numEmployees = ne;

        int? numAdvisors = null;
        var numAdvStr = Get("5B(1)");
        if (int.TryParse(numAdvStr.Replace(",", ""), out var na) && na > 0)
            numAdvisors = na;

        // Compensation types
        bool? compFeeOnly = ParseBool(Get("5E(a)"), Get("6A(1)"), Get("5E1"));
        bool? compCommission = ParseBool(Get("5E(b)"), Get("6A(2)"), Get("5E2"));
        bool? compHourly = ParseBool(Get("5E(c)"), Get("6A(3)"), Get("5E3"));
        bool? compPerformance = ParseBool(Get("5E(d)"), Get("6A(4)"), Get("5E4"));

        // Advisory activities (composite from Item 6 sub-columns)
        var activityLabels = new (string[] keys, string label)[]
        {
            (new[] { "6A(1)", "6A1" }, "Financial Planning"),
            (new[] { "6A(2)", "6A2" }, "Portfolio Management"),
            (new[] { "6A(3)", "6A3" }, "Pension Consulting"),
            (new[] { "6A(4)", "6A4" }, "Selection of Other Advisers"),
            (new[] { "6A(5)", "6A5" }, "Publication of Reports"),
            (new[] { "6A(6)", "6A6" }, "Security Ratings"),
            (new[] { "6A(7)", "6A7" }, "Market Timing"),
            (new[] { "6A(8)", "6A8" }, "Educational Seminars"),
            (new[] { "6A(9)", "6A9" }, "Other"),
        };
        var activities = new List<string>();
        foreach (var (keys, label) in activityLabels)
        {
            foreach (var k in keys)
            {
                var v = Get(k);
                if (IsYes(v)) { activities.Add(label); break; }
            }
        }
        string? advisoryActivities = activities.Count > 0 ? string.Join("; ", activities) : null;

        // Client type breakdowns (Item 5D sub-columns)
        int? clientsIndividuals = ParseInt(Get("5D(1)"), Get("5D(a)"), Get("5D1"));
        int? clientsHighNetWorth = ParseInt(Get("5D(2)"), Get("5D(b)"), Get("5D2"));
        int? clientsBanking = ParseInt(Get("5D(3)"), Get("5D(c)"), Get("5D3"));
        int? clientsInvestment = ParseInt(Get("5D(4)"), Get("5D(d)"), Get("5D4"));
        int? clientsPension = ParseInt(Get("5D(5)"), Get("5D(e)"), Get("5D5"));
        int? clientsCharitable = ParseInt(Get("5D(6)"), Get("5D(f)"), Get("5D6"));
        int? clientsGovt = ParseInt(Get("5D(7)"), Get("5D(g)"), Get("5D7"));
        int? clientsOther = ParseInt(Get("5D(8)"), Get("5D(13)"), Get("5D(h)"), Get("5D8"));

        // Custody and discretion
        bool? hasCustody = ParseBool(Get("9A"), Get("9A(1)"), Get("9A1"));
        bool? hasDiscretion = ParseBool(Get("5F(1)"), Get("5F1"));

        // Private fund data
        int? privateFundCount = ParseInt(Get("7B"), Get("7B(1)"), Get("7B1"));
        decimal? privateFundGrossAssets = ParseDecimal(Get("7B(1)"), Get("7B(2)"), Get("7B2"));
        // If privateFundCount consumed the same column as gross assets, disambiguate:
        // 7B = count, 7B(1) or 7B(2) = gross assets – prefer higher column index for assets
        if (privateFundCount == null)
            privateFundCount = ParseInt(Get("7B"));
        if (privateFundGrossAssets == null)
            privateFundGrossAssets = ParseDecimal(Get("7B(2)"));

        // Number of offices
        int? numOffices = ParseInt(Get("1F"), Get("1.F"), Get("1F(a)"));

        // Also registered as broker-dealer
        bool? isBd = ParseBool(Get("1I"), Get("7A(1)"), Get("7A1"));

        // Also an insurance company/agency
        bool? isInsurance = ParseBool(Get("7A(2)"), Get("7A(8)"), Get("7A2"), Get("7A8"));

        // Total AUM of related persons
        decimal? totalAumRelated = ParseDecimal(Get("5F(2)(c)"), Get("5F(3)"), Get("5F2c"));

        return new Firm
        {
            CrdNumber          = crd,
            Name               = name,
            LegalName          = NullIfEmpty(Get("Legal Name")),
            SECNumber          = NullIfEmpty(Get("SEC#")),
            SECRegion          = NullIfEmpty(Get("SEC Region")),
            Address            = NullIfEmpty(fullAddr),
            City               = NullIfEmpty(Get("Main Office City")),
            State              = NullIfEmpty(Get("Main Office State")),
            Country            = NullIfEmpty(Get("Main Office Country")),
            ZipCode            = NullIfEmpty(Get("Main Office Postal Code")),
            Phone              = NullIfEmpty(Get("Main Office Telephone Number")),
            FaxPhone           = NullIfEmpty(Get("Main Office Facsimile Number")),
            MailingAddress     = NullIfEmpty(mailingAddress),
            Website            = NullIfEmpty(Get("Website Address")),
            BusinessType       = NullIfEmpty(Get("3A")),
            StateOfOrganization = NullIfEmpty(Get("3C-State")),
            RegistrationStatus = NullIfEmpty(Get("SEC Current Status")),
            RegistrationDate   = regDate,
            LatestFilingDate   = NullIfEmpty(Get("Latest ADV Filing Date")),
            NumberOfEmployees  = numEmployees,
            NumberOfAdvisors   = numAdvisors,
            AumDescription     = aumDescription,
            RegulatoryAum      = regulatoryAum,
            RegulatoryAumNonDiscretionary = regulatoryAumNd,
            NumClients         = numClients,
            CompensationFeeOnly = compFeeOnly,
            CompensationCommission = compCommission,
            CompensationHourly = compHourly,
            CompensationPerformanceBased = compPerformance,
            AdvisoryActivities = advisoryActivities,
            ClientsIndividuals = clientsIndividuals,
            ClientsHighNetWorth = clientsHighNetWorth,
            ClientsBankingInstitutions = clientsBanking,
            ClientsInvestmentCompanies = clientsInvestment,
            ClientsPensionPlans = clientsPension,
            ClientsCharitable = clientsCharitable,
            ClientsGovernment = clientsGovt,
            ClientsOther = clientsOther,
            HasCustody = hasCustody,
            HasDiscretionaryAuthority = hasDiscretion,
            PrivateFundCount = privateFundCount,
            PrivateFundGrossAssets = privateFundGrossAssets,
            NumberOfOffices = numOffices,
            IsBrokerDealer = isBd,
            IsInsuranceCompany = isInsurance,
            TotalAumRelatedPersons = totalAumRelated,
            RecordType         = "Investment Adviser",
            IsRegisteredWithSec = true,
            Source             = "SEC",
        };
    }

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s;

    private static bool IsYes(string value)
        => value.Equals("Y", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Yes", StringComparison.OrdinalIgnoreCase)
        || value == "1";

    /// <summary>Parses a boolean from multiple candidate column values (Y/N/1/0).</summary>
    private static bool? ParseBool(params string[] values)
    {
        foreach (var v in values)
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            if (IsYes(v)) return true;
            if (v.Equals("N", StringComparison.OrdinalIgnoreCase)
                || v.Equals("No", StringComparison.OrdinalIgnoreCase)
                || v == "0")
                return false;
        }
        return null;
    }

    /// <summary>Parses an integer from multiple candidate column values.</summary>
    private static int? ParseInt(params string[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v) &&
                int.TryParse(v.Replace(",", ""), out var n))
                return n;
        }
        return null;
    }

    /// <summary>Parses a decimal from multiple candidate column values.</summary>
    private static decimal? ParseDecimal(params string[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v) &&
                decimal.TryParse(v.Replace(",", ""),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d))
                return d;
        }
        return null;
    }

    private static string FormatAum(decimal aum)
    {
        if (aum >= 1_000_000_000) return $"${aum / 1_000_000_000:F1}B";
        if (aum >= 1_000_000)     return $"${aum / 1_000_000:F1}M";
        if (aum >= 1_000)         return $"${aum / 1_000:F0}K";
        return $"${aum:F0}";
    }

    // RFC 4180-compliant CSV parser
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields;
    }
}
