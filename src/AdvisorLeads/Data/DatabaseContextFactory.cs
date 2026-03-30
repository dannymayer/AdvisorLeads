using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AdvisorLeads.Data;

/// <summary>
/// Allows EF Core tooling (dotnet ef migrations add) to instantiate
/// DatabaseContext at design time. Uses a temporary path — only the
/// model shape matters for migration generation.
/// </summary>
public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext(string[] args)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "advisorleads_designtime.db");
        return new DatabaseContext(tempPath);
    }
}
