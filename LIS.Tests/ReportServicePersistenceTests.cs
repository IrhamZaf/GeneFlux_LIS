using LIS.Models;
using LIS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LIS.Tests;

public class ReportServicePersistenceTests
{
    [Fact]
    public async Task CreateAsync_persists_uploaded_report_metadata()
    {
        var factory = new TestDbContextFactory($"lis_report_{Guid.NewGuid():N}");
        var seed = await SeedRequiredReportDataAsync(factory);
        var service = CreateReportService(factory);
        var reportingDate = new DateTime(2025, 3, 26, 16, 0, 0);

        await service.CreateAsync(new Report
        {
            ReferenceNumber = "GF/EP_279",
            HospitalId = seed.HospitalId,
            DoctorId = seed.DoctorId,
            PatientId = seed.PatientId,
            TestId = seed.TestId,
            SpecimenType = "EYE FLUID",
            SampleCollectionDate = new DateTime(2025, 3, 25),
            ReceivedAtLabDate = new DateTime(2025, 3, 25),
            ReportingDate = reportingDate,
            ReportFilePath = "uploads/reports/sample.pdf"
        }, new List<TestResult>(), seed.Actor);

        await using var ctx = factory.CreateDbContext();
        var saved = await ctx.Reports.SingleAsync();

        Assert.Equal("GF/EP_279", saved.ReferenceNumber);
        Assert.Equal("EYE FLUID", saved.SpecimenType);
        Assert.Equal(new DateTime(2025, 3, 25), saved.SampleCollectionDate);
        Assert.Equal(new DateTime(2025, 3, 25), saved.ReceivedAtLabDate);
        Assert.Equal(reportingDate, saved.ReportingDate);
        Assert.Equal("uploads/reports/sample.pdf", saved.ReportFilePath);
    }

    [Fact]
    public async Task UpdateAsync_persists_uploaded_report_metadata_changes()
    {
        var factory = new TestDbContextFactory($"lis_report_{Guid.NewGuid():N}");
        var seed = await SeedRequiredReportDataAsync(factory);
        var service = CreateReportService(factory);
        var created = await service.CreateAsync(new Report
        {
            ReferenceNumber = "GF/EP_OLD",
            HospitalId = seed.HospitalId,
            DoctorId = seed.DoctorId,
            PatientId = seed.PatientId,
            TestId = seed.TestId,
            SpecimenType = "OLD",
            ReportingDate = new DateTime(2025, 3, 25, 12, 0, 0),
            ReportFilePath = "uploads/reports/old.pdf"
        }, new List<TestResult>(), seed.Actor);

        await service.UpdateAsync(new Report
        {
            Id = created.Id,
            ReferenceNumber = "GF/EP_279",
            HospitalId = seed.HospitalId,
            DoctorId = seed.DoctorId,
            PatientId = seed.PatientId,
            TestId = seed.TestId,
            SpecimenType = "EYE FLUID",
            SampleCollectionDate = new DateTime(2025, 3, 25),
            ReceivedAtLabDate = new DateTime(2025, 3, 25),
            ReportingDate = new DateTime(2025, 3, 26, 16, 0, 0),
            ReportFilePath = "uploads/reports/new.pdf"
        }, new List<TestResult>(), seed.Actor);

        await using var ctx = factory.CreateDbContext();
        var saved = await ctx.Reports.SingleAsync();

        Assert.Equal("GF/EP_279", saved.ReferenceNumber);
        Assert.Equal("EYE FLUID", saved.SpecimenType);
        Assert.Equal(new DateTime(2025, 3, 26, 16, 0, 0), saved.ReportingDate);
        Assert.Equal("uploads/reports/new.pdf", saved.ReportFilePath);
    }

