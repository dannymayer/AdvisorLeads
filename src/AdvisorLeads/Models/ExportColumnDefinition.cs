namespace AdvisorLeads.Models;

public record ExportColumnDefinition<T>(
    string Key,
    string Header,
    Func<T, object?> Selector
);
