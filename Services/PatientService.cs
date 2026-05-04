using LIS.Data;
using LIS.Models;
using Microsoft.EntityFrameworkCore;

namespace LIS.Services;

public class PatientService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public PatientService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Patient?> FindByNRICAsync(string nric)
    {
        var key = PatientIdentityNormalizer.NormalizeNric(nric);
        if (string.IsNullOrEmpty(key))
            return null;

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Patients.FirstOrDefaultAsync(p => p.NRIC == key);
    }

    public async Task<Patient?> FindByPassportAsync(string passport)
    {
        var key = PatientIdentityNormalizer.NormalizePassport(passport);
        if (string.IsNullOrEmpty(key))
            return null;

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Patients.FirstOrDefaultAsync(p => p.PassportNo == key);
    }

    public async Task<Patient?> FindByMRNAsync(string mrn)
    {
        var key = PatientIdentityNormalizer.NormalizeMrn(mrn);
        if (string.IsNullOrEmpty(key))
            return null;

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Patients.FirstOrDefaultAsync(p => p.MRN == key);
    }

    public async Task<Patient> CreateOrUpdateAsync(Patient patient)
    {
        patient.NRIC = PatientIdentityNormalizer.NormalizeNric(patient.NRIC);
        patient.PassportNo = PatientIdentityNormalizer.NormalizePassport(patient.PassportNo);
        patient.MRN = PatientIdentityNormalizer.NormalizeMrn(patient.MRN);

        await using var context = await _contextFactory.CreateDbContextAsync();
        // Try to find existing patient by NRIC or Passport or MRN (canonical forms)
        Patient? existing = null;

        if (!string.IsNullOrWhiteSpace(patient.NRIC))
            existing = await context.Patients.FirstOrDefaultAsync(p => p.NRIC == patient.NRIC);
        
        if (existing == null && !string.IsNullOrWhiteSpace(patient.PassportNo))
            existing = await context.Patients.FirstOrDefaultAsync(p => p.PassportNo == patient.PassportNo);

        if (existing == null && !string.IsNullOrWhiteSpace(patient.MRN))
            existing = await context.Patients.FirstOrDefaultAsync(p => p.MRN == patient.MRN);

        if (existing != null)
        {
            existing.Name = patient.Name;
            existing.NRIC = patient.NRIC;
            existing.PassportNo = patient.PassportNo;
            existing.MRN = patient.MRN;
            existing.Sex = patient.Sex;
            existing.DateOfBirth = patient.DateOfBirth;
            existing.IdentityType = patient.IdentityType;
            context.Patients.Update(existing);
            await context.SaveChangesAsync();
            return existing;
        }

        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        return patient;
    }

    /// <summary>
    /// Resolves the patient for an uploaded report using this order (first hit wins):
    /// <list type="number">
    /// <item><description>NRIC — global match on canonical NRIC (12-digit Malaysian format with dashes).</description></item>
    /// <item><description>Passport — global match (trimmed, uppercased).</description></item>
    /// <item><description>MRN at this hospital — patient already has at least one report at <paramref name="hospitalId"/> with the same canonical MRN.</description></item>
    /// </list>
    /// If none match, inserts a new patient. MRN is hospital-specific in meaning; we do not match MRN alone across other hospitals (avoids accidental merges when two sites reuse the same label).
    /// </summary>
    public async Task<Patient> ResolveOrCreatePatientForReportAsync(Patient input, int hospitalId)
    {
        input.NRIC = PatientIdentityNormalizer.NormalizeNric(input.NRIC);
        input.PassportNo = PatientIdentityNormalizer.NormalizePassport(input.PassportNo);
        input.MRN = PatientIdentityNormalizer.NormalizeMrn(input.MRN);

        await using var context = await _contextFactory.CreateDbContextAsync();
        Patient? existing = null;

        if (!string.IsNullOrWhiteSpace(input.NRIC))
            existing = await context.Patients.FirstOrDefaultAsync(p => p.NRIC == input.NRIC);

        if (existing == null && !string.IsNullOrWhiteSpace(input.PassportNo))
            existing = await context.Patients.FirstOrDefaultAsync(p => p.PassportNo == input.PassportNo);

        if (existing == null && !string.IsNullOrWhiteSpace(input.MRN))
        {
            existing = await context.Reports
                .AsNoTracking()
                .Where(r => r.HospitalId == hospitalId && r.Patient != null && r.Patient.MRN == input.MRN)
                .Select(r => r.Patient!)
                .FirstOrDefaultAsync();
        }

        if (existing != null)
        {
            existing.Name = input.Name;
            existing.NRIC = input.NRIC;
            existing.PassportNo = input.PassportNo;
            existing.MRN = input.MRN;
            existing.Sex = input.Sex;
            existing.IdentityType = input.IdentityType;
            if (input.DateOfBirth.HasValue)
                existing.DateOfBirth = input.DateOfBirth;
            context.Patients.Update(existing);
            await context.SaveChangesAsync();
            return existing;
        }

        input.Id = 0;
        context.Patients.Add(input);
        await context.SaveChangesAsync();
        return input;
    }

    public async Task<Patient> UpdatePatientDemographicsAsync(int patientId, Patient input)
    {
        input.NRIC = PatientIdentityNormalizer.NormalizeNric(input.NRIC);
        input.PassportNo = PatientIdentityNormalizer.NormalizePassport(input.PassportNo);
        input.MRN = PatientIdentityNormalizer.NormalizeMrn(input.MRN);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Patients.FindAsync(patientId)
            ?? throw new KeyNotFoundException("Patient not found.");

        existing.Name = input.Name;
        existing.NRIC = input.NRIC;
        existing.PassportNo = input.PassportNo;
        existing.MRN = input.MRN;
        existing.Sex = input.Sex;
        existing.IdentityType = input.IdentityType;
        if (input.DateOfBirth.HasValue)
            existing.DateOfBirth = input.DateOfBirth;

        await context.SaveChangesAsync();
        return existing;
    }

    public async Task<List<Patient>> SearchAsync(string term)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Patients
            .Where(p => p.Name.Contains(term) ||
                        (p.NRIC != null && p.NRIC.Contains(term)) ||
                        (p.MRN != null && p.MRN.Contains(term)))
            .Take(10)
            .ToListAsync();
    }
}
