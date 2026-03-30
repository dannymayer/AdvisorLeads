namespace AdvisorLeads.Models;

public class DataQualityReport
{
    public DateTime GeneratedAt { get; set; }
    public TimeSpan AnalysisDuration { get; set; }
    public int TotalAdvisors { get; set; }
    public int TotalFirms { get; set; }

    public int DuplicateAdvisorGroupCount { get; set; }
    public int DuplicateFirmGroupCount { get; set; }
    public int NormalizationIssueCount { get; set; }
    public int OrphanedRecordCount { get; set; }
    public int InconsistentRelationshipCount { get; set; }

    public List<DuplicateGroup> DuplicateAdvisors { get; set; } = new();
    public List<DuplicateGroup> DuplicateFirms { get; set; } = new();
    public List<CleaningIssue> NormalizationIssues { get; set; } = new();
    public List<CleaningIssue> OrphanedRecords { get; set; } = new();
    public List<CleaningIssue> InconsistentRelationships { get; set; } = new();

    public int TotalIssues => DuplicateAdvisorGroupCount + DuplicateFirmGroupCount
        + NormalizationIssueCount + OrphanedRecordCount + InconsistentRelationshipCount;
}

public class DuplicateGroup
{
    public string GroupKey { get; set; } = string.Empty;
    public DuplicateReason Reason { get; set; }
    public CleaningEntityType EntityType { get; set; }
    public List<int> EntityIds { get; set; } = new();
    public List<string> Names { get; set; } = new();
    public int SuggestedKeepId { get; set; }
    public string SuggestedKeepReason { get; set; } = string.Empty;
}

public enum DuplicateReason
{
    ExactCrdMatch,
    NameFirmStateMatch,
    NameRegistrationDateMatch,
    NullCrdSimilarName
}

public class CleaningIssue
{
    private static int _nextId;
    public int IssueId { get; set; } = Interlocked.Increment(ref _nextId);
    public CleaningIssueType Type { get; set; }
    public CleaningEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public string SuggestedValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CleaningIssueSeverity Severity { get; set; }
    public bool IsAutoFixable { get; set; }
}

public enum CleaningIssueType
{
    NormalizationNeeded,
    OrphanedRecord,
    InconsistentRelationship,
    ImplausibleValue,
    MissingRequiredField,
    UnknownEnumValue
}

public enum CleaningEntityType
{
    Advisor,
    Firm,
    EmploymentHistory,
    Disclosure,
    Qualification,
    AdvisorListMember
}

public enum CleaningIssueSeverity
{
    High,
    Medium,
    Low
}
