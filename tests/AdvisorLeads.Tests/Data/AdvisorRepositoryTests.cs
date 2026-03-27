using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Tests.Data;

/// <summary>
/// Integration tests for AdvisorRepository using a real SQLite database.
/// Each test gets a fresh database to ensure isolation.
/// </summary>
public class AdvisorRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AdvisorRepository _repo;

    public AdvisorRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"advisorleads_repo_test_{Guid.NewGuid():N}.db");
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();
        _repo = new AdvisorRepository(_dbPath);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public void UpsertAdvisor_InsertsNewAdvisor()
    {
        var advisor = MakeAdvisor("INSERT01", "Alice", "Wonderland");
        _repo.UpsertAdvisor(advisor);

        var loaded = _repo.GetAdvisorByCrd("INSERT01");
        Assert.NotNull(loaded);
        Assert.Equal("Alice", loaded!.FirstName);
        Assert.Equal("Wonderland", loaded.LastName);
    }

    [Fact]
    public void UpsertAdvisor_UpdatesExistingAdvisor()
    {
        var advisor = MakeAdvisor("UPDATE01", "Bob", "Builder");
        _repo.UpsertAdvisor(advisor);

        advisor.FirstName = "Robert";
        advisor.State = "TX";
        _repo.UpsertAdvisor(advisor);

        var loaded = _repo.GetAdvisorByCrd("UPDATE01");
        Assert.NotNull(loaded);
        Assert.Equal("Robert", loaded!.FirstName);
        Assert.Equal("TX", loaded.State);
    }

    [Fact]
    public void GetAdvisors_FiltersById()
    {
        _repo.UpsertAdvisor(MakeAdvisor("FILTER01", "Amy", "Adams", "CA"));
        _repo.UpsertAdvisor(MakeAdvisor("FILTER02", "Bob", "Baker", "TX"));
        _repo.UpsertAdvisor(MakeAdvisor("FILTER03", "Carol", "Clark", "CA"));

        var filter = new SearchFilter { State = "CA", PageSize = 100 };
        var results = _repo.GetAdvisors(filter);

        Assert.Equal(2, results.Count);
        Assert.All(results, a => Assert.Equal("CA", a.State));
    }

    [Fact]
    public void GetAdvisors_NameFilterWorks()
    {
        _repo.UpsertAdvisor(MakeAdvisor("NAME01", "John", "Smith"));
        _repo.UpsertAdvisor(MakeAdvisor("NAME02", "Jane", "Doe"));
        _repo.UpsertAdvisor(MakeAdvisor("NAME03", "Johnny", "Walker"));

        var filter = new SearchFilter { NameQuery = "John", PageSize = 100 };
        var results = _repo.GetAdvisors(filter);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void GetAdvisors_ExcludedHiddenByDefault()
    {
        _repo.UpsertAdvisor(MakeAdvisor("EXCL01", "Visible", "One"));
        _repo.UpsertAdvisor(MakeAdvisor("EXCL02", "Hidden", "Two"));

        var a = _repo.GetAdvisorByCrd("EXCL02");
        Assert.NotNull(a);
        _repo.SetAdvisorExcluded(a!.Id, true, "Testing");

        var filter = new SearchFilter { PageSize = 100, IncludeExcluded = false };
        var results = _repo.GetAdvisors(filter);
        Assert.DoesNotContain(results, r => r.CrdNumber == "EXCL02");

        filter.IncludeExcluded = true;
        results = _repo.GetAdvisors(filter);
        Assert.Contains(results, r => r.CrdNumber == "EXCL02");
    }

    [Fact]
    public void SetAdvisorFavorited_TogglesState()
    {
        _repo.UpsertAdvisor(MakeAdvisor("FAV01", "Fav", "Test"));
        var a = _repo.GetAdvisorByCrd("FAV01");
        Assert.NotNull(a);
        Assert.False(a!.IsFavorited);

        _repo.SetAdvisorFavorited(a.Id, true);
        var updated = _repo.GetAdvisorByCrd("FAV01");
        Assert.True(updated!.IsFavorited);

        _repo.SetAdvisorFavorited(a.Id, false);
        updated = _repo.GetAdvisorByCrd("FAV01");
        Assert.False(updated!.IsFavorited);
    }

    [Fact]
    public void SetAdvisorImported_SetsCrmId()
    {
        _repo.UpsertAdvisor(MakeAdvisor("CRM01", "Import", "Test"));
        var a = _repo.GetAdvisorByCrd("CRM01");
        Assert.NotNull(a);
        Assert.False(a!.IsImportedToCrm);

        _repo.SetAdvisorImported(a.Id, "wb-12345");
        var updated = _repo.GetAdvisorByCrd("CRM01");
        Assert.True(updated!.IsImportedToCrm);
        Assert.Equal("wb-12345", updated.CrmId);
    }

    [Fact]
    public void GetDistinctStates_ReturnsUniqueStates()
    {
        _repo.UpsertAdvisor(MakeAdvisor("ST01", "A", "A", "NY"));
        _repo.UpsertAdvisor(MakeAdvisor("ST02", "B", "B", "CA"));
        _repo.UpsertAdvisor(MakeAdvisor("ST03", "C", "C", "NY"));

        var states = _repo.GetDistinctStates();
        Assert.Contains("NY", states);
        Assert.Contains("CA", states);
        Assert.Equal(states.Distinct().Count(), states.Count);
    }

    [Fact]
    public void GetAdvisorCount_MatchesFilteredResults()
    {
        _repo.UpsertAdvisor(MakeAdvisor("CNT01", "X", "Y", "FL"));
        _repo.UpsertAdvisor(MakeAdvisor("CNT02", "X", "Z", "FL"));
        _repo.UpsertAdvisor(MakeAdvisor("CNT03", "X", "W", "GA"));

        var filter = new SearchFilter { State = "FL", PageSize = 100 };
        var count = _repo.GetAdvisorCount(filter);
        Assert.Equal(2, count);
    }

    [Fact]
    public void UpsertFirm_InsertsAndUpdates()
    {
        var firm = new Firm
        {
            CrdNumber = "FIRM01",
            Name = "Test Firm",
            State = "CA",
            Source = "SEC"
        };
        _repo.UpsertFirm(firm);

        var firms = _repo.GetFirms(new FirmSearchFilter { NameQuery = "Test Firm" });
        Assert.Single(firms);
        Assert.Equal("Test Firm", firms[0].Name);

        firm.Name = "Updated Firm";
        _repo.UpsertFirm(firm);

        firms = _repo.GetFirms(new FirmSearchFilter { NameQuery = "Updated Firm" });
        Assert.Single(firms);
        Assert.Equal("Updated Firm", firms[0].Name);
    }

    [Fact]
    public void Pagination_ReturnsCorrectPage()
    {
        for (int i = 1; i <= 10; i++)
            _repo.UpsertAdvisor(MakeAdvisor($"PAGE{i:D3}", $"User{i}", "Test"));

        var page1 = _repo.GetAdvisors(new SearchFilter { PageNumber = 1, PageSize = 3 });
        var page2 = _repo.GetAdvisors(new SearchFilter { PageNumber = 2, PageSize = 3 });

        Assert.Equal(3, page1.Count);
        Assert.Equal(3, page2.Count);
        Assert.DoesNotContain(page2, a => page1.Any(p1 => p1.CrdNumber == a.CrdNumber));
    }

    private static Advisor MakeAdvisor(string crd, string first, string last, string? state = null)
    {
        return new Advisor
        {
            CrdNumber = crd,
            FirstName = first,
            LastName = last,
            State = state,
            Source = "FINRA",
            RecordType = "Registered Representative"
        };
    }
}
