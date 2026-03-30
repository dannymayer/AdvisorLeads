using Microsoft.EntityFrameworkCore;
using AdvisorLeads.Abstractions;
using AdvisorLeads.Models;
using System.Text;

namespace AdvisorLeads.Data;

public partial class AdvisorRepository : IAdvisorRepository
{
    private readonly string _dbPath;

    public AdvisorRepository(string databasePath)
    {
        _dbPath = databasePath;
    }

    private DatabaseContext CreateContext() => new DatabaseContext(_dbPath);
}
