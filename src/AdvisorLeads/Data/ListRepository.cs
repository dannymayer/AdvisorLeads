using Microsoft.EntityFrameworkCore;
using AdvisorLeads.Models;

namespace AdvisorLeads.Data;

public class ListRepository
{
    private readonly string _dbPath;

    public ListRepository(string databasePath)
    {
        _dbPath = databasePath;
    }

    private DatabaseContext CreateContext() => new DatabaseContext(_dbPath);

    public List<AdvisorList> GetAllLists()
    {
        using var ctx = CreateContext();
        return ctx.AdvisorLists
            .AsNoTracking()
            .Select(l => new AdvisorList
            {
                Id = l.Id,
                Name = l.Name,
                Description = l.Description,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt,
                MemberCount = ctx.AdvisorListMembers.Count(m => m.ListId == l.Id)
            })
            .OrderBy(l => l.Name)
            .ToList();
    }

    public AdvisorList CreateList(string name, string? description = null)
    {
        using var ctx = CreateContext();
        var list = new AdvisorList
        {
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.AdvisorLists.Add(list);
        ctx.SaveChanges();
        return list;
    }

    public void RenameList(int listId, string newName, string? description = null)
    {
        using var ctx = CreateContext();
        ctx.AdvisorLists
            .Where(l => l.Id == listId)
            .ExecuteUpdate(s => s
                .SetProperty(l => l.Name, newName)
                .SetProperty(l => l.Description, description)
                .SetProperty(l => l.UpdatedAt, DateTime.UtcNow));
    }

    public void DeleteList(int listId)
    {
        using var ctx = CreateContext();
        ctx.AdvisorLists
            .Where(l => l.Id == listId)
            .ExecuteDelete();
    }

    public bool AddToList(int listId, int advisorId, string? notes = null)
    {
        using var ctx = CreateContext();
        if (ctx.AdvisorListMembers.Any(m => m.ListId == listId && m.AdvisorId == advisorId))
            return false;

        ctx.AdvisorListMembers.Add(new AdvisorListMember
        {
            ListId = listId,
            AdvisorId = advisorId,
            Notes = notes,
            AddedAt = DateTime.UtcNow
        });
        ctx.SaveChanges();
        return true;
    }

    public void RemoveFromList(int listId, int advisorId)
    {
        using var ctx = CreateContext();
        ctx.AdvisorListMembers
            .Where(m => m.ListId == listId && m.AdvisorId == advisorId)
            .ExecuteDelete();
    }

    public List<Advisor> GetListMembers(int listId)
    {
        using var ctx = CreateContext();
        var members = ctx.AdvisorListMembers
            .AsNoTracking()
            .Where(m => m.ListId == listId)
            .Include(m => m.Advisor!)
                .ThenInclude(a => a!.EmploymentHistory)
            .Include(m => m.Advisor!)
                .ThenInclude(a => a!.Disclosures)
            .Include(m => m.Advisor!)
                .ThenInclude(a => a!.QualificationList)
            .OrderBy(m => m.Advisor!.LastName)
            .ToList();

        return members
            .Where(m => m.Advisor != null)
            .Select(m => m.Advisor!)
            .ToList();
    }

    public List<int> GetListIdsForAdvisor(int advisorId)
    {
        using var ctx = CreateContext();
        return ctx.AdvisorListMembers
            .AsNoTracking()
            .Where(m => m.AdvisorId == advisorId)
            .Select(m => m.ListId)
            .ToList();
    }

    public void UpdateMemberNotes(int listId, int advisorId, string? notes)
    {
        using var ctx = CreateContext();
        ctx.AdvisorListMembers
            .Where(m => m.ListId == listId && m.AdvisorId == advisorId)
            .ExecuteUpdate(s => s.SetProperty(m => m.Notes, notes));
    }
}
