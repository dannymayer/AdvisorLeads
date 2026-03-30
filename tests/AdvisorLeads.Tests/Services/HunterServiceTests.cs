using AdvisorLeads.Data;
using AdvisorLeads.Models;
using AdvisorLeads.Services;

namespace AdvisorLeads.Tests.Services;

public class HunterServiceTests
{
    // ── ParseEmailFinderResponse ──────────────────────────────────────

    [Fact]
    public void HunterService_ParseEmailFinderResponse_ReturnsResult()
    {
        const string json = """
            {
              "data": {
                "email": "john.smith@example.com",
                "score": 82,
                "sources": [{ "domain": "linkedin.com" }]
              },
              "meta": {}
            }
            """;

        var result = HunterService.ParseEmailFinderResponse(json);

        Assert.NotNull(result);
        Assert.Equal("john.smith@example.com", result!.Email);
        Assert.Equal(82, result.Score);
        Assert.Equal("linkedin.com", result.Source);
    }

    [Fact]
    public void HunterService_ParseEmailFinderResponse_ReturnsNullOnError()
    {
        const string errorJson = """
            {
              "errors": [{ "details": "No result found.", "id": "email_not_found" }]
            }
            """;

        var result = HunterService.ParseEmailFinderResponse(errorJson);

        Assert.Null(result);
    }

    [Fact]
    public void HunterService_ParseEmailFinderResponse_ReturnsNullWhenEmailMissing()
    {
        const string json = """
            {
              "data": {
                "score": 40
              },
              "meta": {}
            }
            """;

        var result = HunterService.ParseEmailFinderResponse(json);

        Assert.Null(result);
    }

    [Fact]
    public void HunterService_ParseEmailFinderResponse_ReturnsNullOnInvalidJson()
    {
        var result = HunterService.ParseEmailFinderResponse("not valid json");

        Assert.Null(result);
    }

    // ── ExtractDomain ─────────────────────────────────────────────────

    [Theory]
    [InlineData("https://www.example.com", "example.com")]
    [InlineData("http://www.example.com/about", "example.com")]
    [InlineData("https://example.com", "example.com")]
    [InlineData("http://example.com/", "example.com")]
    [InlineData("www.example.com", "example.com")]
    [InlineData("example.com", "example.com")]
    [InlineData("https://sub.example.co.uk/path/page", "sub.example.co.uk")]
    public void HunterService_ExtractDomain_StripsHttpsAndWww(string input, string expected)
    {
        var result = HunterService.ExtractDomain(input);
        Assert.Equal(expected, result);
    }
}

/// <summary>
/// Integration tests for AdvisorRepository.UpdateAdvisorEmails.
/// </summary>
public class HunterRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AdvisorRepository _repo;

    public HunterRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"advisorleads_hunter_test_{Guid.NewGuid():N}.db");
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();
        _repo = new AdvisorRepository(_dbPath);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public void AdvisorRepository_UpdateAdvisorEmails_PersistsEmail()
    {
        // Seed an advisor without an email
        var advisor = new Advisor
        {
            CrdNumber = "HUNTER01",
            FirstName = "Jane",
            LastName = "Hunter",
            RecordType = "Registered Representative",
            RegistrationStatus = "Active",
            Source = "FINRA",
            UpdatedAt = DateTime.UtcNow
        };
        _repo.UpsertAdvisor(advisor);

        var seeded = _repo.GetAdvisorByCrd("HUNTER01");
        Assert.NotNull(seeded);
        Assert.Null(seeded!.Email);

        // Act — update email
        int updated = _repo.UpdateAdvisorEmails(new[] { (seeded.Id, "jane.hunter@example.com") });

        // Assert
        Assert.Equal(1, updated);
        var reloaded = _repo.GetAdvisorByCrd("HUNTER01");
        Assert.NotNull(reloaded);
        Assert.Equal("jane.hunter@example.com", reloaded!.Email);
    }

    [Fact]
    public void AdvisorRepository_UpdateAdvisorEmails_UpdatesMultiple()
    {
        var a1 = new Advisor { CrdNumber = "HMU01", FirstName = "A", LastName = "One", Source = "FINRA", UpdatedAt = DateTime.UtcNow };
        var a2 = new Advisor { CrdNumber = "HMU02", FirstName = "B", LastName = "Two", Source = "FINRA", UpdatedAt = DateTime.UtcNow };
        _repo.UpsertAdvisor(a1);
        _repo.UpsertAdvisor(a2);

        var s1 = _repo.GetAdvisorByCrd("HMU01")!;
        var s2 = _repo.GetAdvisorByCrd("HMU02")!;

        _repo.UpdateAdvisorEmails(new[]
        {
            (s1.Id, "a.one@firm.com"),
            (s2.Id, "b.two@firm.com")
        });

        Assert.Equal("a.one@firm.com", _repo.GetAdvisorByCrd("HMU01")!.Email);
        Assert.Equal("b.two@firm.com", _repo.GetAdvisorByCrd("HMU02")!.Email);
    }
}
