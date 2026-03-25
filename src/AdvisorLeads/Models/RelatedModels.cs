namespace AdvisorLeads.Models;

public class EmploymentHistory
{
    public int Id { get; set; }
    public int AdvisorId { get; set; }
    public string FirmName { get; set; } = string.Empty;
    public string? FirmCrd { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent => EndDate == null;
    public string? Position { get; set; }
}

public class Disclosure
{
    public int Id { get; set; }
    public int AdvisorId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? Date { get; set; }
    public string? Resolution { get; set; }
    public string? Sanctions { get; set; }
    public string? Source { get; set; }
}

public class Qualification
{
    public int Id { get; set; }
    public int AdvisorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public DateTime? Date { get; set; }
    public string? Status { get; set; }
}
