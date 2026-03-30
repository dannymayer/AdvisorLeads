using System.Globalization;
using System.Text.RegularExpressions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;
using Microsoft.EntityFrameworkCore;

namespace AdvisorLeads.Services;

public class DataCleaningService
{
    private readonly string _dbPath;

    private static readonly Dictionary<string, string> StateNameToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alabama"] = "AL", ["Alaska"] = "AK", ["Arizona"] = "AZ", ["Arkansas"] = "AR",
        ["California"] = "CA", ["Colorado"] = "CO", ["Connecticut"] = "CT", ["Delaware"] = "DE",
        ["Florida"] = "FL", ["Georgia"] = "GA", ["Hawaii"] = "HI", ["Idaho"] = "ID",
        ["Illinois"] = "IL", ["Indiana"] = "IN", ["Iowa"] = "IA", ["Kansas"] = "KS",
        ["Kentucky"] = "KY", ["Louisiana"] = "LA", ["Maine"] = "ME", ["Maryland"] = "MD",
        ["Massachusetts"] = "MA", ["Michigan"] = "MI", ["Minnesota"] = "MN", ["Mississippi"] = "MS",
        ["Missouri"] = "MO", ["Montana"] = "MT", ["Nebraska"] = "NE", ["Nevada"] = "NV",
        ["New Hampshire"] = "NH", ["New Jersey"] = "NJ", ["New Mexico"] = "NM", ["New York"] = "NY",
        ["North Carolina"] = "NC", ["North Dakota"] = "ND", ["Ohio"] = "OH", ["Oklahoma"] = "OK",
        ["Oregon"] = "OR", ["Pennsylvania"] = "PA", ["Rhode Island"] = "RI", ["South Carolina"] = "SC",
        ["South Dakota"] = "SD", ["Tennessee"] = "TN", ["Texas"] = "TX", ["Utah"] = "UT",
        ["Vermont"] = "VT", ["Virginia"] = "VA", ["Washington"] = "WA", ["West Virginia"] = "WV",
        ["Wisconsin"] = "WI", ["Wyoming"] = "WY", ["District of Columbia"] = "DC",
        ["Puerto Rico"] = "PR", ["Virgin Islands"] = "VI", ["Guam"] = "GU"
    };

    private static readonly HashSet<string> ValidStateCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AL","AK","AZ","AR","CA","CO","CT","DE","DC","FL","GA","GU","HI","ID","IL","IN",
        "IA","KS","KY","LA","ME","MD","MA","MI","MN","MS","MO","MT","NE","NV","NH","NJ",
        "NM","NY","NC","ND","OH","OK","OR","PA","PR","RI","SC","SD","TN","TX","UT","VT",
        "VA","VI","WA","WV","WI","WY"
    };

    private static readonly HashSet<string> KnownAdvisorStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active", "Inactive", "Terminated", "Barred", "Suspended"
    };

    private static readonly HashSet<string> KnownAdvisorRecordTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Investment Advisor Representative", "Registered Representative"
    };

    public DataCleaningService(string databasePath)
    {
        _dbPath = databasePath;
    }

    private DatabaseContext CreateContext() => new DatabaseContext(_dbPath);

    public async Task<DataQualityReport> AnalyzeAsync(CancellationToken ct, IProgress<string>? progress = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var report = new DataQualityReport { GeneratedAt = DateTime.UtcNow };

        progress?.Report("Counting records...");
        using (var ctx = CreateContext())
        {
            report.TotalAdvisors = await ctx.Advisors.CountAsync(ct);
            report.TotalFirms = await ctx.Firms.CountAsync(ct);
        }

        progress?.Report("Finding duplicate advisors...");
        report.DuplicateAdvisors = await FindDuplicateAdvisorsAsync(ct);
        report.DuplicateAdvisorGroupCount = report.DuplicateAdvisors.Count;

        ct.ThrowIfCancellationRequested();
        progress?.Report("Finding duplicate firms...");
        report.DuplicateFirms = await FindDuplicateFirmsAsync(ct);
        report.DuplicateFirmGroupCount = report.DuplicateFirms.Count;

        ct.ThrowIfCancellationRequested();
        progress?.Report("Finding normalization issues...");
        report.NormalizationIssues = await FindNormalizationIssuesAsync(ct, progress);
        report.NormalizationIssueCount = report.NormalizationIssues.Count;

        ct.ThrowIfCancellationRequested();
        progress?.Report("Finding orphaned records...");
        report.OrphanedRecords = await FindOrphanedRecordsAsync(ct);
        report.OrphanedRecordCount = report.OrphanedRecords.Count;

        ct.ThrowIfCancellationRequested();
        progress?.Report("Finding inconsistent relationships...");
        report.InconsistentRelationships = await FindInconsistentRelationshipsAsync(ct);
        report.InconsistentRelationshipCount = report.InconsistentRelationships.Count;

        sw.Stop();
        report.AnalysisDuration = sw.Elapsed;
        progress?.Report($"Analysis complete: {report.TotalIssues} issues found in {sw.Elapsed.TotalSeconds:F1}s");
        return report;
    }

    // ── Duplicate Detection ───────────────────────────────────────────

    public async Task<List<DuplicateGroup>> FindDuplicateAdvisorsAsync(CancellationToken ct)
    {
        var groups = new List<DuplicateGroup>();
        using var ctx = CreateContext();

        // 1) Name + Firm CRD + State match (different CRD numbers)
        var advisors = await ctx.Advisors.AsNoTracking()
            .Where(a => a.FirstName != "" && a.LastName != "")
            .Select(a => new
            {
                a.Id, a.CrdNumber, a.FirstName, a.LastName,
                a.CurrentFirmCrd, a.State, a.RegistrationDate,
                a.YearsOfExperience, a.HasDisclosures, a.DisclosureCount,
                a.Source, a.Email, a.Phone
            })
            .ToListAsync(ct);

        // Name + Firm + State duplicates
        var nameFirmGroups = advisors
            .Where(a => !string.IsNullOrEmpty(a.CurrentFirmCrd) && !string.IsNullOrEmpty(a.State))
            .GroupBy(a => $"{a.FirstName?.Trim().ToUpperInvariant()}|{a.LastName?.Trim().ToUpperInvariant()}|{a.CurrentFirmCrd}|{a.State}")
            .Where(g => g.Count() > 1 && g.Select(a => a.CrdNumber).Distinct().Count() > 1);

        foreach (var g in nameFirmGroups)
        {
            var best = PickBestAdvisor(g.Select(a => (a.Id, a.Source, a.Email, a.Phone,
                a.YearsOfExperience, a.HasDisclosures, a.DisclosureCount)).ToList());
            groups.Add(new DuplicateGroup
            {
                GroupKey = g.Key,
                Reason = DuplicateReason.NameFirmStateMatch,
                EntityType = CleaningEntityType.Advisor,
                EntityIds = g.Select(a => a.Id).ToList(),
                Names = g.Select(a => $"{a.FirstName} {a.LastName} (CRD: {a.CrdNumber ?? "N/A"})").ToList(),
                SuggestedKeepId = best.Id,
                SuggestedKeepReason = best.Reason
            });
        }

        // 2) Name + State + RegistrationDate match (one record missing CRD)
        var nameRegGroups = advisors
            .Where(a => !string.IsNullOrEmpty(a.State) && a.RegistrationDate.HasValue)
            .GroupBy(a => $"{a.FirstName?.Trim().ToUpperInvariant()}|{a.LastName?.Trim().ToUpperInvariant()}|{a.State}|{a.RegistrationDate:yyyyMMdd}")
            .Where(g => g.Count() > 1 && g.Any(a => string.IsNullOrEmpty(a.CrdNumber)));

        foreach (var g in nameRegGroups)
        {
            if (groups.Any(eg => eg.EntityIds.Intersect(g.Select(a => a.Id)).Count() > 1))
                continue;

            var best = PickBestAdvisor(g.Select(a => (a.Id, a.Source, a.Email, a.Phone,
                a.YearsOfExperience, a.HasDisclosures, a.DisclosureCount)).ToList());
            groups.Add(new DuplicateGroup
            {
                GroupKey = g.Key,
                Reason = DuplicateReason.NameRegistrationDateMatch,
                EntityType = CleaningEntityType.Advisor,
                EntityIds = g.Select(a => a.Id).ToList(),
                Names = g.Select(a => $"{a.FirstName} {a.LastName} (CRD: {a.CrdNumber ?? "N/A"})").ToList(),
                SuggestedKeepId = best.Id,
                SuggestedKeepReason = best.Reason
            });
        }

        return groups;
    }

    private static (int Id, string Reason) PickBestAdvisor(
        List<(int Id, string? Source, string? Email, string? Phone,
              int? YearsOfExperience, bool HasDisclosures, int DisclosureCount)> candidates)
    {
        int bestId = candidates[0].Id;
        int bestScore = 0;
        string bestReason = "First record";

        foreach (var c in candidates)
        {
            int score = 0;
            if (!string.IsNullOrEmpty(c.Email)) score += 3;
            if (!string.IsNullOrEmpty(c.Phone)) score += 2;
            if (c.YearsOfExperience.HasValue && c.YearsOfExperience > 0) score += 1;
            if (c.Source?.Contains("FINRA") == true) score += 1;
            if (c.Source?.Contains("SEC") == true) score += 1;
            if (c.HasDisclosures) score += 1;

            if (score > bestScore)
            {
                bestScore = score;
                bestId = c.Id;
                var reasons = new List<string>();
                if (!string.IsNullOrEmpty(c.Email)) reasons.Add("has email");
                if (!string.IsNullOrEmpty(c.Phone)) reasons.Add("has phone");
                if (c.Source?.Contains(',') == true) reasons.Add("multiple sources");
                bestReason = reasons.Count > 0 ? "Most complete: " + string.Join(", ", reasons) : "Highest data quality score";
            }
        }

        return (bestId, bestReason);
    }

    public async Task<List<DuplicateGroup>> FindDuplicateFirmsAsync(CancellationToken ct)
    {
        var groups = new List<DuplicateGroup>();
        using var ctx = CreateContext();

        var firms = await ctx.Firms.AsNoTracking()
            .Select(f => new { f.Id, f.CrdNumber, f.Name, f.State, f.RegistrationDate, f.RegulatoryAum, f.NumberOfAdvisors })
            .ToListAsync(ct);

        // Same Name + State + RegistrationDate, different CRD
        var nameDups = firms
            .Where(f => !string.IsNullOrEmpty(f.Name) && !string.IsNullOrEmpty(f.State) && f.RegistrationDate.HasValue)
            .GroupBy(f => $"{f.Name.Trim().ToUpperInvariant()}|{f.State}|{f.RegistrationDate:yyyyMMdd}")
            .Where(g => g.Count() > 1 && g.Select(f => f.CrdNumber).Distinct().Count() > 1);

        foreach (var g in nameDups)
        {
            var best = g.OrderByDescending(f => f.RegulatoryAum ?? 0)
                        .ThenByDescending(f => f.NumberOfAdvisors ?? 0)
                        .First();
            groups.Add(new DuplicateGroup
            {
                GroupKey = g.Key,
                Reason = DuplicateReason.NameRegistrationDateMatch,
                EntityType = CleaningEntityType.Firm,
                EntityIds = g.Select(f => f.Id).ToList(),
                Names = g.Select(f => $"{f.Name} (CRD: {f.CrdNumber})").ToList(),
                SuggestedKeepId = best.Id,
                SuggestedKeepReason = "Highest AUM / most advisors"
            });
        }

        return groups;
    }

    // ── Normalization Issues ──────────────────────────────────────────

    public async Task<List<CleaningIssue>> FindNormalizationIssuesAsync(CancellationToken ct, IProgress<string>? progress = null)
    {
        var issues = new List<CleaningIssue>();

        progress?.Report("Checking advisor fields...");
        await FindAdvisorNormalizationIssuesAsync(issues, ct);

        ct.ThrowIfCancellationRequested();
        progress?.Report("Checking firm fields...");
        await FindFirmNormalizationIssuesAsync(issues, ct);

        return issues;
    }

    private async Task FindAdvisorNormalizationIssuesAsync(List<CleaningIssue> issues, CancellationToken ct)
    {
        using var ctx = CreateContext();
        var advisors = await ctx.Advisors.AsNoTracking()
            .Select(a => new
            {
                a.Id, a.FirstName, a.LastName, a.State, a.Phone, a.Email,
                a.CrdNumber, a.RegistrationStatus, a.RecordType,
                a.YearsOfExperience, a.RegistrationDate
            })
            .ToListAsync(ct);

        foreach (var a in advisors)
        {
            string name = $"{a.FirstName} {a.LastName}".Trim();
            if (string.IsNullOrEmpty(name)) name = $"Advisor #{a.Id}";

            // State normalization
            if (!string.IsNullOrEmpty(a.State))
            {
                var normalized = NormalizeState(a.State);
                if (normalized != null && normalized != a.State)
                {
                    issues.Add(new CleaningIssue
                    {
                        Type = CleaningIssueType.NormalizationNeeded,
                        EntityType = CleaningEntityType.Advisor,
                        EntityId = a.Id,
                        EntityName = name,
                        FieldName = "State",
                        CurrentValue = a.State,
                        SuggestedValue = normalized,
                        Description = $"State should be 2-letter code: '{a.State}' → '{normalized}'",
                        Severity = CleaningIssueSeverity.Low,
                        IsAutoFixable = true
                    });
                }
                else if (normalized == null && a.State.Length != 2)
                {
                    issues.Add(new CleaningIssue
                    {
                        Type = CleaningIssueType.UnknownEnumValue,
                        EntityType = CleaningEntityType.Advisor,
                        EntityId = a.Id,
                        EntityName = name,
                        FieldName = "State",
                        CurrentValue = a.State,
                        SuggestedValue = "",
                        Description = $"Unrecognized state value: '{a.State}'",
                        Severity = CleaningIssueSeverity.Medium,
                        IsAutoFixable = false
                    });
                }
            }

            // Name casing — detect ALL CAPS
            if (IsAllCaps(a.FirstName) && a.FirstName.Length > 1)
            {
                issues.Add(new CleaningIssue
                {
                    Type = CleaningIssueType.NormalizationNeeded,
                    EntityType = CleaningEntityType.Advisor,
                    EntityId = a.Id,
                    EntityName = name,
                    FieldName = "FirstName",
                    CurrentValue = a.FirstName,
                    SuggestedValue = ToTitleCase(a.FirstName),
                    Description = $"ALL CAPS first name: '{a.FirstName}' → '{ToTitleCase(a.FirstName)}'",
                    Severity = CleaningIssueSeverity.Low,
                    IsAutoFixable = true
                });
            }
            if (IsAllCaps(a.LastName) && a.LastName.Length > 1)
            {
                issues.Add(new CleaningIssue
                {
                    Type = CleaningIssueType.NormalizationNeeded,
                    EntityType = CleaningEntityType.Advisor,
                    EntityId = a.Id,
                    EntityName = name,
                    FieldName = "LastName",
                    CurrentValue = a.LastName,
                    SuggestedValue = ToTitleCase(a.LastName),
                    Description = $"ALL CAPS last name: '{a.LastName}' → '{ToTitleCase(a.LastName)}'",
                    Severity = CleaningIssueSeverity.Low,
                    IsAutoFixable = true
                });
            }

            // Phone normalization
            if (!string.IsNullOrEmpty(a.Phone))
            {
                var normalizedPhone = NormalizePhone(a.Phone);
                if (normalizedPhone != null && normalizedPhone != a.Phone)
                {
                    issues.Add(new CleaningIssue
                    {
                        Type = CleaningIssueType.NormalizationNeeded,
                        EntityType = CleaningEntityType.Advisor,
                        EntityId = a.Id,
                        EntityName = name,
                        FieldName = "Phone",
                        CurrentValue = a.Phone,
                        SuggestedValue = normalizedPhone,
                        Description = $"Phone format: '{a.Phone}' → '{normalizedPhone}'",
                        Severity = CleaningIssueSeverity.Low,
                        IsAutoFixable = true
                    });
                }
            }

            // Email normalization
            if (!string.IsNullOrEmpty(a.Email))
            {
                var normalizedEmail = a.Email.Trim().ToLowerInvariant();
                if (normalizedEmail != a.Email)
                {
                    issues.Add(new CleaningIssue
                    {
                        Type = CleaningIssueType.NormalizationNeeded,
                        EntityType = CleaningEntityType.Advisor,
                        EntityId = a.Id,
                        EntityName = name,
                        FieldName = "Email",
                        CurrentValue = a.Email,
                        SuggestedValue = normalizedEmail,
                        Description = $"Email should be lowercase/trimmed",
                        Severity = CleaningIssueSeverity.Low,
                        IsAutoFixable = true
                    });
                }
            }

            // CRD leading zeros
            if (!string.IsNullOrEmpty(a.CrdNumber) && a.CrdNumber.StartsWith('0') && a.CrdNumber.Length > 1)
            {
                var stripped = a.CrdNumber.TrimStart('0');
                if (!string.IsNullOrEmpty(stripped))
                {
                    issues.Add(new CleaningIssue
                    {
                        Type = CleaningIssueType.NormalizationNeeded,
                        EntityType = CleaningEntityType.Advisor,
                        EntityId = a.Id,
                        EntityName = name,
                        FieldName = "CrdNumber",
                        CurrentValue = a.CrdNumber,
                        SuggestedValue = stripped,
                        Description = $"CRD has leading zeros: '{a.CrdNumber}' → '{stripped}'",
                        Severity = CleaningIssueSeverity.Medium,
                        IsAutoFixable = true
                    });
                }
            }

            // RegistrationStatus unknown
            if (!string.IsNullOrEmpty(a.RegistrationStatus) && !KnownAdvisorStatuses.Contains(a.RegistrationStatus))
            {
                issues.Add(new CleaningIssue
                {
                    Type = CleaningIssueType.UnknownEnumValue,
                    EntityType = CleaningEntityType.Advisor,
                    EntityId = a.Id,
                    EntityName = name,
                    FieldName = "RegistrationStatus",
                    CurrentValue = a.RegistrationStatus,
                    SuggestedValue = "",
                    Description = $"Unknown registration status: '{a.RegistrationStatus}'",
                    Severity = CleaningIssueSeverity.Medium,
                    IsAutoFixable = false
                });
            }

            // RecordType unknown
            if (!string.IsNullOrEmpty(a.RecordType) && !KnownAdvisorRecordTypes.Contains(a.RecordType))
            {
                issues.Add(new CleaningIssue
                {
                    Type = CleaningIssueType.UnknownEnumValue,
                    EntityType = CleaningEntityType.Advisor,
                    EntityId = a.Id,
                    EntityName = name,
                    FieldName = "RecordType",
                    CurrentValue = a.RecordType,
                    SuggestedValue = "",
                    Description = $"Unknown record type: '{a.RecordType}'",
                    Severity = CleaningIssueSeverity.Medium,
                    IsAutoFixable = false
                });
            }

            // YearsOfExperience implausible
            if (a.YearsOfExperience.HasValue && (a.YearsOfExperience < 0 || a.YearsOfExperience > 60))
            {
                issues.Add(new CleaningIssue
                {
                    Type = CleaningIssueType.ImplausibleValue,
                    EntityType = CleaningEntityType.Advisor,
                    EntityId = a.Id,
                    EntityName = name,
                    FieldName = "YearsOfExperience",
                    CurrentValue = a.YearsOfExperience.Value.ToString(),
                    SuggestedValue = "",
                    Description = a.YearsOfExperience < 0
                        ? "Negative years of experience"
                        : $"Implausibly high years of experience: {a.YearsOfExperience}",
                    Severity = CleaningIssueSeverity.Medium,
                    IsAutoFixable = false
                });
            }

            // RegistrationDate implausible
            if (a.RegistrationDate.HasValue)
            {
                if (a.RegistrationDate.Value > DateTime.UtcNow)
                {
                    issues.Add(new CleaningIssue
                    {
                        Type = CleaningIssueType.ImplausibleValue,
                        EntityType = CleaningEntityType.Advisor,
                        EntityId = a.Id,
                        EntityName = name,
                        FieldName = "RegistrationDate",
                        CurrentValue = a.RegistrationDate.Value.ToString("yyyy-MM-dd"),
                        SuggestedValue = "",
                        Description = $"Registration date is in the future: {a.RegistrationDate:yyyy-MM-dd}",
                        Severity = CleaningIssueSeverity.High,
                        IsAutoFixable = false
                    });
                }
                else if (a.RegistrationDate.Value.Year < 1940)
                {
                    issues.Add(new CleaningIssue
                    {
                        Type = CleaningIssueType.ImplausibleValue,
                        EntityType = CleaningEntityType.Advisor,
                        EntityId = a.Id,
                        EntityName = name,
                        FieldName = "RegistrationDate",
                        CurrentValue = a.RegistrationDate.Value.ToString("yyyy-MM-dd"),
                        SuggestedValue = "",
                        Description = $"Registration date before 1940: {a.RegistrationDate:yyyy-MM-dd}",
                        Severity = CleaningIssueSeverity.Medium,
                        IsAutoFixable = false
                    });
                }
            }

            // Missing name (ghost advisor)
            if (string.IsNullOrWhiteSpace(a.FirstName) && string.IsNullOrWhiteSpace(a.LastName))
            {
                issues.Add(new CleaningIssue
                {
                    Type = CleaningIssueType.MissingRequiredField,
                    EntityType = CleaningEntityType.Advisor,
                    EntityId = a.Id,
                    EntityName = $"Advisor #{a.Id}",
                    FieldName = "FirstName/LastName",
                    CurrentValue = "",
                    SuggestedValue = "",
                    Description = "Advisor has no first or last name",
                    Severity = CleaningIssueSeverity.High,
                    IsAutoFixable = false
                });
            }
        }
    }

    private async Task FindFirmNormalizationIssuesAsync(List<CleaningIssue> issues, CancellationToken ct)
    {
        using var ctx = CreateContext();
        var firms = await ctx.Firms.AsNoTracking()
            .Select(f => new
            {
                f.Id, f.Name, f.State, f.Phone, f.FaxPhone, f.Website,
                f.ZipCode, f.RegulatoryAum, f.NumberOfAdvisors,
                f.RegistrationStatus, f.RegistrationDate, f.CrdNumber
            })
            .ToListAsync(ct);

        foreach (var f in firms)
        {
            string name = string.IsNullOrEmpty(f.Name) ? $"Firm #{f.Id}" : f.Name;

            // State normalization
            if (!string.IsNullOrEmpty(f.State))
            {
                var normalized = NormalizeState(f.State);
                if (normalized != null && normalized != f.State)
                {
                    issues.Add(new CleaningIssue
                    {
                        Type = CleaningIssueType.NormalizationNeeded,
                        EntityType = CleaningEntityType.Firm,
                        EntityId = f.Id,
                        EntityName = name,
                        FieldName = "State",
                        CurrentValue = f.State,
                        SuggestedValue = normalized,
                        Description = $"State should be 2-letter code: '{f.State}' → '{normalized}'",
                        Severity = CleaningIssueSeverity.Low,
                        IsAutoFixable = true
                    });
                }
            }

            // Phone normalization
            if (!string.IsNullOrEmpty(f.Phone))
            {
                var normalizedPhone = NormalizePhone(f.Phone);
                if (normalizedPhone != null && normalizedPhone != f.Phone)
                {
                    issues.Add(new CleaningIssue
                    {
                        Type = CleaningIssueType.NormalizationNeeded,
                        EntityType = CleaningEntityType.Firm,
                        EntityId = f.Id,
                        EntityName = name,
                        FieldName = "Phone",
                        CurrentValue = f.Phone,
                        SuggestedValue = normalizedPhone,
                        Description = $"Phone format: '{f.Phone}' → '{normalizedPhone}'",
                        Severity = CleaningIssueSeverity.Low,
                        IsAutoFixable = true
                    });
                }
            }

            // Fax normalization
            if (!string.IsNullOrEmpty(f.FaxPhone))
            {
                var normalizedFax = NormalizePhone(f.FaxPhone);
                if (normalizedFax != null && normalizedFax != f.FaxPhone)
                {
                    issues.Add(new CleaningIssue
                    {
                        Type = CleaningIssueType.NormalizationNeeded,
                        EntityType = CleaningEntityType.Firm,
                        EntityId = f.Id,
                        EntityName = name,
                        FieldName = "FaxPhone",
                        CurrentValue = f.FaxPhone,
                        SuggestedValue = normalizedFax,
                        Description = $"Fax format: '{f.FaxPhone}' → '{normalizedFax}'",
                        Severity = CleaningIssueSeverity.Low,
                        IsAutoFixable = true
                    });
                }
            }

            // Firm name ALL CAPS
            if (IsAllCaps(f.Name) && f.Name.Length > 2)
            {
                issues.Add(new CleaningIssue
                {
                    Type = CleaningIssueType.NormalizationNeeded,
                    EntityType = CleaningEntityType.Firm,
                    EntityId = f.Id,
                    EntityName = name,
                    FieldName = "Name",
                    CurrentValue = f.Name,
                    SuggestedValue = ToTitleCase(f.Name),
                    Description = $"ALL CAPS firm name: '{f.Name}' → '{ToTitleCase(f.Name)}'",
                    Severity = CleaningIssueSeverity.Low,
                    IsAutoFixable = true
                });
            }

            // Website normalization
            if (!string.IsNullOrEmpty(f.Website))
            {
                var normalizedUrl = NormalizeWebsite(f.Website);
                if (normalizedUrl != f.Website)
                {
                    issues.Add(new CleaningIssue
                    {
                        Type = CleaningIssueType.NormalizationNeeded,
                        EntityType = CleaningEntityType.Firm,
                        EntityId = f.Id,
                        EntityName = name,
                        FieldName = "Website",
                        CurrentValue = f.Website,
                        SuggestedValue = normalizedUrl,
                        Description = $"Website URL normalization",
                        Severity = CleaningIssueSeverity.Low,
                        IsAutoFixable = true
                    });
                }
            }

            // ZipCode format
            if (!string.IsNullOrEmpty(f.ZipCode))
            {
                var normalizedZip = NormalizeZipCode(f.ZipCode);
                if (normalizedZip != null && normalizedZip != f.ZipCode)
                {
                    issues.Add(new CleaningIssue
                    {
                        Type = CleaningIssueType.NormalizationNeeded,
                        EntityType = CleaningEntityType.Firm,
                        EntityId = f.Id,
                        EntityName = name,
                        FieldName = "ZipCode",
                        CurrentValue = f.ZipCode,
                        SuggestedValue = normalizedZip,
                        Description = $"ZIP code format: '{f.ZipCode}' → '{normalizedZip}'",
                        Severity = CleaningIssueSeverity.Low,
                        IsAutoFixable = true
                    });
                }
            }

            // AUM implausible
            if (f.RegulatoryAum.HasValue && (f.RegulatoryAum < 0 || f.RegulatoryAum > 100_000_000_000_000m))
            {
                issues.Add(new CleaningIssue
                {
                    Type = CleaningIssueType.ImplausibleValue,
                    EntityType = CleaningEntityType.Firm,
                    EntityId = f.Id,
                    EntityName = name,
                    FieldName = "RegulatoryAum",
                    CurrentValue = f.RegulatoryAum.Value.ToString("N0"),
                    SuggestedValue = "",
                    Description = f.RegulatoryAum < 0
                        ? "Negative AUM value"
                        : $"AUM exceeds $100 trillion: {f.RegulatoryAum:N0}",
                    Severity = CleaningIssueSeverity.High,
                    IsAutoFixable = false
                });
            }

            // Null/empty CRD on firm
            if (string.IsNullOrEmpty(f.CrdNumber))
            {
                issues.Add(new CleaningIssue
                {
                    Type = CleaningIssueType.MissingRequiredField,
                    EntityType = CleaningEntityType.Firm,
                    EntityId = f.Id,
                    EntityName = name,
                    FieldName = "CrdNumber",
                    CurrentValue = "",
                    SuggestedValue = "",
                    Description = "Firm has no CRD number",
                    Severity = CleaningIssueSeverity.High,
                    IsAutoFixable = false
                });
            }
        }
    }

    // ── Orphaned Records ──────────────────────────────────────────────

    public async Task<List<CleaningIssue>> FindOrphanedRecordsAsync(CancellationToken ct)
    {
        var issues = new List<CleaningIssue>();
        using var ctx = CreateContext();

        var advisorIds = await ctx.Advisors.Select(a => a.Id).ToListAsync(ct);
        var advisorIdSet = new HashSet<int>(advisorIds);

        // Orphaned EmploymentHistory
        var orphanedEh = await ctx.EmploymentHistory.AsNoTracking()
            .Where(eh => !ctx.Advisors.Any(a => a.Id == eh.AdvisorId))
            .Select(eh => new { eh.Id, eh.AdvisorId, eh.FirmName })
            .ToListAsync(ct);

        foreach (var eh in orphanedEh)
        {
            issues.Add(new CleaningIssue
            {
                Type = CleaningIssueType.OrphanedRecord,
                EntityType = CleaningEntityType.EmploymentHistory,
                EntityId = eh.Id,
                EntityName = eh.FirmName,
                FieldName = "AdvisorId",
                CurrentValue = eh.AdvisorId.ToString(),
                Description = $"Employment history for non-existent Advisor #{eh.AdvisorId}",
                Severity = CleaningIssueSeverity.High,
                IsAutoFixable = true
            });
        }

        // Orphaned Disclosures
        var orphanedDisc = await ctx.Disclosures.AsNoTracking()
            .Where(d => !ctx.Advisors.Any(a => a.Id == d.AdvisorId))
            .Select(d => new { d.Id, d.AdvisorId, d.Type })
            .ToListAsync(ct);

        foreach (var d in orphanedDisc)
        {
            issues.Add(new CleaningIssue
            {
                Type = CleaningIssueType.OrphanedRecord,
                EntityType = CleaningEntityType.Disclosure,
                EntityId = d.Id,
                EntityName = d.Type,
                FieldName = "AdvisorId",
                CurrentValue = d.AdvisorId.ToString(),
                Description = $"Disclosure for non-existent Advisor #{d.AdvisorId}",
                Severity = CleaningIssueSeverity.High,
                IsAutoFixable = true
            });
        }

        // Orphaned Qualifications
        var orphanedQual = await ctx.Qualifications.AsNoTracking()
            .Where(q => !ctx.Advisors.Any(a => a.Id == q.AdvisorId))
            .Select(q => new { q.Id, q.AdvisorId, q.Name })
            .ToListAsync(ct);

        foreach (var q in orphanedQual)
        {
            issues.Add(new CleaningIssue
            {
                Type = CleaningIssueType.OrphanedRecord,
                EntityType = CleaningEntityType.Qualification,
                EntityId = q.Id,
                EntityName = q.Name,
                FieldName = "AdvisorId",
                CurrentValue = q.AdvisorId.ToString(),
                Description = $"Qualification for non-existent Advisor #{q.AdvisorId}",
                Severity = CleaningIssueSeverity.High,
                IsAutoFixable = true
            });
        }

        // Orphaned AdvisorListMembers
        var orphanedMembers = await ctx.AdvisorListMembers.AsNoTracking()
            .Where(m => !ctx.Advisors.Any(a => a.Id == m.AdvisorId)
                      || !ctx.AdvisorLists.Any(l => l.Id == m.ListId))
            .Select(m => new { m.Id, m.AdvisorId, m.ListId })
            .ToListAsync(ct);

        foreach (var m in orphanedMembers)
        {
            issues.Add(new CleaningIssue
            {
                Type = CleaningIssueType.OrphanedRecord,
                EntityType = CleaningEntityType.AdvisorListMember,
                EntityId = m.Id,
                EntityName = $"List #{m.ListId} / Advisor #{m.AdvisorId}",
                FieldName = "AdvisorId/ListId",
                CurrentValue = $"AdvisorId={m.AdvisorId}, ListId={m.ListId}",
                Description = $"List member referencing non-existent advisor or list",
                Severity = CleaningIssueSeverity.High,
                IsAutoFixable = true
            });
        }

        return issues;
    }

    // ── Inconsistent Relationships ────────────────────────────────────

    public async Task<List<CleaningIssue>> FindInconsistentRelationshipsAsync(CancellationToken ct)
    {
        var issues = new List<CleaningIssue>();
        using var ctx = CreateContext();

        // Advisors referencing non-existent firm CRD
        var firmCrds = await ctx.Firms.AsNoTracking()
            .Select(f => f.CrdNumber)
            .ToListAsync(ct);
        var firmCrdSet = new HashSet<string>(firmCrds, StringComparer.OrdinalIgnoreCase);

        var advisorsWithFirmCrd = await ctx.Advisors.AsNoTracking()
            .Where(a => a.CurrentFirmCrd != null && a.CurrentFirmCrd != "")
            .Select(a => new { a.Id, a.FirstName, a.LastName, a.CurrentFirmCrd, a.CurrentFirmName })
            .ToListAsync(ct);

        foreach (var a in advisorsWithFirmCrd)
        {
            if (!firmCrdSet.Contains(a.CurrentFirmCrd!))
            {
                issues.Add(new CleaningIssue
                {
                    Type = CleaningIssueType.InconsistentRelationship,
                    EntityType = CleaningEntityType.Advisor,
                    EntityId = a.Id,
                    EntityName = $"{a.FirstName} {a.LastName}".Trim(),
                    FieldName = "CurrentFirmCrd",
                    CurrentValue = a.CurrentFirmCrd!,
                    Description = $"Advisor references firm CRD '{a.CurrentFirmCrd}' which does not exist in Firms table",
                    Severity = CleaningIssueSeverity.Medium,
                    IsAutoFixable = false
                });
            }
        }

        // Advisor firm name mismatch
        var firmNameByCrd = await ctx.Firms.AsNoTracking()
            .Where(f => f.CrdNumber != null && f.CrdNumber != "")
            .ToDictionaryAsync(f => f.CrdNumber, f => f.Name, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var a in advisorsWithFirmCrd)
        {
            if (a.CurrentFirmCrd != null && firmNameByCrd.TryGetValue(a.CurrentFirmCrd, out var firmName))
            {
                if (!string.IsNullOrEmpty(a.CurrentFirmName) && !string.IsNullOrEmpty(firmName)
                    && !string.Equals(a.CurrentFirmName.Trim(), firmName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new CleaningIssue
                    {
                        Type = CleaningIssueType.InconsistentRelationship,
                        EntityType = CleaningEntityType.Advisor,
                        EntityId = a.Id,
                        EntityName = $"{a.FirstName} {a.LastName}".Trim(),
                        FieldName = "CurrentFirmName",
                        CurrentValue = a.CurrentFirmName,
                        SuggestedValue = firmName,
                        Description = $"Firm name mismatch: advisor has '{a.CurrentFirmName}' but firm CRD {a.CurrentFirmCrd} is '{firmName}'",
                        Severity = CleaningIssueSeverity.Low,
                        IsAutoFixable = true
                    });
                }
            }
        }

        return issues;
    }

    // ── Fix Methods ───────────────────────────────────────────────────

    public async Task<int> ApplyNormalizationsAsync(IEnumerable<CleaningIssue> issues, CancellationToken ct, IProgress<string>? progress = null)
    {
        int fixed_ = 0;
        using var ctx = CreateContext();

        foreach (var issue in issues)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(issue.SuggestedValue)) continue;

            if (issue.EntityType == CleaningEntityType.Advisor)
            {
                var advisor = await ctx.Advisors.FirstOrDefaultAsync(a => a.Id == issue.EntityId, ct);
                if (advisor == null) continue;

                SetAdvisorField(advisor, issue.FieldName, issue.SuggestedValue);
                advisor.UpdatedAt = DateTime.UtcNow;
            }
            else if (issue.EntityType == CleaningEntityType.Firm)
            {
                var firm = await ctx.Firms.FirstOrDefaultAsync(f => f.Id == issue.EntityId, ct);
                if (firm == null) continue;

                SetFirmField(firm, issue.FieldName, issue.SuggestedValue);
                firm.UpdatedAt = DateTime.UtcNow;
            }

            fixed_++;
            if (fixed_ % 100 == 0)
                progress?.Report($"Applied {fixed_} normalizations...");
        }

        await ctx.SaveChangesAsync(ct);
        progress?.Report($"Applied {fixed_} normalizations.");
        return fixed_;
    }

    public async Task<int> DeleteOrphanedRecordsAsync(IEnumerable<CleaningIssue> issues, CancellationToken ct)
    {
        int deleted = 0;
        using var ctx = CreateContext();

        foreach (var issue in issues)
        {
            ct.ThrowIfCancellationRequested();
            if (issue.Type != CleaningIssueType.OrphanedRecord) continue;

            switch (issue.EntityType)
            {
                case CleaningEntityType.EmploymentHistory:
                    deleted += await ctx.EmploymentHistory.Where(e => e.Id == issue.EntityId).ExecuteDeleteAsync(ct);
                    break;
                case CleaningEntityType.Disclosure:
                    deleted += await ctx.Disclosures.Where(d => d.Id == issue.EntityId).ExecuteDeleteAsync(ct);
                    break;
                case CleaningEntityType.Qualification:
                    deleted += await ctx.Qualifications.Where(q => q.Id == issue.EntityId).ExecuteDeleteAsync(ct);
                    break;
                case CleaningEntityType.AdvisorListMember:
                    deleted += await ctx.AdvisorListMembers.Where(m => m.Id == issue.EntityId).ExecuteDeleteAsync(ct);
                    break;
            }
        }

        return deleted;
    }

    public async Task MergeDuplicateAdvisorsAsync(DuplicateGroup group, int keepId, CancellationToken ct)
    {
        using var ctx = CreateContext();
        var deleteIds = group.EntityIds.Where(id => id != keepId).ToList();

        foreach (var deleteId in deleteIds)
        {
            // Move employment history
            await ctx.EmploymentHistory
                .Where(eh => eh.AdvisorId == deleteId)
                .ExecuteUpdateAsync(s => s.SetProperty(eh => eh.AdvisorId, keepId), ct);

            // Move disclosures
            await ctx.Disclosures
                .Where(d => d.AdvisorId == deleteId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.AdvisorId, keepId), ct);

            // Move qualifications
            await ctx.Qualifications
                .Where(q => q.AdvisorId == deleteId)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.AdvisorId, keepId), ct);

            // Move list memberships (delete if would create duplicates)
            var keepMemberListIds = await ctx.AdvisorListMembers
                .Where(m => m.AdvisorId == keepId)
                .Select(m => m.ListId)
                .ToListAsync(ct);

            await ctx.AdvisorListMembers
                .Where(m => m.AdvisorId == deleteId && keepMemberListIds.Contains(m.ListId))
                .ExecuteDeleteAsync(ct);

            await ctx.AdvisorListMembers
                .Where(m => m.AdvisorId == deleteId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.AdvisorId, keepId), ct);

            // Move advisor registrations; delete duplicates to avoid violating the
            // unique constraint on (AdvisorId, StateCode, RegistrationCategory) that
            // exists in pre-migration-2 databases.
            var keepRegKeys = await ctx.AdvisorRegistrations
                .Where(r => r.AdvisorId == keepId)
                .Select(r => new { r.StateCode, r.RegistrationCategory })
                .ToListAsync(ct);

            foreach (var key in keepRegKeys)
            {
                string sc = key.StateCode;
                string? rc = key.RegistrationCategory;
                await ctx.AdvisorRegistrations
                    .Where(r => r.AdvisorId == deleteId && r.StateCode == sc && r.RegistrationCategory == rc)
                    .ExecuteDeleteAsync(ct);
            }

            await ctx.AdvisorRegistrations
                .Where(r => r.AdvisorId == deleteId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.AdvisorId, keepId), ct);

            // Delete the duplicate advisor
            await ctx.Advisors
                .Where(a => a.Id == deleteId)
                .ExecuteDeleteAsync(ct);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static string? NormalizeState(string state)
    {
        if (string.IsNullOrWhiteSpace(state)) return null;
        var trimmed = state.Trim();

        if (trimmed.Length == 2)
        {
            var upper = trimmed.ToUpperInvariant();
            return ValidStateCodes.Contains(upper) ? upper : null;
        }

        return StateNameToCode.TryGetValue(trimmed, out var code) ? code : null;
    }

    private static string? NormalizePhone(string phone)
    {
        var digits = Regex.Replace(phone, @"\D", "");
        if (digits.Length == 11 && digits.StartsWith('1'))
            digits = digits[1..];

        if (digits.Length == 10)
        {
            var formatted = $"({digits[..3]}) {digits[3..6]}-{digits[6..]}";
            return formatted;
        }

        return null;
    }

    private static bool IsAllCaps(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var letters = value.Where(char.IsLetter).ToList();
        return letters.Count > 0 && letters.All(char.IsUpper);
    }

    private static string ToTitleCase(string value)
    {
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
    }

    private static string NormalizeWebsite(string url)
    {
        var trimmed = url.Trim().TrimEnd('/');
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "https://" + trimmed;
        }
        return trimmed;
    }

    private static string? NormalizeZipCode(string zip)
    {
        var digits = Regex.Replace(zip.Trim(), @"\D", "");
        if (digits.Length >= 5)
            return digits[..5];
        return null;
    }

    private static void SetAdvisorField(Advisor advisor, string fieldName, string value)
    {
        switch (fieldName)
        {
            case "FirstName": advisor.FirstName = value; break;
            case "LastName": advisor.LastName = value; break;
            case "State": advisor.State = value; break;
            case "Phone": advisor.Phone = value; break;
            case "Email": advisor.Email = value; break;
            case "CrdNumber": advisor.CrdNumber = value; break;
            case "CurrentFirmName": advisor.CurrentFirmName = value; break;
            case "RecordType": advisor.RecordType = value; break;
            case "RegistrationStatus": advisor.RegistrationStatus = value; break;
        }
    }

    private static void SetFirmField(Firm firm, string fieldName, string value)
    {
        switch (fieldName)
        {
            case "Name": firm.Name = value; break;
            case "State": firm.State = value; break;
            case "Phone": firm.Phone = value; break;
            case "FaxPhone": firm.FaxPhone = value; break;
            case "Website": firm.Website = value; break;
            case "ZipCode": firm.ZipCode = value; break;
        }
    }
}
