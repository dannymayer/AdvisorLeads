using AdvisorLeads.Abstractions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

public class DataSeeder
{
    private readonly IAdvisorRepository _repo;

    public DataSeeder(IAdvisorRepository repo)
    {
        _repo = repo;
    }

    public void SeedInitialDataIfEmpty()
    {
        // Check if database already has advisors
        var existing = _repo.GetAdvisors(new SearchFilter());
        if (existing.Count > 0)
            return;

        // Seed sample advisors
        var advisors = new[]
        {
            new Advisor
            {
                CrdNumber = "1234567",
                IapdNumber = "100001",
                FirstName = "John",
                LastName = "Smith",
                MiddleName = "Michael",
                Title = "Senior Financial Advisor",
                Email = "john.smith@advisor.com",
                Phone = "(555) 123-4567",
                City = "New York",
                State = "NY",
                ZipCode = "10001",
                Licenses = "Series 7, Series 63, Series 65",
                Qualifications = "CFP, CFA",
                CurrentFirmName = "Wealth Management Group",
                CurrentFirmCrd = "98765",
                RegistrationStatus = "Active",
                RegistrationDate = new DateTime(2015, 3, 15),
                YearsOfExperience = 18,
                Source = "FINRA",
                HasDisclosures = true,
                DisclosureCount = 2,
                EmploymentHistory = new()
                {
                    new EmploymentHistory
                    {
                        FirmName = "Wealth Management Group",
                        FirmCrd = "98765",
                        Position = "Senior Financial Advisor",
                        StartDate = new DateTime(2018, 6, 1),
                        EndDate = null
                    },
                    new EmploymentHistory
                    {
                        FirmName = "Global Financial Partners",
                        FirmCrd = "54321",
                        Position = "Financial Advisor",
                        StartDate = new DateTime(2015, 3, 15),
                        EndDate = new DateTime(2018, 5, 31)
                    }
                },
                Disclosures = new()
                {
                    new Disclosure
                    {
                        Type = "Customer Complaint",
                        Description = "Unsuitable investment recommendation",
                        Date = new DateTime(2022, 4, 10),
                        Resolution = "Settlement",
                        Source = "FINRA"
                    },
                    new Disclosure
                    {
                        Type = "Regulatory Action",
                        Description = "Late filing of required form",
                        Date = new DateTime(2021, 1, 20),
                        Resolution = "Fine",
                        Source = "SEC"
                    }
                },
                QualificationList = new()
                {
                    new Qualification { Name = "Certified Financial Planner", Code = "CFP", Status = "Active", Date = new DateTime(2012, 5, 15) },
                    new Qualification { Name = "Chartered Financial Analyst", Code = "CFA", Status = "Active", Date = new DateTime(2010, 6, 20) }
                }
            },
            new Advisor
            {
                CrdNumber = "2345678",
                IapdNumber = "100002",
                FirstName = "Sarah",
                LastName = "Johnson",
                MiddleName = "Elizabeth",
                Title = "Investment Advisor",
                Email = "sarah.johnson@advisor.com",
                Phone = "(555) 234-5678",
                City = "Los Angeles",
                State = "CA",
                ZipCode = "90001",
                Licenses = "Series 7, Series 63",
                Qualifications = "CFA",
                CurrentFirmName = "Pacific Investment Advisors",
                CurrentFirmCrd = "11111",
                RegistrationStatus = "Active",
                RegistrationDate = new DateTime(2018, 7, 1),
                YearsOfExperience = 12,
                Source = "SEC",
                HasDisclosures = false,
                DisclosureCount = 0,
                EmploymentHistory = new()
                {
                    new EmploymentHistory
                    {
                        FirmName = "Pacific Investment Advisors",
                        FirmCrd = "11111",
                        Position = "Investment Advisor",
                        StartDate = new DateTime(2020, 1, 15),
                        EndDate = null
                    },
                    new EmploymentHistory
                    {
                        FirmName = "West Coast Wealth Partners",
                        FirmCrd = "22222",
                        Position = "Associate Advisor",
                        StartDate = new DateTime(2018, 7, 1),
                        EndDate = new DateTime(2019, 12, 31)
                    }
                },
                Disclosures = new(),
                QualificationList = new()
                {
                    new Qualification { Name = "Chartered Financial Analyst", Code = "CFA", Status = "Active", Date = new DateTime(2015, 6, 15) }
                }
            },
            new Advisor
            {
                CrdNumber = "3456789",
                IapdNumber = "100003",
                FirstName = "Michael",
                LastName = "Chen",
                Title = "Portfolio Manager",
                Email = "michael.chen@advisor.com",
                Phone = "(555) 345-6789",
                City = "Chicago",
                State = "IL",
                ZipCode = "60601",
                Licenses = "Series 7, Series 24",
                Qualifications = "CFA, CIMA",
                CurrentFirmName = "Midwest Capital Management",
                CurrentFirmCrd = "33333",
                RegistrationStatus = "Active",
                RegistrationDate = new DateTime(2012, 4, 10),
                YearsOfExperience = 22,
                Source = "FINRA",
                HasDisclosures = true,
                DisclosureCount = 1,
                EmploymentHistory = new()
                {
                    new EmploymentHistory
                    {
                        FirmName = "Midwest Capital Management",
                        FirmCrd = "33333",
                        Position = "Senior Portfolio Manager",
                        StartDate = new DateTime(2016, 9, 1),
                        EndDate = null
                    }
                },
                Disclosures = new()
                {
                    new Disclosure
                    {
                        Type = "Financial Disclosure",
                        Description = "Outside business activity",
                        Date = new DateTime(2020, 3, 15),
                        Resolution = "Approved",
                        Source = "Firm"
                    }
                },
                QualificationList = new()
                {
                    new Qualification { Name = "Chartered Financial Analyst", Code = "CFA", Status = "Active", Date = new DateTime(2008, 6, 15) },
                    new Qualification { Name = "Certified Investment Management Analyst", Code = "CIMA", Status = "Active", Date = new DateTime(2015, 4, 20) }
                }
            },
            new Advisor
            {
                CrdNumber = "4567890",
                IapdNumber = "100004",
                FirstName = "Jennifer",
                LastName = "Williams",
                Title = "Financial Advisor",
                Email = "jennifer.williams@advisor.com",
                Phone = "(555) 456-7890",
                City = "Boston",
                State = "MA",
                ZipCode = "02101",
                Licenses = "Series 7, Series 63",
                CurrentFirmName = "Northeast Capital",
                CurrentFirmCrd = "44444",
                RegistrationStatus = "Active",
                RegistrationDate = new DateTime(2019, 8, 5),
                YearsOfExperience = 8,
                Source = "SEC",
                HasDisclosures = false,
                DisclosureCount = 0,
                EmploymentHistory = new()
                {
                    new EmploymentHistory
                    {
                        FirmName = "Northeast Capital",
                        FirmCrd = "44444",
                        Position = "Financial Advisor",
                        StartDate = new DateTime(2019, 8, 5),
                        EndDate = null
                    }
                },
                Disclosures = new(),
                QualificationList = new()
            }
        };

        // Insert all advisors
        foreach (var advisor in advisors)
        {
            _repo.UpsertAdvisor(advisor);
        }
    }
}
