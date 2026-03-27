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
    public async Task<string?> GetLatestRegisteredAdvisersUrlAsync(CancellationToken ct = default)
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

        // Find href="...*.zip" where the .zip path does NOT contain "exempt"
        var matches = Regex.Matches(html,
            @"href=""(/files/investment/data/[^""]+\.zip)""",
            RegexOptions.IgnoreCase);

        foreach (Match m in matches)
        {
            var href = m.Groups[1].Value;
            if (!href.Contains("exempt", StringComparison.OrdinalIgnoreCase))
                return SecBaseUrl + href;
        }

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
        // Strip whitespace and trailing ".00" noise
        var aumClean = aumRaw.Replace(",", "").Trim();

        // Number of employees (5A = total), investment adviser reps (5B(1))
        int? numEmployees = null;
        var numEmpStr = Get("5A");
        if (int.TryParse(numEmpStr.Replace(",", ""), out var ne) && ne > 0)
            numEmployees = ne;

        int? numAdvisors = null;
        var numAdvStr = Get("5B(1)");
        if (int.TryParse(numAdvStr.Replace(",", ""), out var na) && na > 0)
            numAdvisors = na;

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
            AumDescription     = NullIfEmpty(aumClean),
            RecordType         = "Investment Advisor",
            IsRegisteredWithSec = true,
            Source             = "SEC",
        };
    }

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s;

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
