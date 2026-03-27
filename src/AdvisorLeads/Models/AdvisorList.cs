namespace AdvisorLeads.Models;

public class AdvisorList
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MemberCount { get; set; }  // populated by query, not stored
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public override string ToString() => Name;
}

public class AdvisorListMember
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public int AdvisorId { get; set; }
    public string? Notes { get; set; }
    public DateTime AddedAt { get; set; }

    // Navigation — populated by queries
    public Advisor? Advisor { get; set; }
}
