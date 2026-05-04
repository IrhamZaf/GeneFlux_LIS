using LIS.Data;
using LIS.Models;
using LIS.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LIS.Tests;

/// <summary>
/// Smoke-style check: extraction rules against representative Geneflux-style plain text (same labels as current PDF layout).
/// </summary>
public class ReportUploadGenefluxTextTests
{
    public const string SampleGenefluxPlainText =
        """
        Geneflux reference number: GF/EP_279
        Hospital name: PREMIER INTEGRATED LABS CHERAS
        Doctor's name: DR. WONG POH KIM
        Patient's R/N number: 18L193519
        Patient's Name: KUA BOON SENG @ QUAH HOOI SENG
        Identity Card number: 390731-02-5057
        Age (years)/Sex: 85 YEARS / MALE
        Date & Time of Sample Collection: 25/03/2025 AT 04:00 pm
        Date & Time Received at the Laboratory: 25/03/2025 AT 07:00 pm
        Specimen Type: EYE FLUID
        Test Method: Real Time PCR
        EYE PANEL DIAGNOSTIC TEST
        EYE Panel Real Time PCR Test Results:
        CMV DNA DETECTED
        VZV DNA NOT DETECTED
        HSV DNA NOT DETECTED
        Reporting Time & Date: 04:00 pm, 26/03/2025
        """;

    public const string SampleGenefluxPdfPigText =
        """
        LABORATORY INVESTIGATION TEST REPORT
        Geneflux reference number                    : GF/EP_279
        Hospital name                               : PREMIER INTEGRATED LABS CHERAS
        Doctor’s name                              : DR. WONG POH KIM
        Patient’s R/N number                      : 18L193519
        Patient’s Name                            : KUA BOON SENG @ QUAH HOOI SENG
        Identity Card number                      : 390731-02-5057
        Age (years)/Sex                           : 85 YEARS / MALE
        Date & Time of Sample Collection          : 25/03/2025 AT 04:00 pm
        Date & Time Received at the Laboratory    : 25/03/2025 AT 07:00 pm
        Specimen Type                             : EYE FLUID
        Test Method                               : Real Time PCR
        EYE PANEL
        DIAGNOSTIC TEST
        EYE Panel Real Time PCR Test Results:
        CMV DNA DETECTED CMV Viral Load: 149,245 IU/mL
        VZV DNA NOT DETECTED
        HSV DNA NOT DETECTED
        Reporting Time & Date:
        04:00 pm, 26/03/2025
        """;

    /// <summary>
    /// Simulates what PdfPig actually outputs when "EYE PANEL" and "DIAGNOSTIC TEST" are in a
    /// two-column header table: PdfPig interleaves them with the company-name column.
    /// "Reporting Time &amp; Date:" value is also on the same line (no newline).
    /// All tests are seeded to reproduce the "BK &amp; JC" wrong-pick bug in production.
    /// </summary>
    public const string SampleGenefluxActualPdfPigText =
        """
        EYE PANEL GENEFLUX DIAGNOSTICS SDN BHD (891567-M)
        DIAGNOSTIC TEST G1 & G2, Menara KLH, Bandar Puchong Jaya, 47100 Puchong, Selangor.
        Tel: 603-8070 1154 Fax: 603-8070 3654
        LABORATORY INVESTIGATION TEST REPORT
        Geneflux reference number : GF/EP_279
        Hospital name : PREMIER INTEGRATED LABS CHERAS
        Doctor's name : DR. WONG POH KIM
        Patient's R/N number : 18L193519
        Patient's Name : KUA BOON SENG @ QUAH HOOI SENG
        Identity Card number : 390731-02-5057
        Age (years)/Sex : 85 YEARS / MALE
        Date & Time of Sample Collection : 25/03/2025 AT 04:00 pm
        Date & Time Received at the Laboratory : 25/03/2025 AT 07:00 pm
        Specimen Type : EYE FLUID
        Test Method : Real Time PCR
        EYE Panel Real Time PCR Test Results:
        CMV DNA DETECTED CMV Viral Load: 149,245 IU/mL
        VZV DNA NOT DETECTED
        HSV DNA NOT DETECTED
        Reporting Time & Date: 04:00 pm, 26/03/2025
        """;

