using AdvisorLeads.Models;
using System.IO.Compression;
using System.Xml;

namespace AdvisorLeads.Services;

/// <summary>
/// Service for downloading and parsing SEC IAPD compilation data files.
/// Downloads gzipped XML files containing investment advisor firms and individual representatives.
/// Data is sourced from https://adviserinfo.sec.gov/compilation
/// </summary>
public class SecCompilationService
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;

    // SEC IAPD compilation download URLs — gzipped XML published by SEC/IARD
    // If these URLs change, update them here.
    private const string IndividualsGzUrl = "https://adviserinfo.sec.gov/downloads/prod/iapd/compiled/individuals/individuals.xml.gz";
    private const string FirmsGzUrl       = "https://adviserinfo.sec.gov/downloads/prod/iapd/compiled/ria/ria.xml.gz";

    public SecCompilationService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
        // Mimic a browser so the SEC server doesn't block the request
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Referer",
            "https://adviserinfo.sec.gov/");

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _cacheDirectory = Path.Combine(appDataPath, "AdvisorLeads", "SecCache");
        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <summary>
    /// Downloads and parses SEC individual investment advisor representative data.
    /// </summary>
    public async Task<List<Advisor>> DownloadAndParseIndividualsAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report("SEC: Downloading individuals data file...");

        var xmlFilePath = await DownloadAndExtractAsync(
            IndividualsGzUrl,
            "individuals.xml.gz",
            progress,
            cancellationToken);

        if (xmlFilePath == null)
        {
            progress?.Report("SEC: Failed to download individuals data.");
            return new List<Advisor>();
        }

        progress?.Report("SEC: Parsing individuals XML data...");
        return await ParseIndividualsXmlAsync(xmlFilePath, progress, cancellationToken);
    }

    /// <summary>
    /// Downloads and parses SEC investment advisor firm data.
    /// </summary>
    public async Task<List<Firm>> DownloadAndParseFirmsAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report("SEC: Downloading firms data file...");

        var xmlFilePath = await DownloadAndExtractAsync(
            FirmsGzUrl,
            "ria.xml.gz",
            progress,
            cancellationToken);

        if (xmlFilePath == null)
        {
            progress?.Report("SEC: Failed to download firms data.");
            return new List<Firm>();
        }

        progress?.Report("SEC: Parsing firms XML data...");
        return await ParseFirmsXmlAsync(xmlFilePath, progress, cancellationToken);
    }

    /// <summary>
    /// Downloads a gzipped file and extracts it to the cache directory.
    /// Returns the path to the extracted XML file, or null on failure.
    /// </summary>
    private async Task<string?> DownloadAndExtractAsync(
        string url,
        string fileName,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        TimeSpan? cacheExpiry = null)
    {
        try
        {
            var gzipFilePath = Path.Combine(_cacheDirectory, fileName);
            var xmlFilePath = Path.Combine(_cacheDirectory, Path.GetFileNameWithoutExtension(fileName));
            var maxAge = cacheExpiry ?? TimeSpan.FromHours(23);
            const long MinValidXmlBytes = 100 * 1024; // 100 KB — anything smaller is a corrupt/empty download

            // Reuse cached XML only if it exists, is fresh, and has real content
            if (File.Exists(xmlFilePath))
            {
                var info = new FileInfo(xmlFilePath);
                if (info.Length < MinValidXmlBytes)
                {
                    progress?.Report($"SEC: Cached {Path.GetFileName(xmlFilePath)} is too small ({info.Length:N0} bytes) — deleting and re-downloading.");
                    try { File.Delete(xmlFilePath); } catch { }
                    try { File.Delete(gzipFilePath); } catch { }
                }
                else
                {
                    var age = DateTime.UtcNow - info.LastWriteTimeUtc;
                    if (age < maxAge)
                    {
                        progress?.Report($"SEC: Using cached {Path.GetFileName(xmlFilePath)} ({age.TotalHours:F1}h old, {info.Length / (1024 * 1024):N0} MB).");
                        return xmlFilePath;
                    }
                    progress?.Report($"SEC: Cache expired ({age.TotalHours:F1}h old). Re-downloading {fileName}...");
                }
            }

            // Download the .gz file
            progress?.Report($"SEC: Connecting to {url}...");
            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                progress?.Report($"SEC: Response {(int)response.StatusCode} {response.StatusCode}  Content-Type: {contentType}");

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    progress?.Report($"SEC: Server returned {(int)response.StatusCode}. Response: {body[..Math.Min(300, body.Length)]}");
                    return null;
                }

                // If the server returned HTML instead of binary, report the content so we can diagnose the URL
                if (contentType.Contains("text/html") || contentType.Contains("text/plain"))
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    progress?.Report($"SEC: Server returned HTML/text (not a gzip file). Check the URL. Response snippet: {body[..Math.Min(400, body.Length)]}");
                    return null;
                }

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                progress?.Report($"SEC: Downloading {fileName} ({(totalBytes > 0 ? $"{totalBytes / (1024 * 1024):N0} MB" : "size unknown")})...");

                using var fileStream = new FileStream(gzipFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536, useAsync: true);
                using var downloadStream = await response.Content.ReadAsStreamAsync(cancellationToken);

                var buffer = new byte[65536];
                long bytesRead = 0;
                int lastProgress = 0;

                while (true)
                {
                    var read = await downloadStream.ReadAsync(buffer, cancellationToken);
                    if (read == 0) break;

                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    bytesRead += read;

                    if (totalBytes > 0)
                    {
                        var currentProgress = (int)((bytesRead * 100) / totalBytes);
                        if (currentProgress > lastProgress && currentProgress % 5 == 0)
                        {
                            progress?.Report($"SEC: Downloaded {currentProgress}% ({bytesRead / (1024 * 1024):N0} MB / {totalBytes / (1024 * 1024):N0} MB)...");
                            lastProgress = currentProgress;
                        }
                    }
                    else if (bytesRead % (10 * 1024 * 1024) == 0 && bytesRead > 0)
                    {
                        progress?.Report($"SEC: Downloaded {bytesRead / (1024 * 1024):N0} MB...");
                    }
                }

                progress?.Report($"SEC: Download complete ({bytesRead / (1024 * 1024):N0} MB). Checking integrity...");

                if (bytesRead < MinValidXmlBytes)
                {
                    try { File.Delete(gzipFilePath); } catch { }
                    progress?.Report($"SEC: Downloaded file is too small ({bytesRead:N0} bytes) — the server may have returned an error page. Check the URL.");
                    return null;
                }
            }

            progress?.Report($"SEC: Extracting {fileName}...");

            // Extract the gzip file; clean up partial output on error
            try
            {
                using var gzipStream = new FileStream(gzipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var decompressedStream = new GZipStream(gzipStream, CompressionMode.Decompress);
                using var outputStream = new FileStream(xmlFilePath, FileMode.Create, FileAccess.Write);
                await decompressedStream.CopyToAsync(outputStream, cancellationToken);
            }
            catch
            {
                try { File.Delete(xmlFilePath); } catch { }
                throw;
            }

            // Delete the .gz file to save space
            try { File.Delete(gzipFilePath); } catch { }

            var xmlSize = new FileInfo(xmlFilePath).Length;
            progress?.Report($"SEC: Extraction complete. XML file: {xmlSize / (1024 * 1024):N0} MB");

            if (xmlSize < MinValidXmlBytes)
            {
                try { File.Delete(xmlFilePath); } catch { }
                progress?.Report($"SEC: Extracted XML is too small ({xmlSize:N0} bytes) — extraction may have failed.");
                return null;
            }

            return xmlFilePath;
        }
        catch (Exception ex)
        {
            progress?.Report($"SEC: Error downloading/extracting {fileName}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private async Task<List<Advisor>> ParseIndividualsXmlAsync(
        string xmlFilePath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var advisors = new List<Advisor>();

        try
        {
            await Task.Run(() =>
            {
                using var fileStream = new FileStream(xmlFilePath, FileMode.Open, FileAccess.Read);
                using var reader = XmlReader.Create(fileStream, new XmlReaderSettings
                {
                    Async = false,
                    IgnoreWhitespace = true,
                    IgnoreComments = true
                });

                int count = 0;
                int lastReportedCount = 0;

                while (reader.Read())
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "Indvl")
                    {
                        var advisor = ParseIndividualElement(reader.ReadSubtree());
                        if (advisor != null)
                        {
                            advisors.Add(advisor);
                            count++;

                            if (count - lastReportedCount >= 1000)
                            {
                                progress?.Report($"SEC: Parsed {count:N0} individuals...");
                                lastReportedCount = count;
                            }
                        }
                    }
                }

                progress?.Report($"SEC: Completed parsing {count:N0} individuals.");
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            progress?.Report($"SEC: Error parsing individuals XML: {ex.Message}");
        }

        return advisors;
    }

    private Advisor? ParseIndividualElement(XmlReader reader)
    {
        try
        {
            var advisor = new Advisor
            {
                Source = "SEC",
                RecordType = "Investment Advisor Representative",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                EmploymentHistory = new List<EmploymentHistory>(),
                Disclosures = new List<Disclosure>(),
                QualificationList = new List<Qualification>()
            };

            var regAuthorities = new List<string>();
            var licenses = new List<string>();
            var disclosureFlags = new List<string>();
            bool firstEmp = true;
            bool firstRegDate = true;
            var registrations = new List<AdvisorRegistration>();

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                switch (reader.Name)
                {
                    case "Info":
                    {
                        advisor.LastName = reader.GetAttribute("lastNm") ?? string.Empty;
                        advisor.FirstName = reader.GetAttribute("firstNm") ?? string.Empty;
                        advisor.MiddleName = reader.GetAttribute("midNm");
                        advisor.Suffix = reader.GetAttribute("sufNm");
                        var pk = reader.GetAttribute("indvlPK");
                        if (!string.IsNullOrWhiteSpace(pk))
                        {
                            advisor.CrdNumber = pk;
                            advisor.IapdNumber = pk;
                        }
                        advisor.IapdLink = reader.GetAttribute("link");
                        break;
                    }

                    case "CrntEmp":
                    {
                        var empName = reader.GetAttribute("orgNm");
                        if (firstEmp)
                        {
                            advisor.CurrentFirmName = empName;
                            advisor.CurrentFirmCrd = reader.GetAttribute("orgPK");
                            advisor.City = reader.GetAttribute("city");
                            advisor.State = reader.GetAttribute("state");
                            advisor.ZipCode = reader.GetAttribute("postlCd");
                            firstEmp = false;
                        }
                        if (!string.IsNullOrWhiteSpace(empName))
                        {
                            advisor.EmploymentHistory.Add(new EmploymentHistory
                            {
                                FirmName = empName,
                                FirmCrd = reader.GetAttribute("orgPK"),
                                FirmCity = reader.GetAttribute("city"),
                                FirmState = reader.GetAttribute("state")
                                // EndDate left null → IsCurrent == true
                            });
                        }
                        break;
                    }

                    case "CrntRgstn":
                    {
                        var regAuth = reader.GetAttribute("regAuth");
                        var st = reader.GetAttribute("st");
                        var stDt = reader.GetAttribute("stDt");

                        if (!string.IsNullOrWhiteSpace(regAuth) && !regAuthorities.Contains(regAuth))
                            regAuthorities.Add(regAuth);

                        if (advisor.RegistrationStatus == null && !string.IsNullOrWhiteSpace(st))
                            advisor.RegistrationStatus = FinraService.NormalizeStatus(st);

                        if (firstRegDate && DateTime.TryParse(stDt, out var regDate))
                        {
                            advisor.RegistrationDate = regDate;
                            advisor.YearsOfExperience = (int)((DateTime.Today - regDate).TotalDays / 365.25);
                            firstRegDate = false;
                        }
                        break;
                    }

                    case "Exm":
                    {
                        var exmCd = reader.GetAttribute("exmCd");
                        var exmNm = reader.GetAttribute("exmNm");
                        var exmDt = reader.GetAttribute("exmDt");

                        if (!string.IsNullOrWhiteSpace(exmNm))
                        {
                            var qual = new Qualification
                            {
                                Code = exmCd,
                                Name = exmNm
                            };
                            if (DateTime.TryParse(exmDt, out var examDate))
                                qual.Date = examDate;

                            advisor.QualificationList.Add(qual);

                            var licLabel = !string.IsNullOrWhiteSpace(exmCd) ? exmCd : exmNm;
                            if (!licenses.Contains(licLabel))
                                licenses.Add(licLabel);
                        }
                        break;
                    }

                    case "EmpHs":
                    case "EmpH":  // handle both container-child (EmpHs > EmpH) and flat (repeating EmpHs) structures
                    {
                        var empOrg = reader.GetAttribute("orgNm");
                        var fromDt = reader.GetAttribute("fromDt");
                        var toDt = reader.GetAttribute("toDt");

                        if (!string.IsNullOrWhiteSpace(empOrg))
                        {
                            var hist = new EmploymentHistory { FirmName = empOrg };
                            bool empIsCurrent = toDt == "99/9999";

                            if (!string.IsNullOrWhiteSpace(fromDt) && fromDt != "99/9999")
                            {
                                var parts = fromDt.Split('/');
                                if (parts.Length == 2 && int.TryParse(parts[0], out var month) && int.TryParse(parts[1], out var year) && year > 1900)
                                    hist.StartDate = new DateTime(year, Math.Clamp(month, 1, 12), 1);
                            }
                            if (!empIsCurrent && !string.IsNullOrWhiteSpace(toDt))
                            {
                                var parts = toDt.Split('/');
                                if (parts.Length == 2 && int.TryParse(parts[0], out var month) && int.TryParse(parts[1], out var year) && year > 1900)
                                    hist.EndDate = new DateTime(year, Math.Clamp(month, 1, 12), 1);
                            }
                            // IsCurrent is computed as EndDate == null; for empIsCurrent, EndDate stays null

                            if (!advisor.EmploymentHistory.Any(h => h.FirmName == empOrg && h.IsCurrent == empIsCurrent))
                                advisor.EmploymentHistory.Add(hist);
                        }
                        break;
                    }

                    case "DRP":
                    {
                        var flagMap = new (string attr, string label)[]
                        {
                            ("hasRegAction", "Regulatory Action"),
                            ("hasCriminal", "Criminal"),
                            ("hasBankrupt", "Bankruptcy"),
                            ("hasCivilJudc", "Civil Judgment"),
                            ("hasBond", "Bond"),
                            ("hasJudgment", "Judgment"),
                            ("hasInvstgn", "Investigation"),
                            ("hasCustComp", "Customer Complaint"),
                            ("hasTermination", "Termination")
                        };

                        var newFlags = new List<string>();
                        foreach (var (attr, label) in flagMap)
                        {
                            if (reader.GetAttribute(attr) == "Y")
                                newFlags.Add(label);
                        }

                        if (newFlags.Count > 0)
                        {
                            disclosureFlags.AddRange(newFlags.Where(f => !disclosureFlags.Contains(f)));
                            advisor.HasDisclosures = true;
                            advisor.Disclosures.Add(new Disclosure
                            {
                                Type = "DRP Summary",
                                Description = string.Join("; ", newFlags),
                                Source = "SEC"
                            });

                            foreach (var flag in newFlags)
                            {
                                if (flag == "Criminal") advisor.HasCriminalDisclosure = true;
                                else if (flag.Contains("Regulatory") || flag == "Investigation") advisor.HasRegulatoryDisclosure = true;
                                else if (flag.Contains("Civil")) advisor.HasCivilDisclosure = true;
                                else if (flag == "Customer Complaint") advisor.HasCustomerComplaintDisclosure = true;
                                else if (flag == "Bankruptcy" || flag == "Bond" || flag == "Judgment") advisor.HasFinancialDisclosure = true;
                                else if (flag == "Termination") advisor.HasTerminationDisclosure = true;
                            }
                        }
                        break;
                    }

                    case "IndlCntyRgstrtns":
                        // container element; child CntyRgstn elements handled below
                        break;

                    case "CntyRgstn":
                    {
                        var stateCode = reader.GetAttribute("stCd") ?? reader.GetAttribute("state");
                        if (!string.IsNullOrWhiteSpace(stateCode))
                        {
                            registrations.Add(new AdvisorRegistration
                            {
                                StateCode = stateCode,
                                RegistrationCategory = reader.GetAttribute("rgstrtnCtgry") ?? reader.GetAttribute("category"),
                                RegistrationStatus = reader.GetAttribute("st") ?? reader.GetAttribute("status"),
                                StatusDate = reader.GetAttribute("stDt") ?? reader.GetAttribute("statusDate")
                            });
                        }
                        break;
                    }
                }
            }

            advisor.RegAuthorities = regAuthorities.Count > 0 ? string.Join(",", regAuthorities) : null;
            advisor.Licenses = licenses.Count > 0 ? string.Join(", ", licenses) : null;
            advisor.DisclosureFlags = disclosureFlags.Count > 0 ? string.Join(",", disclosureFlags) : null;
            advisor.DisclosureCount = advisor.Disclosures.Count;

            if (registrations.Count > 0)
                advisor.Registrations = registrations;

            var startDates = advisor.EmploymentHistory
                .Where(h => h.StartDate.HasValue)
                .Select(h => h.StartDate!.Value)
                .ToList();
            if (startDates.Count > 0)
                advisor.CareerStartDate = startDates.Min();

            advisor.TotalFirmCount = advisor.EmploymentHistory.Count > 0 ? advisor.EmploymentHistory.Count : (int?)null;
            advisor.BrokerCheckUrl = !string.IsNullOrWhiteSpace(advisor.CrdNumber)
                ? $"https://brokercheck.finra.org/individual/summary/{advisor.CrdNumber}"
                : null;
            advisor.BcDisclosureCount = advisor.Disclosures.Count;

            if (string.IsNullOrWhiteSpace(advisor.CrdNumber) ||
                (string.IsNullOrWhiteSpace(advisor.FirstName) && string.IsNullOrWhiteSpace(advisor.LastName)))
                return null;

            return advisor;
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<Firm>> ParseFirmsXmlAsync(
        string xmlFilePath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var firms = new List<Firm>();

        try
        {
            await Task.Run(() =>
            {
                using var fileStream = new FileStream(xmlFilePath, FileMode.Open, FileAccess.Read);
                using var reader = XmlReader.Create(fileStream, new XmlReaderSettings
                {
                    Async = false,
                    IgnoreWhitespace = true,
                    IgnoreComments = true
                });

                int count = 0;
                int lastReportedCount = 0;

                while (reader.Read())
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "Firm")
                    {
                        var firm = ParseFirmElement(reader.ReadSubtree());
                        if (firm != null)
                        {
                            firms.Add(firm);
                            count++;

                            if (count - lastReportedCount >= 500)
                            {
                                progress?.Report($"SEC: Parsed {count:N0} firms...");
                                lastReportedCount = count;
                            }
                        }
                    }
                }

                progress?.Report($"SEC: Completed parsing {count:N0} firms.");
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            progress?.Report($"SEC: Error parsing firms XML: {ex.Message}");
        }

        return firms;
    }

    private Firm? ParseFirmElement(XmlReader reader)
    {
        try
        {
            var firm = new Firm
            {
                Source = "SEC",
                RecordType = "Investment Advisor",
                IsRegisteredWithSec = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            bool inItem1 = false;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement)
                {
                    if (reader.Name == "Item1") inItem1 = false;
                    continue;
                }

                if (reader.NodeType != XmlNodeType.Element && reader.NodeType != XmlNodeType.Text)
                    continue;

                if (reader.NodeType == XmlNodeType.Text && inItem1)
                {
                    var val = reader.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(val) && string.IsNullOrEmpty(firm.Website))
                        firm.Website = val;
                    continue;
                }

                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                switch (reader.Name)
                {
                    case "Info":
                        firm.CrdNumber = reader.GetAttribute("FirmCrdNb") ?? string.Empty;
                        firm.SECNumber = reader.GetAttribute("SECNb");
                        firm.Name = reader.GetAttribute("BusNm") ?? string.Empty;
                        firm.LegalName = reader.GetAttribute("LegalNm");
                        firm.SECRegion = reader.GetAttribute("SECRgnCD");
                        break;

                    case "MainAddr":
                    {
                        var strt1 = reader.GetAttribute("Strt1") ?? string.Empty;
                        var strt2 = reader.GetAttribute("Strt2") ?? string.Empty;
                        firm.Address = string.IsNullOrWhiteSpace(strt2)
                            ? strt1
                            : string.Join(", ", new[] { strt1, strt2 }.Where(s => !string.IsNullOrWhiteSpace(s)));
                        firm.City = reader.GetAttribute("City");
                        firm.State = reader.GetAttribute("State");
                        firm.ZipCode = reader.GetAttribute("PostlCd");
                        firm.Phone = reader.GetAttribute("PhNb");
                        firm.FaxPhone = reader.GetAttribute("FaxNb");
                        break;
                    }

                    case "MailingAddr":
                    {
                        var mStrt1 = reader.GetAttribute("Strt1") ?? string.Empty;
                        var mStrt2 = reader.GetAttribute("Strt2") ?? string.Empty;
                        var mCity = reader.GetAttribute("City") ?? string.Empty;
                        var mState = reader.GetAttribute("State") ?? string.Empty;
                        var mZip = reader.GetAttribute("PostlCd") ?? string.Empty;
                        var mailParts = new[] { mStrt1, mStrt2, mCity, mState, mZip }
                            .Where(s => !string.IsNullOrWhiteSpace(s));
                        var mailing = string.Join(", ", mailParts);
                        firm.MailingAddress = string.IsNullOrWhiteSpace(mailing) ? null : mailing;
                        break;
                    }

                    case "Rgstn":
                    {
                        var firmType = reader.GetAttribute("FirmType");
                        firm.RegistrationStatus = reader.GetAttribute("St");
                        if (DateTime.TryParse(reader.GetAttribute("Dt"), out var regDt))
                            firm.RegistrationDate = regDt;
                        if (!string.IsNullOrWhiteSpace(firmType) && string.IsNullOrWhiteSpace(firm.BusinessType))
                            firm.BusinessType = firmType;
                        break;
                    }

                    case "Item1":
                        inItem1 = true;
                        firm.AumDescription = reader.GetAttribute("Q1ODesc");
                        break;

                    case "WebAddr":
                        if (!reader.IsEmptyElement)
                        {
                            try
                            {
                                var webVal = reader.ReadElementContentAsString()?.Trim();
                                if (!string.IsNullOrWhiteSpace(webVal) && string.IsNullOrEmpty(firm.Website))
                                    firm.Website = webVal;
                            }
                            catch { }
                        }
                        break;

                    case "Item3A":
                    {
                        var orgForm = reader.GetAttribute("OrgFormNm");
                        if (!string.IsNullOrWhiteSpace(orgForm))
                            firm.BusinessType = orgForm;
                        break;
                    }

                    case "Item3C":
                        firm.StateOfOrganization = reader.GetAttribute("StateCD");
                        break;

                    case "Item5A":
                        if (int.TryParse(reader.GetAttribute("TtlEmp"), out var emp) && emp > 0)
                            firm.NumberOfEmployees = emp;
                        break;

                    case "Item5B":
                        if (int.TryParse(reader.GetAttribute("TtlAdvsrs"), out var advs) && advs > 0)
                            firm.NumberOfAdvisors = advs;
                        else if (int.TryParse(reader.GetAttribute("IAReps"), out var iareps) && iareps > 0)
                            firm.NumberOfAdvisors = iareps;
                        break;

                    case "Item5D":
                        if (int.TryParse(reader.GetAttribute("TtlClnts"), out var clients) && clients > 0)
                            firm.NumClients = clients;
                        break;

                    case "Item5F":
                    {
                        if (decimal.TryParse(reader.GetAttribute("RgltryAUM"),
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var raum) && raum > 0)
                            firm.RegulatoryAum = raum;
                        if (decimal.TryParse(reader.GetAttribute("RgltryAUMNonDisc"),
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var raumnd) && raumnd > 0)
                            firm.RegulatoryAumNonDiscretionary = raumnd;
                        var discAuth = reader.GetAttribute("HasDiscAuth");
                        if (discAuth == "Y") firm.HasDiscretionaryAuthority = true;
                        else if (discAuth == "N") firm.HasDiscretionaryAuthority = false;
                        break;
                    }

                    case "Item9A":
                    {
                        var cust = reader.GetAttribute("HasCustody");
                        if (cust == "Y") firm.HasCustody = true;
                        else if (cust == "N") firm.HasCustody = false;
                        break;
                    }

                    case "Filing":
                    {
                        var filingDt = reader.GetAttribute("Dt");
                        if (!string.IsNullOrWhiteSpace(filingDt))
                            firm.LatestFilingDate = filingDt;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(firm.CrdNumber) || string.IsNullOrWhiteSpace(firm.Name))
                return null;

            return firm;
        }
        catch
        {
            return null;
        }
    }

    private static string ExpandRegistrationStatus(string code) => code switch
    {
        "A" => "Active",
        "I" => "Inactive",
        "T" => "Terminated",
        "B" => "Barred",
        "S" => "Suspended",
        _ => string.IsNullOrWhiteSpace(code) ? "Unknown" : code
    };

    /// <summary>
    /// Clears cached SEC data files to force fresh download.
    /// </summary>
    public void ClearCache()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, recursive: true);
                Directory.CreateDirectory(_cacheDirectory);
            }
        }
        catch
        {
            // Ignore errors when clearing cache
        }
    }
}