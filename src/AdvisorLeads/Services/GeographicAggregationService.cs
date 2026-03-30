using AdvisorLeads.Abstractions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

public record StateAggregation(
    string StateCode,
    string StateName,
    int AdvisorCount,
    int ActiveAdvisorCount,
    decimal? TotalAum,
    decimal? AvgAumPerAdvisor,
    double DisclosureRate,
    int FavoritedCount,
    int WatchedCount);

public class GeographicAggregationService
{
    private readonly IAdvisorRepository _repo;

    public GeographicAggregationService(IAdvisorRepository repo)
    {
        _repo = repo;
    }

    private static readonly Dictionary<string, string> StateNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AL"] = "Alabama", ["AK"] = "Alaska", ["AZ"] = "Arizona", ["AR"] = "Arkansas",
        ["CA"] = "California", ["CO"] = "Colorado", ["CT"] = "Connecticut", ["DE"] = "Delaware",
        ["FL"] = "Florida", ["GA"] = "Georgia", ["HI"] = "Hawaii", ["ID"] = "Idaho",
        ["IL"] = "Illinois", ["IN"] = "Indiana", ["IA"] = "Iowa", ["KS"] = "Kansas",
        ["KY"] = "Kentucky", ["LA"] = "Louisiana", ["ME"] = "Maine", ["MD"] = "Maryland",
        ["MA"] = "Massachusetts", ["MI"] = "Michigan", ["MN"] = "Minnesota", ["MS"] = "Mississippi",
        ["MO"] = "Missouri", ["MT"] = "Montana", ["NE"] = "Nebraska", ["NV"] = "Nevada",
        ["NH"] = "New Hampshire", ["NJ"] = "New Jersey", ["NM"] = "New Mexico", ["NY"] = "New York",
        ["NC"] = "North Carolina", ["ND"] = "North Dakota", ["OH"] = "Ohio", ["OK"] = "Oklahoma",
        ["OR"] = "Oregon", ["PA"] = "Pennsylvania", ["RI"] = "Rhode Island", ["SC"] = "South Carolina",
        ["SD"] = "South Dakota", ["TN"] = "Tennessee", ["TX"] = "Texas", ["UT"] = "Utah",
        ["VT"] = "Vermont", ["VA"] = "Virginia", ["WA"] = "Washington", ["WV"] = "West Virginia",
        ["WI"] = "Wisconsin", ["WY"] = "Wyoming", ["DC"] = "District of Columbia",
        ["PR"] = "Puerto Rico", ["GU"] = "Guam", ["VI"] = "U.S. Virgin Islands",
    };

    public List<StateAggregation> GetStateAggregations(bool activeOnly = true)
    {
        var advisors = _repo.GetAdvisorsForGeography(activeOnly);
        var firms = _repo.GetFirmsForGeography();

        var aumByState = firms
            .Where(f => !string.IsNullOrEmpty(f.State))
            .GroupBy(f => f.State!)
            .ToDictionary(g => g.Key, g => g.Sum(f =>
                (f.RegulatoryAum ?? 0) + (f.RegulatoryAumNonDiscretionary ?? 0)));

        var groups = advisors
            .Where(a => !string.IsNullOrEmpty(a.State))
            .GroupBy(a => a.State!)
            .Select(g =>
            {
                var stateCode = g.Key;
                var total = g.Count();
                var active = g.Count(a => a.RegistrationStatus != null &&
                    a.RegistrationStatus.Contains("Active", StringComparison.OrdinalIgnoreCase));
                var withDisc = g.Count(a => a.HasDisclosures);
                var favorited = g.Count(a => a.IsFavorited);
                var watched = g.Count(a => a.IsWatched);

                aumByState.TryGetValue(stateCode, out decimal totalAum);
                decimal? avgAum = total > 0 && totalAum > 0 ? totalAum / total : null;

                return new StateAggregation(
                    stateCode,
                    StateNames.TryGetValue(stateCode, out var name) ? name : stateCode,
                    total,
                    activeOnly ? total : active,
                    totalAum > 0 ? totalAum : null,
                    avgAum,
                    total > 0 ? (double)withDisc / total : 0.0,
                    favorited,
                    watched);
            })
            .OrderByDescending(s => s.AdvisorCount)
            .ToList();

        return groups;
    }

    public (int min, int max) GetAdvisorCountRange(List<StateAggregation> data)
    {
        if (data.Count == 0) return (0, 0);
        return (data.Min(s => s.AdvisorCount), data.Max(s => s.AdvisorCount));
    }
}
