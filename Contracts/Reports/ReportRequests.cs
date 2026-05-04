using LIS.Models;

namespace LIS.Contracts.Reports;

public class CreateReportRequest
{
    public int HospitalId { get; set; }
    public int DoctorId { get; set; }
    public int TestId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public IdentityType IdentityType { get; set; } = IdentityType.NRIC;
    public string? Nric { get; set; }
    public string? PassportNo { get; set; }
    public string? Mrn { get; set; }
    public Sex Sex { get; set; }
    public string? SpecimenType { get; set; }
    public DateTime? SampleCollectionDate { get; set; }
    public DateTime? ReceivedAtLabDate { get; set; }
    public List<ReportResultRequest> Results { get; set; } = new();
}

public class UpdateReportRequest : CreateReportRequest
{
}

public class ChangeReportStatusRequest
{
    public ReportStatus TargetStatus { get; set; }
}

public class ReportResultRequest
{
    public string TestName { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string? ResultDetail { get; set; }
}
