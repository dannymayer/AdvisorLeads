namespace AdvisorLeads.Models;

public class Firm
{
    public int Id { get; set; }
    public string CrdNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? BusinessType { get; set; }
    public bool IsRegisteredWithSec { get; set; }
    public bool IsRegisteredWithFinra { get; set; }
    public int? NumberOfAdvisors { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public string? Source { get; set; }
    public bool IsExcluded { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public override string ToString() => Name;
}
