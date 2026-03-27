using Microsoft.EntityFrameworkCore;
using AdvisorLeads.Models;

namespace AdvisorLeads.Data;

public class ListRepository
{
    private readonly DatabaseContext _context;

    public ListRepository(DatabaseContext context)
    {
        _context = context;
    }

    public List<AdvisorList> GetAllLists()
    {
        return _context.AdvisorLists
            .AsNoTracking()
            .Select(l => new AdvisorList
            {
                Id = l.Id,
                Name = l.Name,
                Description = l.Description,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt,
                MemberCount = _context.AdvisorListMembers.Count(m => m.ListId == l.Id)
            })
            .OrderBy(l => l.Name)
            .ToList();
    }

    public AdvisorList CreateList(string name, string? description = null)
    {
        var list = new AdvisorList
        {
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.AdvisorLists.Add(list);
        _context.SaveChanges();
        return list;
    }

    public void RenameList(int listId, string newName, string? description = null)
    {
        _context.AdvisorLists
            .Where(l => l.Id == listId)
            .ExecuteUpdate(s => s
                .SetProperty(l => l.Name, newName)
                .SetProperty(l => l.Description, description)
                .SetProperty(l => l.UpdatedAt, DateTime.UtcNow));
    }

    public void DeleteList(int listId)
    {
        _context.AdvisorLists
            .Where(l => l.Id == listId)
            .ExecuteDelete();
    }

    public bool AddToList(int listId, int advisorId, string? notes = null)
    {
        if (_context.AdvisorListMembers.Any(m => m.ListId == listId && m.AdvisorId == advisorId))
            return false;

        _context.AdvisorListMembers.Add(new AdvisorListMember
        {
            ListId = listId,
            AdvisorId = advisorId,
            Notes = notes,
            AddedAt = DateTime.UtcNow
        });
        _context.SaveChanges();
        return true;
    }

    public void RemoveFromList(int listId, int advisorId)
    {
        _context.AdvisorListMembers
            .Where(m => m.ListId == listId && m.AdvisorId == advisorId)
            .ExecuteDelete();
    }

    public List<Advisor> GetListMembers(int listId)
    {
        var members = _context.AdvisorListMembers
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
        return _context.AdvisorListMembers
            .AsNoTracking()
            .Where(m => m.AdvisorId == advisorId)
            .Select(m => m.ListId)
            .ToList();
    }

    public void UpdateMemberNotes(int listId, int advisorId, string? notes)
    {
        _context.AdvisorListMembers
            .Where(m => m.ListId == listId && m.AdvisorId == advisorId)
            .ExecuteUpdate(s => s.SetProperty(m => m.Notes, notes));
    }
}
