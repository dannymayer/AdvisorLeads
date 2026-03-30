namespace AdvisorLeads.Abstractions;

public interface IRefreshStep
{
    string Name { get; }
    int OrderIndex { get; }
    Task ExecuteAsync(IProgress<string>? progress, CancellationToken token);
}
