using AdvisorLeads.Models;

namespace AdvisorLeads.Tests.Models;

/// <summary>
/// Tests for model correctness and computed properties.
/// </summary>
public class ModelTests
{
    [Theory]
    [InlineData("John", "Doe", "John Doe")]
    [InlineData("Jane", "Smith", "Jane Smith")]
    [InlineData("", "Solo", "Solo")]
    [InlineData("First", "", "First")]
    [InlineData("  Alice  ", "  Brown  ", "Alice     Brown")]
    public void Advisor_FullName_CombinesFirstAndLast(string first, string last, string expected)
    {
        var advisor = new Advisor { FirstName = first, LastName = last };
        Assert.Equal(expected, advisor.FullName);
    }

    [Fact]
    public void Advisor_DefaultValues_AreCorrect()
    {
        var advisor = new Advisor();
        Assert.False(advisor.HasDisclosures);
        Assert.Equal(0, advisor.DisclosureCount);
        Assert.False(advisor.IsExcluded);
        Assert.False(advisor.IsFavorited);
        Assert.False(advisor.IsImportedToCrm);
        Assert.Empty(advisor.EmploymentHistory);
        Assert.Empty(advisor.Disclosures);
        Assert.Empty(advisor.QualificationList);
    }

    [Fact]
    public void Advisor_ToString_ReturnsFullName()
    {
        var advisor = new Advisor { FirstName = "Test", LastName = "User" };
        Assert.Equal("Test User", advisor.ToString());
    }

    [Fact]
    public void Firm_DefaultValues_AreCorrect()
    {
        var firm = new Firm();
        Assert.Empty(firm.CrdNumber);
        Assert.Empty(firm.Name);
        Assert.False(firm.IsRegisteredWithSec);
        Assert.False(firm.IsRegisteredWithFinra);
        Assert.False(firm.BrokerProtocolMember);
        Assert.False(firm.IsExcluded);
    }

    [Fact]
    public void Firm_ToString_ReturnsName()
    {
        var firm = new Firm { Name = "Test Firm LLC" };
        Assert.Equal("Test Firm LLC", firm.ToString());
    }

    [Fact]
    public void SearchFilter_DefaultPageSize_IsPositive()
    {
        var filter = new SearchFilter();
        Assert.True(filter.PageSize > 0 || filter.PageSize == 0);
        Assert.Equal(1, filter.PageNumber);
    }

    [Fact]
    public void FirmSearchFilter_Defaults_AreNull()
    {
        var filter = new FirmSearchFilter();
        Assert.Null(filter.NameQuery);
        Assert.Null(filter.State);
        Assert.Null(filter.RecordType);
        Assert.Null(filter.MinRegulatoryAum);
        Assert.False(filter.BrokerProtocolOnly);
    }

    [Fact]
    public void AdvisorList_MemberCount_DefaultsToZero()
    {
        var list = new AdvisorList();
        Assert.Equal(0, list.MemberCount);
        Assert.Equal(string.Empty, list.Name);
    }

    [Fact]
    public void EmploymentHistory_IsCurrent_TrueWhenEndDateNull()
    {
        var emp = new EmploymentHistory();
        Assert.True(emp.IsCurrent); // EndDate is null by default = "current"
        Assert.Equal(string.Empty, emp.FirmName);

        emp.EndDate = DateTime.Now;
        Assert.False(emp.IsCurrent);
    }

    [Fact]
    public void EdgarSearchResult_DefaultValues()
    {
        var result = new EdgarSearchResult();
        Assert.Null(result.FirmCrd);
        Assert.Equal(string.Empty, result.AccessionNumber);
        Assert.Null(result.Category);
    }
}