    [Fact]
    public async Task GetByIdAsync_returns_draft_report_for_attributed_doctor()
    {
        var factory = new TestDbContextFactory($"lis_report_doc_{Guid.NewGuid():N}");
        await using var ctx = factory.CreateDbContext();
        await ctx.Database.EnsureCreatedAsync();

        const string doctorEmail = "dr.attrib@test.local";
        var hospital = new Hospital { Name = "Test Hospital", IsActive = true };
        ctx.Hospitals.Add(hospital);
        var patient = new Patient
        {
            Name = "Pat One",
            IdentityType = IdentityType.NRIC,
            NRIC = "900101-01-0001",
            MRN = "MRN1",
            Sex = Sex.Female
        };
        ctx.Patients.Add(patient);
        var test = new Test { Name = "T1", TestMethod = "PCR", IsActive = true };
        ctx.Tests.Add(test);
        await ctx.SaveChangesAsync();

        var doctor = new Doctor
        {
            Name = "Dr Test",
            HospitalId = hospital.Id,
            Email = doctorEmail,
            IsActive = true
        };
        ctx.Doctors.Add(doctor);
        await ctx.SaveChangesAsync();

        var doctorUser = new ApplicationUser
        {
            Id = "doc-user-1",
            UserName = doctorEmail,
            NormalizedUserName = doctorEmail.ToUpperInvariant(),
            Email = doctorEmail,
            NormalizedEmail = doctorEmail.ToUpperInvariant(),
            EmailConfirmed = true,
            FullName = "Dr Test",
            Role = UserRole.Doctor,
            IsActive = true,
            HospitalId = hospital.Id,
            DoctorId = doctor.Id,
            SecurityStamp = "s1",
            ConcurrencyStamp = "c1",
            PasswordHash = "x"
        };
        ctx.Users.Add(doctorUser);
        await ctx.SaveChangesAsync();

        var report = new Report
        {
            ReferenceNumber = "GF/T_DOC_1",
            HospitalId = hospital.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            TestId = test.Id,
            Status = ReportStatus.Draft,
            CreatedByUserId = doctorUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.Reports.Add(report);
        await ctx.SaveChangesAsync();

        var service = CreateReportService(factory);
        var loaded = await service.GetByIdAsync(report.Id, doctorUser);

        Assert.NotNull(loaded);
        Assert.Equal(ReportStatus.Draft, loaded.Status);
    }

    [Fact]
    public async Task GetReportsAsync_head_nurse_workspace_tabs_filter_by_status()
    {
        var factory = new TestDbContextFactory($"lis_report_hn_{Guid.NewGuid():N}");
        await using var ctx = factory.CreateDbContext();
        await ctx.Database.EnsureCreatedAsync();

        var hospital = new Hospital { Name = "HN Hospital", IsActive = true };
        ctx.Hospitals.Add(hospital);
        var patient = new Patient
        {
            Name = "Pat HN",
            IdentityType = IdentityType.NRIC,
            NRIC = "800101-01-0001",
            MRN = "M1",
            Sex = Sex.Male
        };
        ctx.Patients.Add(patient);
        var test = new Test { Name = "T", TestMethod = "PCR", IsActive = true };
        ctx.Tests.Add(test);
        await ctx.SaveChangesAsync();

        var doctor = new Doctor { Name = "Dr A", HospitalId = hospital.Id, IsActive = true };
        ctx.Doctors.Add(doctor);
        await ctx.SaveChangesAsync();

        var nurse = new ApplicationUser
        {
            Id = "nurse-1",
            UserName = "nurse@test.local",
            NormalizedUserName = "NURSE@TEST.LOCAL",
            Email = "nurse@test.local",
            NormalizedEmail = "NURSE@TEST.LOCAL",
            EmailConfirmed = true,
            FullName = "Nurse",
            Role = UserRole.HeadNurse,
            IsActive = true,
            HospitalId = hospital.Id,
            SecurityStamp = "s1",
            ConcurrencyStamp = "c1",
            PasswordHash = "x"
        };
        ctx.Users.Add(nurse);
        await ctx.SaveChangesAsync();

        var rDraft = new Report
        {
            ReferenceNumber = "R-D",
            HospitalId = hospital.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            TestId = test.Id,
            Status = ReportStatus.Draft,
            CreatedByUserId = nurse.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var rPending = new Report
        {
            ReferenceNumber = "R-P",
            HospitalId = hospital.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            TestId = test.Id,
            Status = ReportStatus.PendingReview,
            CreatedByUserId = nurse.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var rApproved = new Report
        {
            ReferenceNumber = "R-A",
            HospitalId = hospital.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            TestId = test.Id,
            Status = ReportStatus.Approved,
            CreatedByUserId = nurse.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.Reports.AddRange(rDraft, rPending, rApproved);
        await ctx.SaveChangesAsync();

        var service = CreateReportService(factory);
        var filterPending = new ReportFilter
        {
            UserRole = UserRole.HeadNurse,
            AccessibleHospitalIds = new List<int> { hospital.Id },
            DoctorWorkspaceTab = "pending",
            Page = 1,
            PageSize = 20
        };
        var (pendingList, pendingTotal) = await service.GetReportsAsync(filterPending);
        Assert.Equal(2, pendingTotal);
        Assert.Equal(2, pendingList.Count);

        var filterCompleted = new ReportFilter
        {
            UserRole = UserRole.HeadNurse,
            AccessibleHospitalIds = new List<int> { hospital.Id },
            DoctorWorkspaceTab = "completed",
            Page = 1,
            PageSize = 20
        };
        var (completedList, completedTotal) = await service.GetReportsAsync(filterCompleted);
        Assert.Equal(1, completedTotal);
        Assert.Single(completedList);
        Assert.Equal(ReportStatus.Approved, completedList[0].Status);
    }

    [Fact]
    public async Task ApproveAsync_does_not_overwrite_pdf_reporting_date()
    {
        var factory = new TestDbContextFactory($"lis_report_{Guid.NewGuid():N}");
        var seed = await SeedRequiredReportDataAsync(factory);
        var service = CreateReportService(factory);
        var reportingDate = new DateTime(2025, 3, 26, 16, 0, 0);
        var created = await service.CreateAsync(new Report
        {
            ReferenceNumber = "GF/EP_279",
            HospitalId = seed.HospitalId,
            DoctorId = seed.DoctorId,
            PatientId = seed.PatientId,
            TestId = seed.TestId,
            ReportingDate = reportingDate,
            ReportFilePath = "uploads/reports/sample.pdf"
        }, new List<TestResult>(), seed.Actor);

        await service.SubmitForReviewAsync(created.Id, seed.Actor);
        await service.ApproveAsync(created.Id, seed.Actor);

        await using var ctx = factory.CreateDbContext();
        var saved = await ctx.Reports.SingleAsync();

        Assert.Equal(ReportStatus.Approved, saved.Status);
        Assert.Equal(reportingDate, saved.ReportingDate);
        Assert.NotNull(saved.ApprovedAt);
    }

    private static ReportService CreateReportService(TestDbContextFactory factory) =>
        new(
            factory,
            new EmailService(Options.Create(new EmailSettings { Enabled = false }), Options.Create(new AppSiteSettings()), NullLogger<EmailService>.Instance),
            new AuditLogService(factory));

    private static async Task<(int HospitalId, int DoctorId, int PatientId, int TestId, ApplicationUser Actor)> SeedRequiredReportDataAsync(TestDbContextFactory factory)
    {
        await using var ctx = factory.CreateDbContext();
        await ctx.Database.EnsureCreatedAsync();

        var actor = new ApplicationUser
        {
            Id = "user-1",
            UserName = "admin@geneflux.com",
            NormalizedUserName = "ADMIN@GENEFLUX.COM",
            Email = "admin@geneflux.com",
            NormalizedEmail = "ADMIN@GENEFLUX.COM",
            EmailConfirmed = true,
            FullName = "Admin",
            Role = UserRole.SuperAdmin,
            IsActive = true,
            SecurityStamp = "sec",
            ConcurrencyStamp = "con",
            PasswordHash = "x"
        };
        var hospital = new Hospital { Name = "PREMIER INTEGRATED LABS CHERAS", IsActive = true };
        var patient = new Patient { Name = "KUA BOON SENG @ QUAH HOOI SENG", IdentityType = IdentityType.NRIC, NRIC = "390731-02-5057", MRN = "18L193519", Sex = Sex.Male };
        var test = new Test { Name = "Eye Panel 1 (EP1)", TestMethod = "Real Time PCR", IsActive = true };

        ctx.Users.Add(actor);
        ctx.Hospitals.Add(hospital);
        ctx.Patients.Add(patient);
        ctx.Tests.Add(test);
        await ctx.SaveChangesAsync();

        var doctor = new Doctor { Name = "Dr. Wong Poh Kim", HospitalId = hospital.Id, IsActive = true };
        ctx.Doctors.Add(doctor);
        await ctx.SaveChangesAsync();

        return (hospital.Id, doctor.Id, patient.Id, test.Id, actor);
    }
}
