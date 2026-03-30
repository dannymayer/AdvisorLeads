using AdvisorLeads.Data;
using AdvisorLeads.Models;
using Microsoft.EntityFrameworkCore;

namespace AdvisorLeads.Services;

public record TeamLiftEvent(
    string FromFirmName,
    string FromFirmCrd,
    List<EmploymentChangeEvent> Members,
    DateTime EarliestDeparture,
    DateTime LatestDeparture);

public class TeamLiftDetectionService
{
    private readonly string _dbPath;

    public TeamLiftDetectionService(string dbPath)
    {
        _dbPath = dbPath;
    }

    private DatabaseContext CreateContext() => new DatabaseContext(_dbPath);

    public List<TeamLiftEvent> DetectRecentLifts(int windowDays = 90, int minGroupSize = 2)
    {
        using var ctx = CreateContext();
        var cutoff = DateTime.UtcNow.AddDays(-windowDays);
        var events = ctx.EmploymentChangeEvents
            .AsNoTracking()
            .Where(e => e.DetectedAt >= cutoff && e.FromFirmCrd != null)
            .ToList();

        var groups = events
            .GroupBy(e => e.FromFirmCrd!)
            .Where(g => g.Count() >= minGroupSize)
            .Select(g =>
            {
                var members = g.ToList();
                var earliest = members.Min(e => e.DetectedAt);
                var latest = members.Max(e => e.DetectedAt);
                if ((latest - earliest).TotalDays <= windowDays)
                {
                    return new TeamLiftEvent(
                        members.First().FromFirmName ?? g.Key,
                        g.Key,
                        members,
                        earliest,
                        latest);
                }
                return null;
            })
            .Where(e => e != null)
            .Select(e => e!)
            .OrderByDescending(e => e.Members.Count)
            .ToList();

        return groups;
    }
}
