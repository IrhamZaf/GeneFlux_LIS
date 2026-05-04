using LIS.Contracts.Dashboard;
using LIS.Data;
using LIS.Models;
using Microsoft.EntityFrameworkCore;

namespace LIS.Services;

public class DashboardService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly CurrentUserService _currentUserService;

    public DashboardService(IDbContextFactory<ApplicationDbContext> contextFactory, CurrentUserService currentUserService)
    {
        _contextFactory = contextFactory;
        _currentUserService = currentUserService;
    }

    public async Task<DashboardResponse> GetDashboardAsync(ApplicationUser user, int? hospitalId = null, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var scopedReports = BuildScopedReportQuery(context, user, hospitalId);
        var scope = await BuildScopeAsync(context, user, hospitalId, cancellationToken);

        var stats = new DashboardStatsDto
        {
            TotalHospitals = user.Role == UserRole.SuperAdmin
                ? await context.Hospitals.CountAsync(h => h.IsActive, cancellationToken)
                : scope.Hospitals.Count,
            TotalUsers = await GetUserCountAsync(context, user, hospitalId, cancellationToken),
            TotalPatients = await scopedReports.Select(r => r.PatientId).Distinct().CountAsync(cancellationToken),
            TotalReports = await scopedReports.CountAsync(cancellationToken),
            PendingReports = await scopedReports.CountAsync(r => r.Status == ReportStatus.PendingReview, cancellationToken),
            CompletedReports = await scopedReports.CountAsync(r => r.Status == ReportStatus.Approved || r.Status == ReportStatus.Archived, cancellationToken)
        };

        var recentReports = await scopedReports
            .Include(r => r.Patient)
            .Include(r => r.Hospital)
            .Include(r => r.Doctor)
            .OrderByDescending(r => r.UpdatedAt)
            .ThenByDescending(r => r.CreatedAt)
            .Take(5)
            .Select(r => new DashboardRecentReportDto
            {
                Id = r.Id,
                ReferenceNumber = r.ReferenceNumber,
                PatientName = r.Patient.Name,
                HospitalName = r.Hospital.Name,
                DoctorName = r.Doctor.Name,
                Status = r.Status,
                ActivityAt = r.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var recentPatients = await scopedReports
            .Include(r => r.Patient)
            .Include(r => r.Hospital)
            .Include(r => r.Doctor)
            .OrderByDescending(r => r.UpdatedAt)
            .ThenByDescending(r => r.CreatedAt)
            .GroupBy(r => r.PatientId)
            .Select(g => g
                .OrderByDescending(r => r.UpdatedAt)
                .ThenByDescending(r => r.CreatedAt)
                .Select(r => new DashboardRecentPatientDto
                {
                    PatientId = r.PatientId,
                    PatientName = r.Patient.Name,
                    IdentityNumber = r.Patient.IdentityType == IdentityType.NRIC ? (r.Patient.NRIC ?? "-") : (r.Patient.PassportNo ?? "-"),
                    HospitalName = r.Hospital.Name,
                    DoctorName = r.Doctor.Name,
                    LastVisitAt = r.UpdatedAt,
                    LatestReferenceNumber = r.ReferenceNumber
                })
                .First())
            .Take(5)
            .ToListAsync(cancellationToken);

        var recentActivity = recentReports
            .Select(report => new DashboardRecentActivityDto
            {
                Type = "Report",
                Title = $"{report.PatientName} report updated",
                Detail = $"{report.ReferenceNumber} · {GetStatusLabel(report.Status)}",
                ActivityAt = report.ActivityAt,
                Href = "/reports"
            })
            .Concat(recentPatients.Select(patient => new DashboardRecentActivityDto
            {
                Type = "Patient",
                Title = $"{patient.PatientName} record touched",
                Detail = $"{patient.HospitalName} · {patient.LatestReferenceNumber}",
                ActivityAt = patient.LastVisitAt,
                Href = "/patients"
            }))
            .OrderByDescending(item => item.ActivityAt)
            .Take(6)
            .ToList();

        var topTests = await scopedReports
            .GroupBy(r => r.TestId)
            .Select(g => new { TestId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .Join(context.Tests, x => x.TestId, t => t.Id,
                (x, t) => new DashboardTestUsageDto { TestName = t.Name, Count = x.Count })
            .ToListAsync(cancellationToken);

        var statusBreakdown = await scopedReports
            .GroupBy(r => r.Status)
            .Select(g => new DashboardStatusBreakdownDto
            {
                Label = g.Key.ToString(),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-5);
        var firstOfRange = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1);
        var monthlyTrend = await scopedReports
            .Where(r => r.CreatedAt >= firstOfRange)
            .GroupBy(r => new { r.CreatedAt.Year, r.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var monthlyTrendSorted = Enumerable.Range(0, 6)
            .Select(i => firstOfRange.AddMonths(i))
            .Select(d => new DashboardMonthlyTrendDto
            {
                Month = d.ToString("MMM yyyy"),
                Count = monthlyTrend.FirstOrDefault(m => m.Year == d.Year && m.Month == d.Month)?.Count ?? 0
            })
            .ToList();

        var hospitalVolume = await scopedReports
            .GroupBy(r => r.HospitalId)
            .Select(g => new { HospitalId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .Join(context.Hospitals, x => x.HospitalId, h => h.Id,
                (x, h) => new DashboardHospitalVolumeDto { HospitalName = h.Name, Count = x.Count })
            .ToListAsync(cancellationToken);

        return new DashboardResponse
        {
            Stats = stats,
            RecentReports = recentReports,
            RecentPatients = recentPatients,
            RecentActivity = recentActivity,
            Scope = scope,
            TopTests = topTests,
            StatusBreakdown = statusBreakdown,
            MonthlyTrend = monthlyTrendSorted,
            HospitalVolume = hospitalVolume
        };
    }

    public async Task<DashboardStatsDto> GetStatsAsync(ApplicationUser user, int? hospitalId = null, CancellationToken cancellationToken = default)
    {
        return (await GetDashboardAsync(user, hospitalId, cancellationToken)).Stats;
    }

    public async Task<List<DashboardRecentReportDto>> GetRecentReportsAsync(ApplicationUser user, int? hospitalId = null, CancellationToken cancellationToken = default)
    {
        return (await GetDashboardAsync(user, hospitalId, cancellationToken)).RecentReports;
    }

    public async Task<List<DashboardRecentPatientDto>> GetRecentPatientsAsync(ApplicationUser user, int? hospitalId = null, CancellationToken cancellationToken = default)
    {
        return (await GetDashboardAsync(user, hospitalId, cancellationToken)).RecentPatients;
    }

    private IQueryable<Report> BuildScopedReportQuery(ApplicationDbContext context, ApplicationUser user, int? hospitalId)
    {
        var query = context.Reports.AsQueryable();
        var accessibleHospitalIds = _currentUserService.GetAccessibleHospitalIds(user);

        if (user.Role != UserRole.SuperAdmin)
        {
            if (accessibleHospitalIds.Count > 0)
                query = query.Where(r => accessibleHospitalIds.Contains(r.HospitalId));

            if (user.Role == UserRole.Doctor)
            {
                query = query.Where(r =>
                    (user.DoctorId.HasValue && r.DoctorId == user.DoctorId.Value) ||
                    (!string.IsNullOrWhiteSpace(user.Email) && r.Doctor.Email != null && r.Doctor.Email == user.Email) ||
                    r.CreatedByUserId == user.Id);
                query = query.Where(r => r.Status == ReportStatus.Approved);
            }
        }
        else if (hospitalId.HasValue)
        {
            query = query.Where(r => r.HospitalId == hospitalId.Value);
        }

        if (user.Role != UserRole.SuperAdmin && hospitalId.HasValue)
        {
            if (accessibleHospitalIds.Contains(hospitalId.Value))
                query = query.Where(r => r.HospitalId == hospitalId.Value);
            else
                query = query.Where(_ => false);
        }

        return query;
    }

    private async Task<DashboardScopeDto> BuildScopeAsync(ApplicationDbContext context, ApplicationUser user, int? requestedHospitalId, CancellationToken cancellationToken)
    {
        var hospitals = user.Role == UserRole.SuperAdmin
            ? await context.Hospitals
                .Where(h => h.IsActive)
                .OrderBy(h => h.Name)
                .Select(h => new DashboardHospitalOptionDto
                {
                    Id = h.Id,
                    Name = h.Name
                })
                .ToListAsync(cancellationToken)
            : _currentUserService.GetAccessibleHospitals(user)
                .Select(h => new DashboardHospitalOptionDto
                {
                    Id = h.Id,
                    Name = h.Name
                })
                .ToList();

        var selectedHospitalId = user.Role == UserRole.SuperAdmin
            ? requestedHospitalId
            : _currentUserService.GetDefaultHospitalId(user);

        return new DashboardScopeDto
        {
            Role = user.Role.ToString(),
            SelectedHospitalId = selectedHospitalId,
            ScopeLabel = GetScopeLabel(user, hospitals.Count),
            Hospitals = hospitals
        };
    }

    private static string GetScopeLabel(ApplicationUser user, int hospitalCount)
    {
        return user.Role switch
        {
            UserRole.Doctor => "Personal dashboard",
            UserRole.HeadNurse or UserRole.LabManager or UserRole.LabAdmin when hospitalCount > 1 => "Assigned hospitals",
            UserRole.HeadNurse or UserRole.LabManager or UserRole.LabAdmin => "Scoped operational overview",
            UserRole.SuperAdmin => "System-wide overview",
            _ => "System overview"
        };
    }

    private async Task<int> GetUserCountAsync(ApplicationDbContext context, ApplicationUser user, int? hospitalId, CancellationToken cancellationToken)
    {
        if (user.Role == UserRole.SuperAdmin)
        {
            if (!hospitalId.HasValue)
                return await context.Users.CountAsync(u => u.IsActive, cancellationToken);

            return await context.UserHospitals
                .Where(uh => uh.HospitalId == hospitalId.Value && uh.User.IsActive)
                .Select(uh => uh.UserId)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        var accessibleHospitalIds = _currentUserService.GetAccessibleHospitalIds(user);
        if (hospitalId.HasValue && !accessibleHospitalIds.Contains(hospitalId.Value))
            return 0;

        var scopedHospitalIds = hospitalId.HasValue ? new[] { hospitalId.Value } : accessibleHospitalIds;
        return await context.UserHospitals
            .Where(uh => scopedHospitalIds.Contains(uh.HospitalId) && uh.User.IsActive)
            .Select(uh => uh.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    private static string GetStatusLabel(ReportStatus status) => status switch
    {
        ReportStatus.PendingReview => "Pending Review",
        _ => status.ToString()
    };
}

