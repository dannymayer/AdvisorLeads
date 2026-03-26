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

    // SEC IAPD compilation URLs
    private const string IndividualsDataUrl = "https://adviserinfo.sec.gov/downloads/prod/iapd/compiled/individuals/individuals.xml.gz";
    private const string FirmsDataUrl = "https://adviserinfo.sec.gov/downloads/prod/iapd/compiled/ria/ria.xml.gz";

    public SecCompilationService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30) // Large files can take time
        };

        // Store downloaded files in app data directory
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
            IndividualsDataUrl,
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
            FirmsDataUrl,
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
        CancellationToken cancellationToken)
    {
        try
        {
            var gzipFilePath = Path.Combine(_cacheDirectory, fileName);
            var xmlFilePath = Path.Combine(_cacheDirectory, Path.GetFileNameWithoutExtension(fileName));

            // Download the .gz file
            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                progress?.Report($"SEC: Downloading {fileName} ({totalBytes / (1024 * 1024):N0} MB)...");

                using var fileStream = new FileStream(gzipFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true);
                using var downloadStream = await response.Content.ReadAsStreamAsync(cancellationToken);

                var buffer = new byte[8192];
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
                        if (currentProgress > lastProgress && currentProgress % 10 == 0)
                        {
                            progress?.Report($"SEC: Downloaded {currentProgress}% ({bytesRead / (1024 * 1024):N0} MB)...");
                            lastProgress = currentProgress;
                        }
                    }
                }
            }

            progress?.Report($"SEC: Extracting {fileName}...");

            // Extract the gzip file
            using (var gzipStream = new FileStream(gzipFilePath, FileMode.Open, FileAccess.Read))
            using (var decompressedStream = new GZipStream(gzipStream, CompressionMode.Decompress))
            using (var outputStream = new FileStream(xmlFilePath, FileMode.Create, FileAccess.Write))
            {
                await decompressedStream.CopyToAsync(outputStream, cancellationToken);
            }

            // Delete the .gz file to save space
            File.Delete(gzipFilePath);

            progress?.Report($"SEC: Extraction complete. XML file size: {new FileInfo(xmlFilePath).Length / (1024 * 1024):N0} MB");

            return xmlFilePath;
        }
        catch (Exception ex)
        {
            progress?.Report($"SEC: Error downloading/extracting: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses the individuals XML file and extracts advisor representative data.
    /// The XML structure is based on Form U4 data.
    /// </summary>
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

                    // Look for individual records
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "Individual")
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

    /// <summary>
    /// Parses a single Individual XML element into an Advisor object.
    /// </summary>
    private Advisor? ParseIndividualElement(XmlReader reader)
    {
        try
        {
            var advisor = new Advisor
            {
                Source = "SEC",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                EmploymentHistory = new List<EmploymentHistory>(),
                Disclosures = new List<Disclosure>(),
                QualificationList = new List<Qualification>()
            };

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                switch (reader.Name)
                {
                    case "Info":
                        ParseIndividualInfo(reader.ReadSubtree(), advisor);
                        break;
                    case "CrdInfo":
                        ParseCrdInfo(reader.ReadSubtree(), advisor);
                        break;
                    case "Employments":
                        ParseEmployments(reader.ReadSubtree(), advisor);
                        break;
                    case "DRPs": // Disclosure Reporting Pages
                        ParseDisclosures(reader.ReadSubtree(), advisor);
                        break;
                    case "Exams":
                        ParseExams(reader.ReadSubtree(), advisor);
                        break;
                }
            }

            // Only return if we have at least a CRD number and name
            if (string.IsNullOrWhiteSpace(advisor.CrdNumber) ||
                (string.IsNullOrWhiteSpace(advisor.FirstName) && string.IsNullOrWhiteSpace(advisor.LastName)))
            {
                return null;
            }

            return advisor;
        }
        catch
        {
            return null;
        }
    }

    private void ParseIndividualInfo(XmlReader reader, Advisor advisor)
    {
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
                continue;

            switch (reader.Name)
            {
                case "FirstName":
                    advisor.FirstName = reader.ReadElementContentAsString();
                    break;
                case "MiddleName":
                    advisor.MiddleName = reader.ReadElementContentAsString();
                    break;
                case "LastName":
                    advisor.LastName = reader.ReadElementContentAsString();
                    break;
            }
        }
    }

    private void ParseCrdInfo(XmlReader reader, Advisor advisor)
    {
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
                continue;

            if (reader.Name == "CrdNumber")
            {
                advisor.CrdNumber = reader.ReadElementContentAsString();
            }
        }
    }

    private void ParseEmployments(XmlReader reader, Advisor advisor)
    {
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "Employment")
            {
                var employment = ParseEmployment(reader.ReadSubtree());
                if (employment != null)
                {
                    advisor.EmploymentHistory.Add(employment);

                    // Set current firm if this is the current employment
                    if (employment.IsCurrent)
                    {
                        advisor.CurrentFirmName = employment.FirmName;
                        advisor.CurrentFirmCrd = employment.FirmCrd;
                    }
                }
            }
        }
    }

    private EmploymentHistory? ParseEmployment(XmlReader reader)
    {
        var employment = new EmploymentHistory();

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
                continue;

            switch (reader.Name)
            {
                case "OrgName":
                    employment.FirmName = reader.ReadElementContentAsString();
                    break;
                case "OrgCrdNumber":
                    employment.FirmCrd = reader.ReadElementContentAsString();
                    break;
                case "StartDate":
                    if (DateTime.TryParse(reader.ReadElementContentAsString(), out var startDate))
                        employment.StartDate = startDate;
                    break;
                case "EndDate":
                    var endDateStr = reader.ReadElementContentAsString();
                    if (!string.IsNullOrWhiteSpace(endDateStr) && DateTime.TryParse(endDateStr, out var endDate))
                        employment.EndDate = endDate;
                    break;
                case "Position":
                    employment.Position = reader.ReadElementContentAsString();
                    break;
            }
        }

        return string.IsNullOrWhiteSpace(employment.FirmName) ? null : employment;
    }

    private void ParseDisclosures(XmlReader reader, Advisor advisor)
    {
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "DRP")
            {
                var disclosure = ParseDisclosure(reader.ReadSubtree());
                if (disclosure != null)
                {
                    advisor.Disclosures.Add(disclosure);
                    advisor.HasDisclosures = true;
                }
            }
        }

        advisor.DisclosureCount = advisor.Disclosures.Count;
    }

    private Disclosure? ParseDisclosure(XmlReader reader)
    {
        var disclosure = new Disclosure
        {
            Source = "SEC"
        };

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
                continue;

            switch (reader.Name)
            {
                case "DRPType":
                    disclosure.Type = reader.ReadElementContentAsString();
                    break;
                case "AllegationDesc":
                    disclosure.Description = reader.ReadElementContentAsString();
                    break;
                case "Date":
                    if (DateTime.TryParse(reader.ReadElementContentAsString(), out var date))
                        disclosure.Date = date;
                    break;
                case "Resolution":
                    disclosure.Resolution = reader.ReadElementContentAsString();
                    break;
                case "Sanctions":
                    disclosure.Sanctions = reader.ReadElementContentAsString();
                    break;
            }
        }

        return disclosure;
    }

    private void ParseExams(XmlReader reader, Advisor advisor)
    {
        var licenses = new List<string>();

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "Exam")
            {
                var examSubtree = reader.ReadSubtree();
                while (examSubtree.Read())
                {
                    if (examSubtree.NodeType == XmlNodeType.Element)
                    {
                        switch (examSubtree.Name)
                        {
                            case "ExamName":
                                var examName = examSubtree.ReadElementContentAsString();
                                if (!string.IsNullOrWhiteSpace(examName))
                                {
                                    licenses.Add(examName);

                                    // Also add to qualifications list
                                    var qual = new Qualification
                                    {
                                        Name = examName,
                                        Code = examName
                                    };
                                    advisor.QualificationList.Add(qual);
                                }
                                break;
                            case "ExamDate":
                                if (advisor.QualificationList.Count > 0 &&
                                    DateTime.TryParse(examSubtree.ReadElementContentAsString(), out var examDate))
                                {
                                    advisor.QualificationList.Last().Date = examDate;
                                }
                                break;
                        }
                    }
                }
            }
        }

        if (licenses.Count > 0)
        {
            advisor.Licenses = string.Join(", ", licenses);
        }
    }

    /// <summary>
    /// Parses the firms XML file and extracts investment advisor firm data.
    /// The XML structure is based on Form ADV data.
    /// </summary>
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

                    // Look for firm records (RIA)
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

    /// <summary>
    /// Parses a single Firm XML element into a Firm object.
    /// </summary>
    private Firm? ParseFirmElement(XmlReader reader)
    {
        try
        {
            var firm = new Firm
            {
                Source = "SEC",
                IsRegisteredWithSec = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                switch (reader.Name)
                {
                    case "Info":
                        ParseFirmInfo(reader.ReadSubtree(), firm);
                        break;
                    case "MainAddress":
                        ParseFirmAddress(reader.ReadSubtree(), firm);
                        break;
                }
            }

            // Only return if we have at least a CRD number and name
            if (string.IsNullOrWhiteSpace(firm.CrdNumber) || string.IsNullOrWhiteSpace(firm.Name))
            {
                return null;
            }

            return firm;
        }
        catch
        {
            return null;
        }
    }

    private void ParseFirmInfo(XmlReader reader, Firm firm)
    {
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
                continue;

            switch (reader.Name)
            {
                case "FirmCrdNumber":
                    firm.CrdNumber = reader.ReadElementContentAsString();
                    break;
                case "BusName":
                case "LegalName":
                    var name = reader.ReadElementContentAsString();
                    if (!string.IsNullOrWhiteSpace(name))
                        firm.Name = name;
                    break;
                case "Website":
                    firm.Website = reader.ReadElementContentAsString();
                    break;
                case "RegistrationDate":
                    if (DateTime.TryParse(reader.ReadElementContentAsString(), out var regDate))
                        firm.RegistrationDate = regDate;
                    break;
            }
        }
    }

    private void ParseFirmAddress(XmlReader reader, Firm firm)
    {
        var addressParts = new List<string>();

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
                continue;

            switch (reader.Name)
            {
                case "Street1":
                case "Street2":
                    var street = reader.ReadElementContentAsString();
                    if (!string.IsNullOrWhiteSpace(street))
                        addressParts.Add(street);
                    break;
                case "City":
                    firm.City = reader.ReadElementContentAsString();
                    break;
                case "State":
                    firm.State = reader.ReadElementContentAsString();
                    break;
                case "Country":
                    // Store country in address if not US
                    var country = reader.ReadElementContentAsString();
                    if (!string.IsNullOrWhiteSpace(country) && country != "US" && country != "USA")
                        addressParts.Add(country);
                    break;
                case "PostalCode":
                    firm.ZipCode = reader.ReadElementContentAsString();
                    break;
                case "PhoneNumber":
                    firm.Phone = reader.ReadElementContentAsString();
                    break;
            }
        }

        if (addressParts.Count > 0)
        {
            firm.Address = string.Join(", ", addressParts);
        }
    }

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
