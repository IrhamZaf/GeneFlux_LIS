using System.Linq;
using LIS.Contracts.Administration;
using LIS.Data;
using LIS.Models;
using Microsoft.EntityFrameworkCore;

namespace LIS.Services;

public class AdministrationService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly AuditLogService _auditLogService;
    private readonly CurrentUserService _currentUserService;

    public AdministrationService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        AuditLogService auditLogService,
        CurrentUserService currentUserService)
    {
        _contextFactory = contextFactory;
        _auditLogService = auditLogService;
        _currentUserService = currentUserService;
    }

    public async Task<List<HospitalListItemDto>> GetHospitalsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Hospitals
            .OrderBy(h => h.Name)
            .Select(h => new HospitalListItemDto
            {
                Id = h.Id,
                Name = h.Name,
                Address = h.Address,
                ContactNumber = h.ContactNumber,
                ContactEmail = h.ContactEmail,
                LogoPath = h.LogoPath,
                IsActive = h.IsActive,
                TotalUsers = h.UserHospitals.Select(uh => uh.UserId).Distinct().Count(),
                TotalPatients = h.Reports.Select(r => r.PatientId).Distinct().Count(),
                TotalReports = h.Reports.Count()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<HospitalDetailsDto?> GetHospitalDetailsAsync(int hospitalId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var hospital = await context.Hospitals.FirstOrDefaultAsync(h => h.Id == hospitalId, cancellationToken);
        if (hospital == null)
            return null;

        var totalDoctors = await context.UserHospitals
            .Where(uh => uh.HospitalId == hospitalId && uh.User.Role == UserRole.Doctor)
            .Select(uh => uh.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalPatients = await context.Reports
            .Where(r => r.HospitalId == hospitalId)
            .Select(r => r.PatientId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalReports = await context.Reports
            .CountAsync(r => r.HospitalId == hospitalId, cancellationToken);

        // Two-query + in-memory counts: EF often mis-translates correlated Count() after Distinct(),
        // and reports may reference a Doctor row that is linked only by CreatedByUserId or email
        // rather than Doctor.UserId / ApplicationUser.DoctorId.
        var assignedUserIds = await context.UserHospitals
            .Where(uh => uh.HospitalId == hospitalId)
            .Select(uh => uh.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var usersList = await context.Users
            .Where(u => assignedUserIds.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);

        var reportRows = await context.Reports
            .AsNoTracking()
            .Where(r => r.HospitalId == hospitalId)
            .Select(r => new ReportMatchRow(
                r.DoctorId,
                r.Doctor!.UserId,
                r.Doctor!.Email,
                r.CreatedByUserId))
            .ToListAsync(cancellationToken);

        var users = usersList
            .Select(u => new HospitalUserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                Role = u.Role,
                IsActive = u.IsActive,
                TotalReports = CountReportsForAssignedUser(reportRows, u)
            })
            .ToList();

        var topTestRows = await context.Reports
            .Where(r => r.HospitalId == hospitalId)
            .GroupBy(r => r.TestId)
            .Select(g => new { TestId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        var testIds = topTestRows.Select(x => x.TestId).ToList();
        var testNames = await context.Tests
            .Where(t => testIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var topTests = topTestRows.Select(x => new HospitalTestUsageDto
        {
            TestName = testNames.GetValueOrDefault(x.TestId) ?? "Unknown test",
            Count = x.Count,
            Percent = totalReports > 0 ? Math.Round(100.0 * x.Count / totalReports, 1) : 0
        }).ToList();

        return new HospitalDetailsDto
        {
            Hospital = hospital,
            TotalDoctors = totalDoctors,
            TotalPatients = totalPatients,
            TotalReports = totalReports,
            Users = users,
            TopTests = topTests
        };
    }

    public async Task<List<HospitalPatientDto>> GetHospitalPatientsAsync(int hospitalId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Reports
            .Where(r => r.HospitalId == hospitalId)
            .GroupBy(r => r.PatientId)
            .Select(g => new
            {
                PatientId = g.Key,
                TotalReports = g.Count(),
                LastReportDate = g.Max(r => r.UpdatedAt)
            })
            .Join(context.Patients,
                g => g.PatientId,
                p => p.Id,
                (g, p) => new HospitalPatientDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    IdentityType = p.IdentityType,
                    IdentityNumber = p.IdentityType == IdentityType.NRIC ? p.NRIC : p.PassportNo,
                    MRN = p.MRN,
                    Sex = p.Sex,
                    TotalReports = g.TotalReports,
                    LastReportDate = g.LastReportDate
                })
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<PatientDetailDto?> GetPatientDetailAsync(int patientId, int hospitalId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var patient = await context.Patients.FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        if (patient == null)
            return null;

        var reports = await context.Reports
            .Where(r => r.PatientId == patientId && r.HospitalId == hospitalId)
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r => new PatientReportDto
            {
                Id = r.Id,
                ReferenceNumber = r.ReferenceNumber,
                TestName = r.Test.Name,
                DoctorName = r.Doctor.Name,
                Status = r.Status,
                ReportingDate = r.ReportingDate,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var doctorsSummary = string.Join(", ",
            reports.Select(r => r.DoctorName.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n));

        return new PatientDetailDto
        {
            Id = patient.Id,
            Name = patient.Name,
            IdentityType = patient.IdentityType,
            NRIC = patient.NRIC,
            PassportNo = patient.PassportNo,
            MRN = patient.MRN,
            Sex = patient.Sex,
            DateOfBirth = patient.DateOfBirth,
            DoctorsSummary = string.IsNullOrWhiteSpace(doctorsSummary) ? null : doctorsSummary,
            Reports = reports
        };
    }

    /// <summary>
    /// Patient detail for patient-directory users: same demographics as admin view, but report rows are limited to reports the viewer may access at this hospital.
    /// </summary>
    public async Task<PatientDetailDto?> GetPatientDetailForViewerAsync(
        int patientId,
        int hospitalId,
        ApplicationUser viewer,
        CancellationToken cancellationToken = default)
    {
        if (viewer.Role is not (UserRole.Doctor or UserRole.HeadNurse or UserRole.LabManager))
            return null;

        if (!_currentUserService.GetAccessibleHospitalIds(viewer).Contains(hospitalId))
            return null;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var patient = await context.Patients.FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        if (patient == null)
            return null;

        var query = context.Reports
            .AsNoTracking()
            .Where(r => r.PatientId == patientId && r.HospitalId == hospitalId);

        if (viewer.Role == UserRole.Doctor)
        {
            var email = viewer.Email?.Trim();
            query = query.Where(r =>
                r.Status == ReportStatus.Approved &&
                ((viewer.DoctorId.HasValue && r.DoctorId == viewer.DoctorId.Value) ||
                 (!string.IsNullOrWhiteSpace(email) &&
                  r.Doctor.Email != null &&
                  r.Doctor.Email.Trim().ToLower() == email.ToLower()) ||
                 r.CreatedByUserId == viewer.Id));
        }
        else
        {
            query = query.Where(r => r.Status == ReportStatus.Approved);
        }

        var reports = await query
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r => new PatientReportDto
            {
                Id = r.Id,
                ReferenceNumber = r.ReferenceNumber,
                TestName = r.Test.Name,
                DoctorName = r.Doctor.Name,
                Status = r.Status,
                ReportingDate = r.ReportingDate,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var doctorsSummary = string.Join(", ",
            reports.Select(r => r.DoctorName.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n));

        return new PatientDetailDto
        {
            Id = patient.Id,
            Name = patient.Name,
            IdentityType = patient.IdentityType,
            NRIC = patient.NRIC,
            PassportNo = patient.PassportNo,
            MRN = patient.MRN,
            Sex = patient.Sex,
            DateOfBirth = patient.DateOfBirth,
            DoctorsSummary = string.IsNullOrWhiteSpace(doctorsSummary) ? null : doctorsSummary,
            Reports = reports
        };
    }

    public async Task<Hospital> CreateHospitalAsync(CreateHospitalRequest request, ApplicationUser actor, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var hospital = new Hospital
        {
            Name = request.Name.Trim(),
            Address = request.Address,
            ContactNumber = request.ContactNumber,
            ContactEmail = request.ContactEmail,
            LogoPath = request.LogoPath,
            IsActive = request.IsActive
        };

        context.Hospitals.Add(hospital);
        await context.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync("HospitalCreated", "Hospital", hospital.Id.ToString(), actor, after: hospital);
        return hospital;
    }

    public async Task<bool> UpdateHospitalAsync(int id, UpdateHospitalRequest request, ApplicationUser actor, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var hospital = await context.Hospitals.FindAsync([id], cancellationToken);
        if (hospital == null)
            return false;

        var before = new
        {
            hospital.Name,
            hospital.Address,
            hospital.ContactNumber,
            hospital.ContactEmail,
            hospital.LogoPath,
            hospital.IsActive
        };

        hospital.Name = request.Name.Trim();
        hospital.Address = request.Address;
        hospital.ContactNumber = request.ContactNumber;
        hospital.ContactEmail = request.ContactEmail;
        hospital.LogoPath = request.LogoPath;
        hospital.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync("HospitalUpdated", "Hospital", hospital.Id.ToString(), actor, before, hospital);
        return true;
    }

    private sealed record ReportMatchRow(int DoctorId, string? DoctorUserId, string? DoctorEmail, string CreatedByUserId);

    private static int CountReportsForAssignedUser(IReadOnlyList<ReportMatchRow> rows, ApplicationUser u)
    {
        var n = 0;
        foreach (var r in rows)
        {
            if (ReportRowMatchesUser(r, u))
                n++;
        }

        return n;
    }

    /// <summary>
    /// Links reports to an assigned user when the usual FK paths disagree (duplicate Doctor rows,
    /// PDF extraction doctor vs catalog doctor, or uploads keyed only by CreatedByUserId).
    /// </summary>
    private static bool ReportRowMatchesUser(ReportMatchRow r, ApplicationUser u)
    {
        if (u.DoctorId.HasValue && r.DoctorId == u.DoctorId.Value)
            return true;
        if (!string.IsNullOrEmpty(r.DoctorUserId) && string.Equals(r.DoctorUserId, u.Id, StringComparison.Ordinal))
            return true;
        if (u.Role == UserRole.Doctor && string.Equals(r.CreatedByUserId, u.Id, StringComparison.Ordinal))
            return true;
        if (!string.IsNullOrWhiteSpace(u.Email) && !string.IsNullOrWhiteSpace(r.DoctorEmail)
            && string.Equals(u.Email.Trim(), r.DoctorEmail.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
