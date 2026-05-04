using LIS.Data;
using LIS.Models;
using Microsoft.EntityFrameworkCore;

namespace LIS.Services;

public class ReportService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly EmailService _emailService;
    private readonly AuditLogService _auditLogService;

    public ReportService(IDbContextFactory<ApplicationDbContext> contextFactory, EmailService emailService, AuditLogService auditLogService)
    {
        _contextFactory = contextFactory;
        _emailService = emailService;
        _auditLogService = auditLogService;
    }

    public async Task<(List<Report> Reports, int TotalCount)> GetReportsAsync(ReportFilter filter)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = BuildReportQuery(context, filter);

        var totalCount = await query.CountAsync();

        var reports = await query
            .OrderByDescending(r => r.UpdatedAt)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (reports, totalCount);
    }

    public async Task<Dictionary<ReportStatus, int>> GetStatusCountsAsync(ReportFilter filter)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var counts = await BuildReportQuery(context, filter, includeStatusFilter: false)
            .GroupBy(r => r.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();

        return counts.ToDictionary(x => x.Key, x => x.Count);
    }

    public async Task<Report?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Reports
            .Include(r => r.Hospital)
            .Include(r => r.Doctor)
            .Include(r => r.Patient)
            .Include(r => r.Test)
            .Include(r => r.TestResults.OrderBy(tr => tr.SortOrder))
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Report?> GetByIdAsync(int id, ApplicationUser actor)
    {
        var report = await GetByIdAsync(id);
        if (report == null || !CanAccessReport(actor, report))
            return null;
        if (actor.Role == UserRole.Doctor && report.Status != ReportStatus.Approved)
            return null;
        return report;
    }

    public async Task<Report?> GetByReferenceNumberAsync(string referenceNumber)
    {
        if (string.IsNullOrWhiteSpace(referenceNumber))
            return null;

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Reports
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.ReferenceNumber == referenceNumber.Trim());
    }

    public async Task<Report> CreateAsync(Report report, List<TestResult> testResults, ApplicationUser actor)
    {
        EnsureLabStaff(actor);
        EnsureHospitalAccess(actor, report.HospitalId);

        await using var context = await _contextFactory.CreateDbContextAsync();
        report.ReferenceNumber = string.IsNullOrWhiteSpace(report.ReferenceNumber)
            ? await GenerateReferenceNumberAsync()
            : report.ReferenceNumber.Trim();
        report.CreatedByUserId = actor.Id;
        report.UpdatedByUserId = actor.Id;
        report.CreatedAt = DateTime.UtcNow;
        report.UpdatedAt = DateTime.UtcNow;
        report.SubmittedAt = null;
        report.ApprovedAt = null;
        report.ArchivedAt = null;
        report.Status = ReportStatus.Draft;

        context.Reports.Add(report);
        await context.SaveChangesAsync();

        foreach (var tr in NormalizeTestResults(testResults))
        {
            tr.ReportId = report.Id;
            context.TestResults.Add(tr);
        }

        await context.SaveChangesAsync();
        await _auditLogService.WriteAsync("ReportCreated", "Report", report.Id.ToString(), actor, after: new
        {
            report.ReferenceNumber,
            Status = report.Status.ToString(),
            report.PatientId,
            report.DoctorId,
            report.HospitalId
        });
        return report;
    }

    public async Task<Report> UpdateAsync(Report report, List<TestResult> testResults, ApplicationUser actor)
    {
        EnsureLabStaff(actor);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Reports
            .Include(r => r.TestResults)
            .FirstOrDefaultAsync(r => r.Id == report.Id)
            ?? throw new KeyNotFoundException("Report not found.");

        EnsureReportAccess(actor, existing);

        if (existing.Status != ReportStatus.Draft)
            throw new InvalidOperationException("Only draft reports can be edited.");

        EnsureHospitalAccess(actor, report.HospitalId);

        existing.ReferenceNumber = string.IsNullOrWhiteSpace(report.ReferenceNumber)
            ? existing.ReferenceNumber
            : report.ReferenceNumber.Trim();
        existing.HospitalId = report.HospitalId;
        existing.DoctorId = report.DoctorId;
        existing.PatientId = report.PatientId;
        existing.TestId = report.TestId;
        existing.SpecimenType = report.SpecimenType;
        existing.SampleCollectionDate = report.SampleCollectionDate;
        existing.ReceivedAtLabDate = report.ReceivedAtLabDate;
        existing.ReportingDate = report.ReportingDate;
        existing.ReportFilePath = string.IsNullOrWhiteSpace(report.ReportFilePath)
            ? existing.ReportFilePath
            : report.ReportFilePath;
        existing.UpdatedByUserId = actor.Id;
        existing.UpdatedAt = DateTime.UtcNow;

        context.TestResults.RemoveRange(existing.TestResults);
        foreach (var tr in NormalizeTestResults(testResults))
        {
            tr.ReportId = existing.Id;
            context.TestResults.Add(tr);
        }

        await context.SaveChangesAsync();
        await _auditLogService.WriteAsync("ReportUpdated", "Report", existing.Id.ToString(), actor, after: new
        {
            existing.ReferenceNumber,
            Status = existing.Status.ToString(),
            existing.PatientId,
            existing.DoctorId,
            existing.HospitalId
        });
        return existing;
    }

    public async Task<bool> DeleteAsync(int id, ApplicationUser actor)
    {
        if (actor.Role != UserRole.SuperAdmin)
            return false;

        await using var context = await _contextFactory.CreateDbContextAsync();
        var report = await context.Reports.FindAsync(id);
        if (report == null)
            return false;

        context.Reports.Remove(report);
        await context.SaveChangesAsync();
        await _auditLogService.WriteAsync("ReportDeleted", "Report", report.Id.ToString(), actor, metadata: new { report.ReferenceNumber, Status = report.Status.ToString() });
        return true;
    }

    public async Task<Report?> SubmitForReviewAsync(int id, ApplicationUser actor)
    {
        EnsureLabStaff(actor);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var report = await context.Reports.FindAsync(id);
        if (report == null)
            return null;

        EnsureReportAccess(actor, report);
        report.SubmitForReview();
        report.UpdatedByUserId = actor.Id;

        await context.SaveChangesAsync();
        await _auditLogService.WriteAsync("ReportSubmittedForReview", "Report", report.Id.ToString(), actor, metadata: new { report.ReferenceNumber });
        return report;
    }

    public async Task<Report?> ApproveAsync(int id, ApplicationUser actor)
    {
        EnsureLabStaff(actor);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var report = await context.Reports.FindAsync(id);
        if (report == null)
            return null;

        EnsureReportAccess(actor, report);
        report.Approve();
        report.UpdatedByUserId = actor.Id;

        await context.SaveChangesAsync();

        var fullReport = await GetByIdAsync(report.Id);
        if (fullReport != null)
            await _emailService.SendReportCompletedEmailAsync(fullReport);

        await _auditLogService.WriteAsync("ReportApproved", "Report", report.Id.ToString(), actor, metadata: new { report.ReferenceNumber });

        return report;
    }

    public async Task<Report?> RejectAsync(int id, ApplicationUser actor)
    {
        EnsureLabStaff(actor);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var report = await context.Reports.FindAsync(id);
        if (report == null)
            return null;

        EnsureReportAccess(actor, report);
        report.RejectToDraft();
        report.UpdatedByUserId = actor.Id;
        await context.SaveChangesAsync();
        await _auditLogService.WriteAsync("ReportRejected", "Report", report.Id.ToString(), actor, metadata: new { report.ReferenceNumber });
        return report;
    }

    public async Task<Report?> ArchiveAsync(int id, ApplicationUser actor)
    {
        EnsureLabStaff(actor);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var report = await context.Reports.FindAsync(id);
        if (report == null)
            return null;

        EnsureReportAccess(actor, report);
        report.Archive();
        report.UpdatedByUserId = actor.Id;
        await context.SaveChangesAsync();
        await _auditLogService.WriteAsync("ReportArchived", "Report", report.Id.ToString(), actor, metadata: new { report.ReferenceNumber });
        return report;
    }

    private static IQueryable<Report> BuildReportQuery(ApplicationDbContext context, ReportFilter filter, bool includeStatusFilter = true)
    {
        var query = context.Reports
            .Include(r => r.Hospital)
            .Include(r => r.Doctor)
            .Include(r => r.Patient)
            .Include(r => r.Test)
            .Include(r => r.TestResults)
            .AsQueryable();

        query = ApplyAccessScope(query, filter);

        // Doctors only consume released results; drafts and pipeline statuses stay with lab roles.
        if (filter.UserRole == UserRole.Doctor)
            query = query.Where(r => r.Status == ReportStatus.Approved);

        if (filter.FilterHospitalId.HasValue)
            query = query.Where(r => r.HospitalId == filter.FilterHospitalId.Value);
        else if (!string.IsNullOrWhiteSpace(filter.HospitalSearch))
            query = query.Where(r => r.Hospital.Name.Contains(filter.HospitalSearch));

        if (filter.FilterDoctorId.HasValue)
            query = query.Where(r => r.DoctorId == filter.FilterDoctorId.Value);
        else if (!string.IsNullOrWhiteSpace(filter.DoctorSearch))
            query = query.Where(r => r.Doctor.Name.Contains(filter.DoctorSearch));

        if (!string.IsNullOrWhiteSpace(filter.PatientName))
            query = query.Where(r => r.Patient.Name.Contains(filter.PatientName));

        if (!string.IsNullOrWhiteSpace(filter.NRIC))
            query = query.Where(r => r.Patient.NRIC != null && r.Patient.NRIC.Contains(filter.NRIC));

        if (!string.IsNullOrWhiteSpace(filter.MRN))
            query = query.Where(r => r.Patient.MRN != null && r.Patient.MRN.Contains(filter.MRN));

        if (filter.FilterTestId.HasValue)
            query = query.Where(r => r.TestId == filter.FilterTestId.Value);

        if (includeStatusFilter && filter.Status.HasValue)
            query = query.Where(r => r.Status == filter.Status.Value);

        if (filter.DateFrom.HasValue)
            query = query.Where(r => r.CreatedAt >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            query = query.Where(r => r.CreatedAt <= filter.DateTo.Value.AddDays(1));

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(r =>
                r.ReferenceNumber.ToLower().Contains(term) ||
                r.Patient.Name.ToLower().Contains(term) ||
                r.Doctor.Name.ToLower().Contains(term) ||
                r.Hospital.Name.ToLower().Contains(term) ||
                r.Test.Name.ToLower().Contains(term) ||
                (r.Patient.NRIC != null && r.Patient.NRIC.ToLower().Contains(term)) ||
                (r.Patient.MRN != null && r.Patient.MRN.ToLower().Contains(term)));
        }

        return query;
    }

    private static IQueryable<Report> ApplyAccessScope(IQueryable<Report> query, ReportFilter filter)
    {
        if (filter.UserRole == UserRole.SuperAdmin)
            return query;

        var hospitalIds = GetAccessibleHospitalIds(filter);
        if (hospitalIds.Count > 0)
            query = query.Where(r => hospitalIds.Contains(r.HospitalId));

        if (filter.UserRole == UserRole.Doctor)
        {
            // Doctor scoping supports both DoctorId and doctor email to handle duplicate doctor rows.
            return query.Where(r =>
                (filter.DoctorId.HasValue && r.DoctorId == filter.DoctorId.Value) ||
                (!string.IsNullOrWhiteSpace(filter.UserEmail) && r.Doctor.Email != null && r.Doctor.Email == filter.UserEmail) ||
                (!string.IsNullOrWhiteSpace(filter.UserId) && r.CreatedByUserId == filter.UserId));
        }

        return query;
    }

    private static bool DoctorHasReportAccess(ApplicationUser actor, Report report)
    {
        if (!GetAccessibleHospitalIds(actor).Contains(report.HospitalId))
            return false;

        if (actor.DoctorId.HasValue && report.DoctorId == actor.DoctorId.Value)
            return true;

        if (!string.IsNullOrWhiteSpace(actor.Email) &&
            !string.IsNullOrWhiteSpace(report.Doctor?.Email) &&
            string.Equals(report.Doctor.Email, actor.Email, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(report.CreatedByUserId, actor.Id, StringComparison.Ordinal);
    }

    private static bool CanAccessReport(ApplicationUser actor, Report report)
    {
        if (!actor.IsActive)
            return false;

        return actor.Role switch
        {
            UserRole.SuperAdmin => true,
            UserRole.Doctor => DoctorHasReportAccess(actor, report),
            UserRole.LabAdmin or UserRole.HeadNurse or UserRole.LabManager => GetAccessibleHospitalIds(actor).Contains(report.HospitalId),
            _ => false
        };
    }

    private static IReadOnlyList<int> GetAccessibleHospitalIds(ReportFilter filter)
    {
        if (filter.AccessibleHospitalIds.Count > 0)
            return filter.AccessibleHospitalIds;

        if (filter.HospitalId.HasValue)
            return [filter.HospitalId.Value];

        return Array.Empty<int>();
    }

    private static IReadOnlyList<int> GetAccessibleHospitalIds(ApplicationUser actor)
    {
        var ids = actor.UserHospitals
            .Select(uh => uh.HospitalId)
            .Distinct()
            .ToList();

        if (ids.Count == 0 && actor.HospitalId.HasValue)
            ids.Add(actor.HospitalId.Value);

        return ids;
    }

    private static void EnsureHospitalAccess(ApplicationUser actor, int hospitalId)
    {
        if (actor.Role == UserRole.SuperAdmin)
            return;

        if (!GetAccessibleHospitalIds(actor).Contains(hospitalId))
            throw new UnauthorizedAccessException("You do not have access to this hospital.");
    }

    private static void EnsureReportAccess(ApplicationUser actor, Report report)
    {
        if (!CanAccessReport(actor, report))
            throw new UnauthorizedAccessException("You do not have access to this report.");
    }

    private static IEnumerable<TestResult> NormalizeTestResults(IEnumerable<TestResult> testResults)
    {
        var ordered = testResults
            .Where(tr => !string.IsNullOrWhiteSpace(tr.TestName) || !string.IsNullOrWhiteSpace(tr.Result) || !string.IsNullOrWhiteSpace(tr.ResultDetail))
            .Select((tr, index) => new TestResult
            {
                TestName = tr.TestName?.Trim() ?? string.Empty,
                Result = tr.Result?.Trim() ?? string.Empty,
                ResultDetail = tr.ResultDetail?.Trim(),
                SortOrder = index + 1
            })
            .ToList();

        if (ordered.Count != 0)
            return ordered;

        return
        [
            new TestResult
            {
                TestName = "Result",
                Result = "Pending",
                SortOrder = 1
            }
        ];
    }

    private static void EnsureLabStaff(ApplicationUser actor)
    {
        if (actor.Role is not (UserRole.SuperAdmin or UserRole.LabAdmin))
            throw new UnauthorizedAccessException("Only laboratory administrators can modify reports.");
    }

    private async Task<string> GenerateReferenceNumberAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var lastReport = await context.Reports
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();

        var nextNumber = 1;
        if (lastReport != null)
        {
            var digits = new string(lastReport.ReferenceNumber.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            if (int.TryParse(digits, out var lastNumber))
                nextNumber = lastNumber + 1;
        }

        return $"GF/REP_{nextNumber:D4}";
    }
}

public class ReportFilter
{
    public UserRole UserRole { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public int? DoctorId { get; set; }
    public int? HospitalId { get; set; }
    public List<int> AccessibleHospitalIds { get; set; } = new();

    public int? FilterHospitalId { get; set; }
    public int? FilterDoctorId { get; set; }
    /// <summary>Partial match on hospital name (UI filter). Ignored when <see cref="FilterHospitalId"/> is set.</summary>
    public string? HospitalSearch { get; set; }
    /// <summary>Partial match on doctor name (UI filter). Ignored when <see cref="FilterDoctorId"/> is set.</summary>
    public string? DoctorSearch { get; set; }
    public string? PatientName { get; set; }
    public string? NRIC { get; set; }
    public string? MRN { get; set; }
    public int? FilterTestId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? SearchTerm { get; set; }
    public ReportStatus? Status { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public int ActiveFilterCount
    {
        get
        {
            var count = 0;
            if (FilterHospitalId.HasValue) count++;
            else if (!string.IsNullOrWhiteSpace(HospitalSearch)) count++;
            if (FilterDoctorId.HasValue) count++;
            else if (!string.IsNullOrWhiteSpace(DoctorSearch)) count++;
            if (!string.IsNullOrWhiteSpace(PatientName)) count++;
            if (!string.IsNullOrWhiteSpace(NRIC)) count++;
            if (!string.IsNullOrWhiteSpace(MRN)) count++;
            if (FilterTestId.HasValue) count++;
            if (DateFrom.HasValue) count++;
            if (DateTo.HasValue) count++;
            if (!string.IsNullOrWhiteSpace(SearchTerm)) count++;
            return count;
        }
    }
}



