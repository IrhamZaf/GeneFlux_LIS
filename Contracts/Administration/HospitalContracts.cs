using LIS.Models;

namespace LIS.Contracts.Administration;

public class CreateHospitalRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactNumber { get; set; }
    public string? ContactEmail { get; set; }
    public string? LogoPath { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateHospitalRequest : CreateHospitalRequest
{
}

public class HospitalListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactNumber { get; set; }
    public string? ContactEmail { get; set; }
    public string? LogoPath { get; set; }
    public bool IsActive { get; set; }
    public int TotalUsers { get; set; }
    public int TotalPatients { get; set; }
    public int TotalReports { get; set; }
}

public class HospitalDetailsDto
{
    public Hospital Hospital { get; set; } = new();
    public int TotalDoctors { get; set; }
    public int TotalPatients { get; set; }
    public int TotalReports { get; set; }
    public List<HospitalUserDto> Users { get; set; } = new();
    /// <summary>Most frequently ordered tests at this hospital (by report count).</summary>
    public List<HospitalTestUsageDto> TopTests { get; set; } = new();
}

public class HospitalTestUsageDto
{
    public string TestName { get; set; } = string.Empty;
    public int Count { get; set; }
    /// <summary>Share of all reports at this hospital (0–100).</summary>
    public double Percent { get; set; }
}

public class HospitalUserDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public int TotalReports { get; set; }
}

public class HospitalPatientDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IdentityType IdentityType { get; set; }
    public string? IdentityNumber { get; set; }
    public string? MRN { get; set; }
    public Sex Sex { get; set; }
    public int TotalReports { get; set; }
    public DateTime LastReportDate { get; set; }
}

public class PatientDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IdentityType IdentityType { get; set; }
    public string? NRIC { get; set; }
    public string? PassportNo { get; set; }
    public string? MRN { get; set; }
    public Sex Sex { get; set; }
    public DateTime? DateOfBirth { get; set; }
    /// <summary>Distinct attending doctors from reports at this hospital (for summary header).</summary>
    public string? DoctorsSummary { get; set; }
    public List<PatientReportDto> Reports { get; set; } = new();
}

public class PatientReportDto
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public ReportStatus Status { get; set; }
    public DateTime? ReportingDate { get; set; }
    public DateTime UpdatedAt { get; set; }
}
