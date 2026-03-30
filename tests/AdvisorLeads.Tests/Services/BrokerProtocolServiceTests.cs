using AdvisorLeads.Services;

namespace AdvisorLeads.Tests.Services;

/// <summary>
/// Tests for BrokerProtocolService HTML parsing logic.
/// No HTTP calls — exercises the static ParseMemberNames method directly.
/// </summary>
public class BrokerProtocolServiceTests
{
    // ── Table extraction ──────────────────────────────────────────────

    [Fact]
    public void BrokerProtocolService_ParseMemberNames_ExtractsFromTable()
    {
        // Build a table with 22 firm names to exceed the threshold that activates the table path
        var rows = new[]
        {
            "Ameriprise Financial Services",
            "Baird Financial Group",
            "Cambridge Investment Research",
            "Commonwealth Financial Network",
            "Cetera Financial Group",
            "Edward Jones",
            "Fidelity Investments",
            "First Clearing",
            "Hilliard Lyons",
            "Infinex Financial Group",
            "Janney Montgomery Scott",
            "Kestra Financial",
            "Lincoln Financial Advisors",
            "MML Investors Services",
            "National Planning Holdings",
            "Northwestern Mutual",
            "PNC Investments",
            "Raymond James Financial Services",
            "RBC Capital Markets",
            "Securities America",
            "Signator Investors",
            "Voya Financial Advisors",
        };

        var tds = string.Join("\n", rows.Select(r => $"<tr><td>{r}</td></tr>"));
        var html = $"<html><body><table>{tds}</table></body></html>";

        var names = BrokerProtocolService.ParseMemberNames(html);

        Assert.NotEmpty(names);
        Assert.Contains("Fidelity Investments", names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Raymond James Financial Services", names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Edward Jones", names, StringComparer.OrdinalIgnoreCase);
    }

    // ── <li> fallback ─────────────────────────────────────────────────

    [Fact]
    public void BrokerProtocolService_ParseMemberNames_FallsBackToListItems()
    {
        const string html = """
            <html><body>
            <ul>
              <li>Fidelity Investments</li>
              <li>Raymond James Financial Services</li>
              <li>Edward Jones</li>
            </ul>
            </body></html>
            """;

        var names = BrokerProtocolService.ParseMemberNames(html);

        Assert.NotEmpty(names);
        Assert.Contains("Fidelity Investments", names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Raymond James Financial Services", names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Edward Jones", names, StringComparer.OrdinalIgnoreCase);
    }

    // ── Navigation/boilerplate filtering ─────────────────────────────

    [Fact]
    public void BrokerProtocolService_ParseMemberNames_FiltersNavigationText()
    {
        const string html = """
            <html><body>
            <ul>
              <li>Fidelity Investments</li>
              <li>Contact J.S. Held</li>
              <li>Copyright 2024</li>
              <li>Privacy Policy</li>
              <li>Terms of Use</li>
              <li>Edward Jones</li>
            </ul>
            </body></html>
            """;

        var names = BrokerProtocolService.ParseMemberNames(html);

        Assert.Contains("Fidelity Investments", names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Edward Jones", names, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(names, n => n.Contains("J.S. Held", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Privacy", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Copyright", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Terms", StringComparison.OrdinalIgnoreCase));
    }

    // ── Deduplication ─────────────────────────────────────────────────

    [Fact]
    public void BrokerProtocolService_ParseMemberNames_DeduplicatesResults()
    {
        const string html = """
            <html><body>
            <ul>
              <li>Fidelity Investments</li>
              <li>Fidelity Investments</li>
              <li>Edward Jones</li>
            </ul>
            </body></html>
            """;

        var names = BrokerProtocolService.ParseMemberNames(html);

        Assert.Equal(names.Distinct(StringComparer.OrdinalIgnoreCase).Count(), names.Count);
    }
}
