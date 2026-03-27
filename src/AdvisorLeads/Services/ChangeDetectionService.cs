using AdvisorLeads.Data;
using AdvisorLeads.Models;
using Microsoft.EntityFrameworkCore;

namespace AdvisorLeads.Services;

/// <summary>
/// Detects significant changes in firm data between monthly SEC data updates.
/// Compares incoming firm records against the database and generates events
/// for AUM changes, status changes, employee shifts, and other M&A signals.
/// </summary>
public class ChangeDetectionService
{
    private readonly string _dbPath;

    public ChangeDetectionService(string databasePath)
    {
        _dbPath = databasePath;
    }

    private DatabaseContext CreateContext() => new DatabaseContext(_dbPath);

    /// <summary>
    /// Compares a batch of incoming firm records against stored values and records events.
    /// Call this BEFORE upserting the new firm data so we can diff old vs new.
    /// Returns the number of events generated.
    /// </summary>
    public int DetectChanges(List<Firm> incomingFirms, IProgress<string>? progress = null)
    {
        using var ctx = CreateContext();
        var events = new List<FirmFilingEvent>();
        var now = DateTime.UtcNow;

        // Load existing firms by CRD for comparison
        var crdNumbers = incomingFirms
            .Where(f => !string.IsNullOrEmpty(f.CrdNumber))
            .Select(f => f.CrdNumber)
            .ToList();

        // Batch load in chunks of 500 to avoid SQLite parameter limits
        var existingFirmsDict = new Dictionary<string, Firm>();
        foreach (var chunk in crdNumbers.Chunk(500))
        {
            var chunkList = chunk.ToList();
            var firmsBatch = ctx.Firms.AsNoTracking()
                .Where(f => chunkList.Contains(f.CrdNumber))
                .ToList();
            foreach (var f in firmsBatch)
                existingFirmsDict[f.CrdNumber] = f;
        }

        foreach (var incoming in incomingFirms)
        {
            if (string.IsNullOrEmpty(incoming.CrdNumber)) continue;
            if (!existingFirmsDict.TryGetValue(incoming.CrdNumber, out var existing)) continue;

            // --- AUM Changes ---
            var oldAum = (existing.RegulatoryAum ?? 0) + (existing.RegulatoryAumNonDiscretionary ?? 0);
            var newAum = (incoming.RegulatoryAum ?? 0) + (incoming.RegulatoryAumNonDiscretionary ?? 0);

            if (oldAum > 0 && newAum > 0)
            {
                var pctChange = (double)((newAum - oldAum) / oldAum * 100);

                if (pctChange > 50)
                {
                    events.Add(new FirmFilingEvent
                    {
                        FirmCrd = incoming.CrdNumber,
                        EventDate = now,
                        EventType = "AUM_SURGE",
                        Description = $"AUM surged {pctChange:F1}% from {FormatHelpers.FormatAum(oldAum)} to {FormatHelpers.FormatAum(newAum)}",
                        OldValue = oldAum.ToString("F0"),
                        NewValue = newAum.ToString("F0"),
                        PercentChange = pctChange,
                        Severity = "HIGH",
                        CreatedAt = now
                    });
                }
                else if (pctChange > 20)
                {
                    events.Add(new FirmFilingEvent
                    {
                        FirmCrd = incoming.CrdNumber,
                        EventDate = now,
                        EventType = "AUM_JUMP",
                        Description = $"AUM grew {pctChange:F1}% from {FormatHelpers.FormatAum(oldAum)} to {FormatHelpers.FormatAum(newAum)}",
                        OldValue = oldAum.ToString("F0"),
                        NewValue = newAum.ToString("F0"),
                        PercentChange = pctChange,
                        Severity = "HIGH",
                        CreatedAt = now
                    });
                }
                else if (pctChange < -20)
                {
                    events.Add(new FirmFilingEvent
                    {
                        FirmCrd = incoming.CrdNumber,
                        EventDate = now,
                        EventType = "AUM_DROP",
                        Description = $"AUM declined {Math.Abs(pctChange):F1}% from {FormatHelpers.FormatAum(oldAum)} to {FormatHelpers.FormatAum(newAum)}",
                        OldValue = oldAum.ToString("F0"),
                        NewValue = newAum.ToString("F0"),
                        PercentChange = pctChange,
                        Severity = "HIGH",
                        CreatedAt = now
                    });
                }
            }

            // --- Status Changes ---
            if (!string.IsNullOrEmpty(existing.RegistrationStatus) &&
                !string.IsNullOrEmpty(incoming.RegistrationStatus) &&
                !string.Equals(existing.RegistrationStatus, incoming.RegistrationStatus, StringComparison.OrdinalIgnoreCase))
            {
                var severity = incoming.RegistrationStatus.Contains("Approved", StringComparison.OrdinalIgnoreCase)
                    ? "MEDIUM" : "HIGH";

                events.Add(new FirmFilingEvent
                {
                    FirmCrd = incoming.CrdNumber,
                    EventDate = now,
                    EventType = "STATUS_CHANGE",
                    Description = $"Registration status changed from '{existing.RegistrationStatus}' to '{incoming.RegistrationStatus}'",
                    OldValue = existing.RegistrationStatus,
                    NewValue = incoming.RegistrationStatus,
                    Severity = severity,
                    CreatedAt = now
                });
            }

            // --- Employee Count Changes ---
            if (existing.NumberOfEmployees.HasValue && incoming.NumberOfEmployees.HasValue &&
                existing.NumberOfEmployees > 0)
            {
                var empPct = (double)(incoming.NumberOfEmployees.Value - existing.NumberOfEmployees.Value) /
                             existing.NumberOfEmployees.Value * 100;

                if (Math.Abs(empPct) > 30)
                {
                    var direction = empPct > 0 ? "increased" : "decreased";
                    events.Add(new FirmFilingEvent
                    {
                        FirmCrd = incoming.CrdNumber,
                        EventDate = now,
                        EventType = "EMPLOYEE_CHANGE",
                        Description = $"Employee count {direction} {Math.Abs(empPct):F0}% from {existing.NumberOfEmployees} to {incoming.NumberOfEmployees}",
                        OldValue = existing.NumberOfEmployees.ToString(),
                        NewValue = incoming.NumberOfEmployees.ToString(),
                        PercentChange = empPct,
                        Severity = Math.Abs(empPct) > 50 ? "HIGH" : "MEDIUM",
                        CreatedAt = now
                    });
                }
            }

            // --- Client Count Changes ---
            if (existing.NumClients.HasValue && incoming.NumClients.HasValue &&
                existing.NumClients > 0)
            {
                var clientPct = (double)(incoming.NumClients.Value - existing.NumClients.Value) /
                                existing.NumClients.Value * 100;

                if (Math.Abs(clientPct) > 25)
                {
                    var direction = clientPct > 0 ? "grew" : "declined";
                    events.Add(new FirmFilingEvent
                    {
                        FirmCrd = incoming.CrdNumber,
                        EventDate = now,
                        EventType = "CLIENT_SHIFT",
                        Description = $"Client count {direction} {Math.Abs(clientPct):F0}% from {existing.NumClients} to {incoming.NumClients}",
                        OldValue = existing.NumClients.ToString(),
                        NewValue = incoming.NumClients.ToString(),
                        PercentChange = clientPct,
                        Severity = Math.Abs(clientPct) > 50 ? "HIGH" : "MEDIUM",
                        CreatedAt = now
                    });
                }
            }

            // --- Advisor Count Changes (growth signal) ---
            if (existing.NumberOfAdvisors.HasValue && incoming.NumberOfAdvisors.HasValue &&
                existing.NumberOfAdvisors > 0)
            {
                var advPct = (double)(incoming.NumberOfAdvisors.Value - existing.NumberOfAdvisors.Value) /
                             existing.NumberOfAdvisors.Value * 100;

                if (advPct > 30)
                {
                    events.Add(new FirmFilingEvent
                    {
                        FirmCrd = incoming.CrdNumber,
                        EventDate = now,
                        EventType = "GROWTH_SIGNAL",
                        Description = $"Advisor headcount grew {advPct:F0}% from {existing.NumberOfAdvisors} to {incoming.NumberOfAdvisors}",
                        OldValue = existing.NumberOfAdvisors.ToString(),
                        NewValue = incoming.NumberOfAdvisors.ToString(),
                        PercentChange = advPct,
                        Severity = "MEDIUM",
                        CreatedAt = now
                    });
                }
            }
        }

        // Batch insert events
        if (events.Count > 0)
        {
            ctx.FirmFilingEvents.AddRange(events);
            ctx.SaveChanges();
        }

        progress?.Report($"Change Detection: Generated {events.Count} events from {incomingFirms.Count} firms.");
        return events.Count;
    }

