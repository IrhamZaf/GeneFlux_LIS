using LIS.Data;
using LIS.Models;
using LIS.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LIS.Tests;

public class PatientServiceResolveTests
{
    [Fact]
    public async Task Resolve_matches_nric_globally_even_when_first_seen_at_another_hospital()
    {
        var dbName = $"lis_{Guid.NewGuid():N}";
        var factory = new TestDbContextFactory(dbName);
        await using (var ctx = factory.CreateDbContext())
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Hospitals.AddRange(
                new Hospital { Name = "Hospital A", IsActive = true },
                new Hospital { Name = "Hospital B", IsActive = true });
            ctx.Users.Add(new ApplicationUser
            {
                Id = "user-1",
                UserName = "t@t.com",
                NormalizedUserName = "T@T.COM",
                Email = "t@t.com",
                NormalizedEmail = "T@T.COM",
                EmailConfirmed = true,
                FullName = "Tester",
                Role = UserRole.SuperAdmin,
                SecurityStamp = "sec",
                ConcurrencyStamp = "con",
                PasswordHash = "x"
            });
            ctx.Patients.Add(new Patient
            {
                Name = "Existing",
                NRIC = "390731-02-5057",
                Sex = Sex.Male,
                IdentityType = IdentityType.NRIC,
                MRN = "OLD-MRN"
            });
            await ctx.SaveChangesAsync();

            var ha = await ctx.Hospitals.FirstAsync(h => h.Name == "Hospital A");
            ctx.Doctors.Add(new Doctor { Name = "Doc A", HospitalId = ha.Id, IsActive = true });
            ctx.Tests.Add(new Test { Name = "Panel", IsActive = true });
            await ctx.SaveChangesAsync();

            var p = await ctx.Patients.SingleAsync();
            ctx.Reports.Add(new Report
            {
                ReferenceNumber = "REF-A1",
                HospitalId = ha.Id,
                DoctorId = (await ctx.Doctors.SingleAsync()).Id,
                PatientId = p.Id,
                TestId = (await ctx.Tests.SingleAsync()).Id,
                CreatedByUserId = "user-1",
                Status = ReportStatus.Draft
            });
            await ctx.SaveChangesAsync();
        }

        var hbId = await factory.CreateDbContext().Hospitals
            .Where(h => h.Name == "Hospital B")
            .Select(h => h.Id)
            .SingleAsync();

        var svc = new PatientService(factory);
        var resolved = await svc.ResolveOrCreatePatientForReportAsync(new Patient
        {
            Name = "Renamed From Upload",
            NRIC = "390731025057",
            Sex = Sex.Male,
            IdentityType = IdentityType.NRIC,
            MRN = "NEW-MRN"
        }, hbId);

        Assert.Equal("390731-02-5057", resolved.NRIC);
        Assert.Equal("NEW-MRN", resolved.MRN);

        await using (var ctx = factory.CreateDbContext())
            Assert.Equal(1, await ctx.Patients.CountAsync());
    }

    [Fact]
    public async Task Resolve_mrn_only_links_when_prior_report_exists_at_same_hospital()
    {
        var dbName = $"lis_{Guid.NewGuid():N}";
        var factory = new TestDbContextFactory(dbName);
        int h1Id;
        int h2Id;
        int originalPatientId;

        await using (var ctx = factory.CreateDbContext())
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Hospitals.AddRange(
                new Hospital { Name = "Site One", IsActive = true },
                new Hospital { Name = "Site Two", IsActive = true });
            ctx.Users.Add(new ApplicationUser
            {
                Id = "user-1",
                UserName = "t@t.com",
                NormalizedUserName = "T@T.COM",
                Email = "t@t.com",
                NormalizedEmail = "T@T.COM",
                EmailConfirmed = true,
                FullName = "Tester",
                Role = UserRole.SuperAdmin,
                SecurityStamp = "sec",
                ConcurrencyStamp = "con",
                PasswordHash = "x"
            });
            ctx.Patients.Add(new Patient
            {
                Name = "Pat",
                MRN = "18L193519",
                Sex = Sex.Male,
                IdentityType = IdentityType.Passport
            });
            await ctx.SaveChangesAsync();
            originalPatientId = (await ctx.Patients.SingleAsync()).Id;

            h1Id = (await ctx.Hospitals.FirstAsync(h => h.Name == "Site One")).Id;
            h2Id = (await ctx.Hospitals.FirstAsync(h => h.Name == "Site Two")).Id;

            ctx.Doctors.Add(new Doctor { Name = "D1", HospitalId = h1Id, IsActive = true });
            ctx.Doctors.Add(new Doctor { Name = "D2", HospitalId = h2Id, IsActive = true });
            ctx.Tests.Add(new Test { Name = "PCR", IsActive = true });
            await ctx.SaveChangesAsync();

            ctx.Reports.Add(new Report
            {
                ReferenceNumber = "REF-S1",
                HospitalId = h1Id,
                DoctorId = (await ctx.Doctors.FirstAsync(d => d.HospitalId == h1Id)).Id,
                PatientId = originalPatientId,
                TestId = (await ctx.Tests.SingleAsync()).Id,
                CreatedByUserId = "user-1",
                Status = ReportStatus.Draft
            });
            await ctx.SaveChangesAsync();
        }

        var svc = new PatientService(factory);

        var atH1 = await svc.ResolveOrCreatePatientForReportAsync(new Patient
        {
            Name = "Pat Updated",
            MRN = "18l193519",
            Sex = Sex.Male,
            IdentityType = IdentityType.Passport
        }, h1Id);

        Assert.Equal(originalPatientId, atH1.Id);

        await svc.ResolveOrCreatePatientForReportAsync(new Patient
        {
            Name = "Other hospital same label",
            MRN = "18L193519",
            Sex = Sex.Male,
            IdentityType = IdentityType.Passport
        }, h2Id);

        await using (var ctx = factory.CreateDbContext())
            Assert.Equal(2, await ctx.Patients.CountAsync());
    }
}
