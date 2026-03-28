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

    // ── GetAdvisorsWithCount ───────────────────────────────────────────

    [Fact]
    public void GetAdvisorsWithCount_ReturnsTupleWithCorrectCount()
    {
        _repo.UpsertAdvisor(MakeAdvisor("WC01", "Alpha", "One", "NY"));
        _repo.UpsertAdvisor(MakeAdvisor("WC02", "Beta", "Two", "NY"));
        _repo.UpsertAdvisor(MakeAdvisor("WC03", "Gamma", "Three", "CA"));

        var filter = new SearchFilter { State = "NY", PageSize = 100 };
        var (advisors, total) = _repo.GetAdvisorsWithCount(filter);

        Assert.Equal(2, total);
        Assert.Equal(2, advisors.Count);
        Assert.All(advisors, a => Assert.Equal("NY", a.State));
    }

    [Fact]
    public void GetAdvisorsWithCount_CountReflectsFullDataset_NotPage()
    {
        for (int i = 1; i <= 10; i++)
            _repo.UpsertAdvisor(MakeAdvisor($"WCP{i:D3}", $"User{i}", "Bulk"));

        var filter = new SearchFilter { PageNumber = 1, PageSize = 3 };
        var (advisors, total) = _repo.GetAdvisorsWithCount(filter);

        Assert.Equal(3, advisors.Count);
        Assert.Equal(10, total);
    }

    [Fact]
    public void GetAdvisorsWithCount_EmptyResult_ReturnsZero()
    {
        var filter = new SearchFilter { State = "ZZ", PageSize = 100 };
        var (advisors, total) = _repo.GetAdvisorsWithCount(filter);

        Assert.Empty(advisors);
        Assert.Equal(0, total);
    }

    // ── Name search CRD detection ───────────────────────────────────────

    [Fact]
    public void GetAdvisors_NameQueryNumeric_SearchesByCrd()
    {
        _repo.UpsertAdvisor(MakeAdvisor("123456", "John", "Smith"));
        _repo.UpsertAdvisor(MakeAdvisor("789012", "Jane", "Doe"));

        var filter = new SearchFilter { NameQuery = "123456", PageSize = 100 };
        var results = _repo.GetAdvisors(filter);

        Assert.Single(results);
        Assert.Equal("123456", results[0].CrdNumber);
    }

    [Fact]
    public void GetAdvisors_NameQueryShortNumeric_SearchesByName()
    {
        // Short numeric strings (< 3 digits) should still search by name, not CRD
        _repo.UpsertAdvisor(MakeAdvisor("SNUM01", "12", "Test"));

        var filter = new SearchFilter { NameQuery = "12", PageSize = 100 };
        var results = _repo.GetAdvisors(filter);

        Assert.Single(results);
        Assert.Equal("12", results[0].FirstName);
    }

    [Fact]
    public void GetAdvisors_NameQueryAlphabetic_SearchesByName()
    {
        _repo.UpsertAdvisor(MakeAdvisor("ALPHA01", "John", "Smith"));
        _repo.UpsertAdvisor(MakeAdvisor("ALPHA02", "Jane", "Doe"));

        var filter = new SearchFilter { NameQuery = "Smith", PageSize = 100 };
        var results = _repo.GetAdvisors(filter);

        Assert.Single(results);
        Assert.Equal("John", results[0].FirstName);
    }

    // ── Filter combinations ─────────────────────────────────────────────

    [Fact]
    public void GetAdvisors_StateAndRecordType_CombinesFilters()
    {
        _repo.UpsertAdvisor(MakeAdvisor("COMBO01", "A", "One", "CA"));
        var iarAdvisor = MakeAdvisor("COMBO02", "B", "Two", "CA");
        iarAdvisor.RecordType = "Investment Advisor Representative";
        _repo.UpsertAdvisor(iarAdvisor);
        _repo.UpsertAdvisor(MakeAdvisor("COMBO03", "C", "Three", "TX"));

        var filter = new SearchFilter
        {
            State = "CA",
            RecordType = "Investment Advisor Representative",
            PageSize = 100
        };
        var results = _repo.GetAdvisors(filter);

        Assert.Single(results);
        Assert.Equal("COMBO02", results[0].CrdNumber);
    }

    [Fact]
    public void GetAdvisors_FavoritesOnly_FiltersCorrectly()
    {
        _repo.UpsertAdvisor(MakeAdvisor("FAVF01", "Fav", "One"));
        _repo.UpsertAdvisor(MakeAdvisor("FAVF02", "NotFav", "Two"));

        var a = _repo.GetAdvisorByCrd("FAVF01");
        Assert.NotNull(a);
        _repo.SetAdvisorFavorited(a!.Id, true);

        var filter = new SearchFilter { ShowFavoritesOnly = true, PageSize = 100 };
        var results = _repo.GetAdvisors(filter);

        Assert.Single(results);
        Assert.Equal("FAVF01", results[0].CrdNumber);
    }

    [Fact]
    public void GetAdvisors_CityFilter_MatchesPartial()
    {
        var adv = MakeAdvisor("CITY01", "City", "Test", "CA");
        adv.City = "San Francisco";
        _repo.UpsertAdvisor(adv);

        var adv2 = MakeAdvisor("CITY02", "City2", "Test2", "CA");
        adv2.City = "Los Angeles";
        _repo.UpsertAdvisor(adv2);

        var filter = new SearchFilter { City = "San", PageSize = 100 };
        var results = _repo.GetAdvisors(filter);

        Assert.Single(results);
        Assert.Equal("CITY01", results[0].CrdNumber);
    }

    [Fact]
    public void GetAdvisors_MinYearsExperience_FiltersCorrectly()
    {
        var adv1 = MakeAdvisor("EXP01", "Rookie", "One");
        adv1.YearsOfExperience = 2;
        _repo.UpsertAdvisor(adv1);

        var adv2 = MakeAdvisor("EXP02", "Veteran", "Two");
        adv2.YearsOfExperience = 15;
        _repo.UpsertAdvisor(adv2);

        var filter = new SearchFilter { MinYearsExperience = 10, PageSize = 100 };
        var results = _repo.GetAdvisors(filter);

        Assert.Single(results);
        Assert.Equal("EXP02", results[0].CrdNumber);
    }

    // ── Sorting ─────────────────────────────────────────────────────────

    [Fact]
    public void GetAdvisors_SortByFirstName_OrdersCorrectly()
    {
        _repo.UpsertAdvisor(MakeAdvisor("SORT01", "Charlie", "Test"));
        _repo.UpsertAdvisor(MakeAdvisor("SORT02", "Alice", "Test"));
        _repo.UpsertAdvisor(MakeAdvisor("SORT03", "Bob", "Test"));

        var filter = new SearchFilter { SortBy = "FirstName", PageSize = 100 };
        var results = _repo.GetAdvisors(filter);

        Assert.Equal("Alice", results[0].FirstName);
        Assert.Equal("Bob", results[1].FirstName);
        Assert.Equal("Charlie", results[2].FirstName);
    }

    [Fact]
    public void GetAdvisors_SortDescending_ReversesOrder()
    {
        _repo.UpsertAdvisor(MakeAdvisor("SORTD01", "A", "Alpha"));
        _repo.UpsertAdvisor(MakeAdvisor("SORTD02", "A", "Zulu"));

        var filter = new SearchFilter { SortDescending = true, PageSize = 100 };
        var results = _repo.GetAdvisors(filter);

        Assert.Equal("Zulu", results[0].LastName);
        Assert.Equal("Alpha", results[1].LastName);
    }

    // ── GetAdvisorById with related data ────────────────────────────────

    [Fact]
    public void GetAdvisorById_ReturnsRelatedData()
    {
        var adv = MakeAdvisor("DETAIL01", "Detail", "Test");
        adv.EmploymentHistory.Add(new EmploymentHistory { FirmName = "Test Firm" });
        adv.QualificationList.Add(new Qualification { Name = "Series 7" });
        _repo.UpsertAdvisor(adv);

        var inserted = _repo.GetAdvisorByCrd("DETAIL01");
        Assert.NotNull(inserted);

        var loaded = _repo.GetAdvisorById(inserted!.Id);
        Assert.NotNull(loaded);
        Assert.NotEmpty(loaded!.EmploymentHistory);
        Assert.Equal("Test Firm", loaded.EmploymentHistory[0].FirmName);
        Assert.NotEmpty(loaded.QualificationList);
        Assert.Equal("Series 7", loaded.QualificationList[0].Name);
    }

    // ── Firm queries ────────────────────────────────────────────────────

    [Fact]
    public void GetFirms_Pagination_ReturnsCorrectPage()
    {
        for (int i = 1; i <= 8; i++)
            _repo.UpsertFirm(new Firm
            {
                CrdNumber = $"FP{i:D3}",
                Name = $"Firm {i:D3}",
                State = "CA",
                Source = "SEC"
            });

        var filter = new FirmSearchFilter { PageNumber = 1, PageSize = 3 };
        var page1 = _repo.GetFirms(filter);
        filter.PageNumber = 2;
        var page2 = _repo.GetFirms(filter);

        Assert.Equal(3, page1.Count);
        Assert.Equal(3, page2.Count);
        Assert.DoesNotContain(page2, f => page1.Any(p => p.CrdNumber == f.CrdNumber));
    }

    [Fact]
    public void GetFirmsWithCount_ReturnsTupleWithCorrectCount()
    {
        for (int i = 1; i <= 5; i++)
            _repo.UpsertFirm(new Firm
            {
                CrdNumber = $"FWC{i:D3}",
                Name = $"Firm WC {i}",
                State = i <= 3 ? "NY" : "TX",
                Source = "SEC"
            });

        var filter = new FirmSearchFilter { State = "NY", PageSize = 100 };
        var (firms, total) = _repo.GetFirmsWithCount(filter);

        Assert.Equal(3, total);
        Assert.Equal(3, firms.Count);
        Assert.All(firms, f => Assert.Equal("NY", f.State));
    }

    [Fact]
    public void GetFirmsWithCount_CountReflectsFullDataset()
    {
        for (int i = 1; i <= 7; i++)
            _repo.UpsertFirm(new Firm
            {
                CrdNumber = $"FWP{i:D3}",
                Name = $"Firm WP {i}",
                Source = "SEC"
            });

        var filter = new FirmSearchFilter { PageNumber = 1, PageSize = 2 };
        var (firms, total) = _repo.GetFirmsWithCount(filter);

        Assert.Equal(2, firms.Count);
        Assert.Equal(7, total);
    }

    [Fact]
    public void GetFirms_BrokerProtocolOnly_FiltersCorrectly()
    {
        _repo.UpsertFirm(new Firm
        {
            CrdNumber = "BPF01", Name = "Protocol Firm",
            BrokerProtocolMember = true, Source = "SEC"
        });
        _repo.UpsertFirm(new Firm
        {
            CrdNumber = "BPF02", Name = "Normal Firm",
            BrokerProtocolMember = false, Source = "SEC"
        });

        var filter = new FirmSearchFilter { BrokerProtocolOnly = true, PageSize = 100 };
        var results = _repo.GetFirms(filter);

        Assert.Single(results);
        Assert.Equal("BPF01", results[0].CrdNumber);
    }

    // ── AdvisorCount consistent with shared filter logic ────────────────

    [Fact]
    public void GetAdvisorCount_ConsistentWithGetAdvisorsWithCount()
    {
        _repo.UpsertAdvisor(MakeAdvisor("CONS01", "Alice", "Smith", "FL"));
        _repo.UpsertAdvisor(MakeAdvisor("CONS02", "Bob", "Jones", "FL"));
        _repo.UpsertAdvisor(MakeAdvisor("CONS03", "Carol", "Brown", "GA"));

        var filter = new SearchFilter { State = "FL", PageSize = 100 };

        int count = _repo.GetAdvisorCount(filter);
        var (_, total) = _repo.GetAdvisorsWithCount(filter);

        Assert.Equal(count, total);
        Assert.Equal(2, count);
    }

    [Fact]
    public void GetAdvisors_DisclosureFilter_Works()
    {
        var adv1 = MakeAdvisor("DISC01", "Has", "Disclosures");
        adv1.HasDisclosures = true;
        adv1.DisclosureCount = 3;
        _repo.UpsertAdvisor(adv1);

        var adv2 = MakeAdvisor("DISC02", "No", "Disclosures");
        adv2.HasDisclosures = false;
        _repo.UpsertAdvisor(adv2);

        var filter = new SearchFilter { HasDisclosures = true, PageSize = 100 };
        var results = _repo.GetAdvisors(filter);

        Assert.Single(results);
        Assert.Equal("DISC01", results[0].CrdNumber);
    }

    [Fact]
    public void GetAdvisors_MinDisclosureCount_FiltersCorrectly()
    {
        var adv1 = MakeAdvisor("MDC01", "Low", "Count");
        adv1.HasDisclosures = true;
        adv1.DisclosureCount = 1;
        _repo.UpsertAdvisor(adv1);

        var adv2 = MakeAdvisor("MDC02", "High", "Count");
        adv2.HasDisclosures = true;
        adv2.DisclosureCount = 5;
        _repo.UpsertAdvisor(adv2);

        var filter = new SearchFilter { MinDisclosureCount = 3, PageSize = 100 };
        var results = _repo.GetAdvisors(filter);

        Assert.Single(results);
        Assert.Equal("MDC02", results[0].CrdNumber);
    }

    [Fact]
    public void GetAdvisors_CrmImportFilter_Works()
    {
        _repo.UpsertAdvisor(MakeAdvisor("CIF01", "Imported", "One"));
        _repo.UpsertAdvisor(MakeAdvisor("CIF02", "NotImported", "Two"));

        var a = _repo.GetAdvisorByCrd("CIF01");
        Assert.NotNull(a);
        _repo.SetAdvisorImported(a!.Id, "crm-abc");

        var filter = new SearchFilter { IsImportedToCrm = true, PageSize = 100 };
        var results = _repo.GetAdvisors(filter);
        Assert.Single(results);
        Assert.Equal("CIF01", results[0].CrdNumber);

        filter.IsImportedToCrm = false;
        results = _repo.GetAdvisors(filter);
        Assert.Single(results);
        Assert.Equal("CIF02", results[0].CrdNumber);
    }

    [Fact]
    public void GetAdvisors_SourceFilter_Both_RequiresBothSources()
    {
        var adv1 = MakeAdvisor("SRC01", "Both", "Sources");
        adv1.Source = "FINRA,SEC";
        _repo.UpsertAdvisor(adv1);

        var adv2 = MakeAdvisor("SRC02", "Only", "Finra");
        adv2.Source = "FINRA";
        _repo.UpsertAdvisor(adv2);

        var filter = new SearchFilter { Source = "Both", PageSize = 100 };
        var results = _repo.GetAdvisors(filter);

        Assert.Single(results);
        Assert.Equal("SRC01", results[0].CrdNumber);
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