    /// <summary>
    /// Gets all events for a firm, ordered by date descending.
    /// </summary>
    public List<FirmFilingEvent> GetEventsForFirm(string firmCrd)
    {
        using var ctx = CreateContext();
        return ctx.FirmFilingEvents
            .AsNoTracking()
            .Where(e => e.FirmCrd == firmCrd)
            .OrderByDescending(e => e.EventDate)
            .ToList();
    }

    /// <summary>
    /// Gets recent events across all firms, optionally filtered by severity or type.
    /// </summary>
    public List<FirmFilingEvent> GetRecentEvents(
        int count = 100,
        string? severity = null,
        string? eventType = null,
        bool unReviewedOnly = false)
    {
        using var ctx = CreateContext();
        IQueryable<FirmFilingEvent> query = ctx.FirmFilingEvents.AsNoTracking();

        if (!string.IsNullOrEmpty(severity))
            query = query.Where(e => e.Severity == severity);
        if (!string.IsNullOrEmpty(eventType))
            query = query.Where(e => e.EventType == eventType);
        if (unReviewedOnly)
            query = query.Where(e => !e.IsReviewed);

        return query
            .OrderByDescending(e => e.EventDate)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Gets event counts by type for summary display.
    /// </summary>
    public Dictionary<string, int> GetEventSummary(int daysBack = 30)
    {
        using var ctx = CreateContext();
        var since = DateTime.UtcNow.AddDays(-daysBack);
        return ctx.FirmFilingEvents
            .AsNoTracking()
            .Where(e => e.EventDate >= since)
            .GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Marks events as reviewed.
    /// </summary>
    public void MarkEventsReviewed(IEnumerable<int> eventIds)
    {
        using var ctx = CreateContext();
        var idList = eventIds.ToList();
        ctx.FirmFilingEvents
            .Where(e => idList.Contains(e.Id))
            .ExecuteUpdate(s => s.SetProperty(e => e.IsReviewed, true));
    }
}
