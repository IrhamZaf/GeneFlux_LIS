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
            new EmailService(Options.Create(new EmailSettings { Enabled = false }), NullLogger<EmailService>.Instance),
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
