namespace LIS.Models;

public class Report
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    
    public int HospitalId { get; set; }
    public Hospital Hospital { get; set; } = null!;
    
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    
    public int TestId { get; set; }
    public Test Test { get; set; } = null!;
    
    public string? SpecimenType { get; set; }
    public DateTime? SampleCollectionDate { get; set; }
    public DateTime? ReceivedAtLabDate { get; set; }
    public DateTime? ReportingDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    
    public ReportStatus Status { get; set; } = ReportStatus.Draft;
    
    public string? ReportFilePath { get; set; }
    /// <summary>Original browser filename the user uploaded (e.g. "GF_EP_279.pdf"). Stored separately from the disk path which uses a UUID name.</summary>
    public string? ReportOriginalFileName { get; set; }
    
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    
    public string? UpdatedByUserId { get; set; }
    public ApplicationUser? UpdatedByUser { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<TestResult> TestResults { get; set; } = new List<TestResult>();

    public bool CanTransitionTo(ReportStatus targetStatus)
    {
        return (Status, targetStatus) switch
        {
            (ReportStatus.Draft, ReportStatus.PendingReview) => true,
            (ReportStatus.PendingReview, ReportStatus.Approved) => true,
            (ReportStatus.PendingReview, ReportStatus.Draft) => true,
            (ReportStatus.Approved, ReportStatus.Archived) => true,
            _ => false
        };
    }

    public void SubmitForReview()
    {
        EnsureTransition(ReportStatus.PendingReview);
        Status = ReportStatus.PendingReview;
        SubmittedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Approve()
    {
        EnsureTransition(ReportStatus.Approved);
        Status = ReportStatus.Approved;
        ApprovedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RejectToDraft()
    {
        EnsureTransition(ReportStatus.Draft);
        Status = ReportStatus.Draft;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        EnsureTransition(ReportStatus.Archived);
        Status = ReportStatus.Archived;
        ArchivedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureTransition(ReportStatus targetStatus)
    {
        if (!CanTransitionTo(targetStatus))
            throw new InvalidOperationException($"Invalid report transition from {Status} to {targetStatus}.");
    }
}
