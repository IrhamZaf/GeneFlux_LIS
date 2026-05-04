using LIS.Data;
using LIS.Models;
using Microsoft.EntityFrameworkCore;

namespace LIS.Services;

public class DropdownService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly AuditLogService _auditLogService;

    public DropdownService(IDbContextFactory<ApplicationDbContext> contextFactory, AuditLogService auditLogService)
    {
        _contextFactory = contextFactory;
        _auditLogService = auditLogService;
    }

    public async Task<List<Hospital>> GetHospitalsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Hospitals.Where(h => h.IsActive).OrderBy(h => h.Name).ToListAsync();
    }

    public async Task<List<Doctor>> GetDoctorsAsync(int? hospitalId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Doctors.Include(d => d.Hospital).Where(d => d.IsActive).AsQueryable();
        if (hospitalId.HasValue)
            query = query.Where(d => d.HospitalId == hospitalId.Value);
        return await query.OrderBy(d => d.Name).ToListAsync();
    }

    /// <summary>
    /// Doctors for a hospital dropdown. When <paramref name="alwaysIncludeDoctorId"/> is set, that doctor is
    /// appended if missing — e.g. the report references a doctor whose home <see cref="Doctor.HospitalId"/> differs from the report hospital.
    /// </summary>
    public async Task<List<Doctor>> GetDoctorsForHospitalDropdownAsync(int hospitalId, int? alwaysIncludeDoctorId = null)
    {
        var list = await GetDoctorsAsync(hospitalId);
        if (!alwaysIncludeDoctorId.HasValue || alwaysIncludeDoctorId.Value <= 0)
            return list;

        if (list.Exists(d => d.Id == alwaysIncludeDoctorId.Value))
            return list;

        var extra = await GetDoctorByIdAsync(alwaysIncludeDoctorId.Value);
        if (extra == null)
            return list;

        return list.Concat(new[] { extra }).OrderBy(d => d.Name).ToList();
    }

    public async Task<Doctor?> GetDoctorByIdAsync(int doctorId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Doctors
            .Include(d => d.Hospital)
            .FirstOrDefaultAsync(d => d.Id == doctorId);
    }

    public async Task<List<Test>> GetTestsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Tests.Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<List<DropdownValue>> GetDropdownValuesAsync(string category)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.DropdownValues
            .Where(d => d.Category == category && d.IsActive)
            .OrderBy(d => d.SortOrder)
            .ToListAsync();
    }

    public async Task<DropdownValue> CreateDropdownValueAsync(DropdownValue value)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.DropdownValues.Add(value);
        await context.SaveChangesAsync();
        return value;
    }

    public async Task UpdateDropdownValueAsync(DropdownValue value)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.DropdownValues.Update(value);
        await context.SaveChangesAsync();
    }

    public async Task DeleteDropdownValueAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var value = await context.DropdownValues.FindAsync(id);
        if (value != null)
        {
            value.IsActive = false;
            await context.SaveChangesAsync();
        }
    }

    public async Task<Hospital> CreateHospitalAsync(Hospital hospital, ApplicationUser? actor = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Hospitals.Add(hospital);
        await context.SaveChangesAsync();
        await _auditLogService.WriteAsync("HospitalCreated", "Hospital", hospital.Id.ToString(), actor, after: hospital);
        return hospital;
    }

    public async Task UpdateHospitalAsync(Hospital hospital, ApplicationUser? actor = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Hospitals.AsNoTracking().FirstOrDefaultAsync(h => h.Id == hospital.Id);
        context.Hospitals.Update(hospital);
        await context.SaveChangesAsync();
        await _auditLogService.WriteAsync("HospitalUpdated", "Hospital", hospital.Id.ToString(), actor, existing, hospital);
    }

    public async Task DeleteHospitalAsync(int id, ApplicationUser? actor = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var hospital = await context.Hospitals.FindAsync(id);
        if (hospital != null)
        {
            var before = new { hospital.Name, hospital.IsActive };
            hospital.IsActive = false;
            await context.SaveChangesAsync();
            await _auditLogService.WriteAsync("HospitalDeactivated", "Hospital", hospital.Id.ToString(), actor, before, new { hospital.Name, hospital.IsActive });
        }
    }

    public async Task<Doctor> CreateDoctorAsync(Doctor doctor, ApplicationUser? actor = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var emailError = await GetDoctorEmailUniquenessErrorAsync(context, doctor.Email, excludeDoctorId: null);
        if (emailError != null)
            throw new InvalidOperationException(emailError);

        context.Doctors.Add(doctor);
        await context.SaveChangesAsync();
        await _auditLogService.WriteAsync("DoctorCreated", "Doctor", doctor.Id.ToString(), actor, after: doctor);
        return doctor;
    }

    public async Task UpdateDoctorAsync(Doctor doctor, ApplicationUser? actor = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var emailError = await GetDoctorEmailUniquenessErrorAsync(context, doctor.Email, doctor.Id);
        if (emailError != null)
            throw new InvalidOperationException(emailError);

        var existing = await context.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == doctor.Id);
        context.Doctors.Update(doctor);
        await context.SaveChangesAsync();
        await _auditLogService.WriteAsync("DoctorUpdated", "Doctor", doctor.Id.ToString(), actor, existing, doctor);
    }

    /// <summary>
    /// Non-empty doctor emails must not collide with another doctor or a user account (except the doctor's linked user when updating).
    /// </summary>
    private static async Task<string?> GetDoctorEmailUniquenessErrorAsync(
        ApplicationDbContext context,
        string? email,
        int? excludeDoctorId)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim();
        var lower = normalized.ToLowerInvariant();

        var doctorTaken = await context.Doctors.AnyAsync(d =>
            d.Email != null &&
            !string.IsNullOrWhiteSpace(d.Email) &&
            d.Email.Trim().ToLower() == lower &&
            (!excludeDoctorId.HasValue || d.Id != excludeDoctorId.Value));
        if (doctorTaken)
            return "Another doctor already uses this email.";

        var linkedUserId = excludeDoctorId.HasValue
            ? await context.Doctors.AsNoTracking()
                .Where(d => d.Id == excludeDoctorId.Value)
                .Select(d => d.UserId)
                .FirstOrDefaultAsync()
            : null;

        var userTaken = await context.Users.AnyAsync(u =>
            u.Email != null &&
            u.Email.Trim().ToLower() == lower &&
            (linkedUserId == null || u.Id != linkedUserId));

        if (userTaken)
            return "This email is already used by a user account.";

        return null;
    }

    public async Task DeleteDoctorAsync(int id, ApplicationUser? actor = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var doctor = await context.Doctors.FindAsync(id);
        if (doctor != null)
        {
            var before = new { doctor.Name, doctor.IsActive };
            doctor.IsActive = false;
            await context.SaveChangesAsync();
            await _auditLogService.WriteAsync("DoctorDeactivated", "Doctor", doctor.Id.ToString(), actor, before, new { doctor.Name, doctor.IsActive });
        }
    }

    public async Task<Test> CreateTestAsync(Test test, ApplicationUser? actor = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Tests.Add(test);
        await context.SaveChangesAsync();
        await _auditLogService.WriteAsync("TestCreated", "Test", test.Id.ToString(), actor, after: test);
        return test;
    }

    public async Task UpdateTestAsync(Test test, ApplicationUser? actor = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Tests.AsNoTracking().FirstOrDefaultAsync(t => t.Id == test.Id);
        context.Tests.Update(test);
        await context.SaveChangesAsync();
        await _auditLogService.WriteAsync("TestUpdated", "Test", test.Id.ToString(), actor, existing, test);
    }

    public async Task DeleteTestAsync(int id, ApplicationUser? actor = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var test = await context.Tests.FindAsync(id);
        if (test != null)
        {
            var before = new { test.Name, test.IsActive };
            test.IsActive = false;
            await context.SaveChangesAsync();
            await _auditLogService.WriteAsync("TestDeactivated", "Test", test.Id.ToString(), actor, before, new { test.Name, test.IsActive });
        }
    }
}