    [Fact]
    public async Task ParseExtractedText_populates_core_fields_for_geneflux_layout()
    {
        var dbName = $"lis_upload_{Guid.NewGuid():N}";
        var factory = new TestDbContextFactory(dbName);
        await using (var ctx = factory.CreateDbContext())
        {
            await ctx.Database.EnsureCreatedAsync();
            var hospital = new Hospital { Name = "PREMIER INTEGRATED LABS CHERAS", IsActive = true };
            ctx.Hospitals.Add(hospital);
            await ctx.SaveChangesAsync();

            ctx.Doctors.Add(new Doctor
            {
                Name = "DR. WONG POH KIM",
                HospitalId = hospital.Id,
                IsActive = true
            });
            ctx.Tests.Add(new Test { Name = "EYE PANEL DIAGNOSTIC TEST (Real Time PCR)", IsActive = true });
            await ctx.SaveChangesAsync();
        }

        var dropdownService = new DropdownService(factory, new AuditLogService(factory));
        var uploadService = new ReportUploadService(new FakeWebHostEnvironment(), dropdownService);

        var data = await uploadService.ParseExtractedTextAsync(SampleGenefluxPlainText);

        Assert.Equal("GF/EP_279", data.ReferenceNumber);
        Assert.Equal("PREMIER INTEGRATED LABS CHERAS", data.HospitalName);
        Assert.Contains("KUA BOON SENG", data.PatientName ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Equal("390731-02-5057", data.NricOrPassport);
        Assert.Equal("18L193519", data.Mrn);
        Assert.Equal(Sex.Male, data.Sex);
        Assert.Equal("EYE FLUID", data.SpecimenType);
        Assert.NotNull(data.CollectedDate);
        Assert.Equal(25, data.CollectedDate!.Value.Day);
        Assert.NotNull(data.ReceivedDate);
        Assert.NotNull(data.ReportDate);
        Assert.Equal(26, data.ReportDate!.Value.Day);

        Assert.NotNull(data.HospitalId);
        Assert.NotNull(data.DoctorId);
        Assert.NotNull(data.TestId);
        Assert.NotEmpty(data.TestResults);
        Assert.Contains(data.TestResults, r =>
            r.Result.Contains("DETECTED", StringComparison.OrdinalIgnoreCase) &&
            r.TestName.Replace(" ", "", StringComparison.OrdinalIgnoreCase).Contains("CMV", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseExtractedText_populates_core_fields_from_pdfpig_spaced_geneflux_layout()
    {
        var dbName = $"lis_upload_{Guid.NewGuid():N}";
        var factory = new TestDbContextFactory(dbName);
        await using (var ctx = factory.CreateDbContext())
        {
            await ctx.Database.EnsureCreatedAsync();
            var hospital = new Hospital { Name = "PREMIER INTEGRATED LABS CHERAS", IsActive = true };
            ctx.Hospitals.Add(hospital);
            await ctx.SaveChangesAsync();

            ctx.Doctors.Add(new Doctor
            {
                Name = "Dr. Wong Poh Kim",
                HospitalId = hospital.Id,
                IsActive = true
            });
            ctx.Tests.Add(new Test { Name = "Eye Panel 1 (EP1)", TestMethod = "Real Time PCR", IsActive = true });
            await ctx.SaveChangesAsync();
        }

        var dropdownService = new DropdownService(factory, new AuditLogService(factory));
        var uploadService = new ReportUploadService(new FakeWebHostEnvironment(), dropdownService);

        var data = await uploadService.ParseExtractedTextAsync(SampleGenefluxPdfPigText);

        Assert.Equal("GF/EP_279", data.ReferenceNumber);
        Assert.Equal("KUA BOON SENG @ QUAH HOOI SENG", data.PatientName);
        Assert.Equal("18L193519", data.Mrn);
        Assert.Equal("390731-02-5057", data.NricOrPassport);
        Assert.Equal("EYE FLUID", data.SpecimenType);
        Assert.NotNull(data.DoctorId);
        Assert.NotNull(data.TestId);
        Assert.Equal(new DateTime(2025, 3, 26, 16, 0, 0), data.ReportDate);
    }

    /// <summary>
    /// Reproduces the production bug: PdfPig interleaves the two-column header so
    /// "EYE PANEL" and "DIAGNOSTIC TEST" are not adjacent. All production tests are seeded
    /// so the old token-matching fallback would pick "BK &amp; JC" before "Eye Panel 1".
    /// Also verifies that when "Reporting Time &amp; Date:" has the time on the same line
    /// (not the next line) it is still parsed correctly.
    /// </summary>
    [Fact]
    public async Task ParseExtractedText_handles_actual_pdfpig_interleaved_header_and_inline_date()
    {
        var dbName = $"lis_upload_{Guid.NewGuid():N}";
        var factory = new TestDbContextFactory(dbName);
        await using (var ctx = factory.CreateDbContext())
        {
            await ctx.Database.EnsureCreatedAsync();
            var hospital = new Hospital { Name = "PREMIER INTEGRATED LABS CHERAS", IsActive = true };
            ctx.Hospitals.Add(hospital);
            await ctx.SaveChangesAsync();

            ctx.Doctors.Add(new Doctor { Name = "Dr. Wong Poh Kim", HospitalId = hospital.Id, IsActive = true });

            // Seed ALL production tests to reproduce the wrong-pick scenario
            ctx.Tests.AddRange(
                new Test { Name = "Respiratory Pathogen Panel 36 (RPP36)", TestMethod = "Real Time PCR", IsActive = true },
                new Test { Name = "BK & JC (BKJC)", TestMethod = "Real Time PCR", IsActive = true },
                new Test { Name = "Cytomegalovirus (CMV)", TestMethod = "Real Time PCR", IsActive = true },
                new Test { Name = "Eye Panel 1 (EP1)", TestMethod = "Real Time PCR", IsActive = true },
                new Test { Name = "Eye Panel 2 (EP2)", TestMethod = "Real Time PCR", IsActive = true }
            );
            await ctx.SaveChangesAsync();
        }

        var dropdownService = new DropdownService(factory, new AuditLogService(factory));
        var uploadService = new ReportUploadService(new FakeWebHostEnvironment(), dropdownService);

        var data = await uploadService.ParseExtractedTextAsync(SampleGenefluxActualPdfPigText);

        // Test name must NOT be "BK & JC" — it must be an Eye Panel test
        Assert.NotNull(data.TestId);
        var ctx2 = factory.CreateDbContext();
        var pickedTest = await ctx2.Tests.FindAsync(data.TestId);
        Assert.NotNull(pickedTest);
        Assert.Contains("eye panel", pickedTest!.Name, StringComparison.OrdinalIgnoreCase);

        // Reporting date must have both date AND time from the same-line value
        Assert.Equal(new DateTime(2025, 3, 26, 16, 0, 0), data.ReportDate);
    }
}
