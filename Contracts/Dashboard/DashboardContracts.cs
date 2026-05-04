using LIS.Models;

namespace LIS.Contracts.Dashboard;

public class DashboardResponse
{
    public DashboardStatsDto Stats { get; set; } = new();
    public List<DashboardRecentReportDto> RecentReports { get; set; } = new();
    public List<DashboardRecentPatientDto> RecentPatients { get; set; } = new();
    public List<DashboardRecentActivityDto> RecentActivity { get; set; } = new();
    public DashboardScopeDto Scope { get; set; } = new();
    public List<DashboardTestUsageDto> TopTests { get; set; } = new();
    public List<DashboardStatusBreakdownDto> StatusBreakdown { get; set; } = new();
    public List<DashboardMonthlyTrendDto> MonthlyTrend { get; set; } = new();
    public List<DashboardHospitalVolumeDto> HospitalVolume { get; set; } = new();
}

public class DashboardTestUsageDto
{
    public string TestName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DashboardStatusBreakdownDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DashboardMonthlyTrendDto
{
    public string Month { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DashboardHospitalVolumeDto
{
    public string HospitalName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DashboardStatsDto
{
    public int TotalHospitals { get; set; }
    public int TotalUsers { get; set; }
    public int TotalPatients { get; set; }
    public int TotalReports { get; set; }
    public int PendingReports { get; set; }
    public int CompletedReports { get; set; }
}

public class DashboardRecentReportDto
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string HospitalName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public ReportStatus Status { get; set; }
    public DateTime ActivityAt { get; set; }
}

public class DashboardRecentPatientDto
{
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
    public string HospitalName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public DateTime LastVisitAt { get; set; }
    public string LatestReferenceNumber { get; set; } = string.Empty;
}

public class DashboardRecentActivityDto
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime ActivityAt { get; set; }
    public string Href { get; set; } = string.Empty;
}

public class DashboardScopeDto
{
    public string Role { get; set; } = string.Empty;
    public int? SelectedHospitalId { get; set; }
    public string ScopeLabel { get; set; } = string.Empty;
    public List<DashboardHospitalOptionDto> Hospitals { get; set; } = new();
}

public class DashboardHospitalOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
