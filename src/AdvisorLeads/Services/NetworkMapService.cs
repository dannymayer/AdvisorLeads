using AdvisorLeads.Data;
using AdvisorLeads.Models;
using Microsoft.EntityFrameworkCore;

namespace AdvisorLeads.Services;

public record NetworkNode(string Id, string Label, string Type, int AdvisorCount, decimal? Aum);
public record NetworkEdge(string Source, string Target, int SharedAdvisors, string EdgeType);
public record NetworkGraphData(List<NetworkNode> Nodes, List<NetworkEdge> Edges);

public class NetworkMapService
{
    private readonly string _dbPath;

    public NetworkMapService(string dbPath)
    {
        _dbPath = dbPath;
    }

    private DatabaseContext CreateContext() => new DatabaseContext(_dbPath);

    public NetworkGraphData GetFirmNetworkGraph(string firmCrd, int maxDegrees = 1)
    {
        using var ctx = CreateContext();
        var nodes = new Dictionary<string, NetworkNode>();
        var edges = new List<NetworkEdge>();

        var centralFirm = ctx.Firms.AsNoTracking().FirstOrDefault(f => f.CrdNumber == firmCrd);
        if (centralFirm == null)
            return new NetworkGraphData(new List<NetworkNode>(), new List<NetworkEdge>());

        var centralAum = (centralFirm.RegulatoryAum ?? 0) + (centralFirm.RegulatoryAumNonDiscretionary ?? 0);
        nodes[firmCrd] = new NetworkNode(
            firmCrd, centralFirm.Name, "firm",
            centralFirm.NumberOfAdvisors ?? 0,
            centralAum > 0 ? centralAum : null);

        var currentAdvisors = ctx.Advisors.AsNoTracking()
            .Where(a => a.CurrentFirmCrd == firmCrd && !a.IsExcluded)
            .Select(a => new { a.Id, a.FirstName, a.LastName, a.CrdNumber })
            .ToList();

        foreach (var adv in currentAdvisors)
        {
            var advId = "adv_" + adv.CrdNumber;
            var fullName = $"{adv.FirstName} {adv.LastName}".Trim();
            nodes[advId] = new NetworkNode(advId, fullName, "advisor", 0, null);
            edges.Add(new NetworkEdge(firmCrd, advId, 1, "current"));
        }

        if (maxDegrees >= 1)
        {
            var advisorIds = currentAdvisors.Select(a => a.Id).ToList();
            var priorHistory = ctx.EmploymentHistory.AsNoTracking()
                .Where(eh => advisorIds.Contains(eh.AdvisorId) &&
                             eh.FirmCrd != null && eh.FirmCrd != firmCrd &&
                             eh.EndDate != null)
                .ToList();

            var priorFirmGroups = priorHistory
                .Where(h => !string.IsNullOrEmpty(h.FirmCrd))
                .GroupBy(h => h.FirmCrd!);

            foreach (var grp in priorFirmGroups)
            {
                var priorFirmCrd = grp.Key;
                if (!nodes.ContainsKey(priorFirmCrd))
                {
                    var priorFirm = ctx.Firms.AsNoTracking().FirstOrDefault(f => f.CrdNumber == priorFirmCrd);
                    var priorAum = priorFirm != null
                        ? (priorFirm.RegulatoryAum ?? 0) + (priorFirm.RegulatoryAumNonDiscretionary ?? 0)
                        : 0;
                    nodes[priorFirmCrd] = new NetworkNode(
                        priorFirmCrd,
                        priorFirm?.Name ?? priorFirmCrd,
                        "prior_firm",
                        priorFirm?.NumberOfAdvisors ?? 0,
                        priorAum > 0 ? priorAum : null);
                }
                edges.Add(new NetworkEdge(priorFirmCrd, firmCrd, grp.Count(), "prior_employment"));
            }
        }

        return new NetworkGraphData(nodes.Values.ToList(), edges);
    }

    public NetworkGraphData GetTopCoRegistrationClusters(int topN = 20)
    {
        using var ctx = CreateContext();
        var nodes = new Dictionary<string, NetworkNode>();
        var edgeMap = new Dictionary<(string, string), int>();

        var firmPairs = ctx.Advisors.AsNoTracking()
            .Where(a => a.CurrentFirmCrd != null && !a.IsExcluded)
            .Join(ctx.EmploymentHistory.AsNoTracking()
                .Where(eh => eh.FirmCrd != null && eh.EndDate != null),
                a => a.Id, eh => eh.AdvisorId,
                (a, eh) => new { CurrentFirm = a.CurrentFirmCrd!, PriorFirm = eh.FirmCrd! })
            .Where(x => x.CurrentFirm != x.PriorFirm)
            .GroupBy(x => new { x.CurrentFirm, x.PriorFirm })
            .Select(g => new { g.Key.CurrentFirm, g.Key.PriorFirm, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(topN)
            .ToList();

        foreach (var pair in firmPairs)
        {
            foreach (var crd in new[] { pair.CurrentFirm, pair.PriorFirm })
            {
                if (!nodes.ContainsKey(crd))
                {
                    var firm = ctx.Firms.AsNoTracking().FirstOrDefault(f => f.CrdNumber == crd);
                    var aum = firm != null
                        ? (firm.RegulatoryAum ?? 0) + (firm.RegulatoryAumNonDiscretionary ?? 0)
                        : 0;
                    nodes[crd] = new NetworkNode(
                        crd,
                        firm?.Name ?? crd,
                        "firm",
                        firm?.NumberOfAdvisors ?? 0,
                        aum > 0 ? aum : null);
                }
            }

            var key = pair.CurrentFirm.CompareTo(pair.PriorFirm) < 0
                ? (pair.CurrentFirm, pair.PriorFirm)
                : (pair.PriorFirm, pair.CurrentFirm);
            edgeMap[key] = edgeMap.TryGetValue(key, out var existing) ? existing + pair.Count : pair.Count;
        }

        var edges = edgeMap.Select(kv =>
            new NetworkEdge(kv.Key.Item1, kv.Key.Item2, kv.Value, "co_registration")).ToList();

        return new NetworkGraphData(nodes.Values.ToList(), edges);
    }
}
