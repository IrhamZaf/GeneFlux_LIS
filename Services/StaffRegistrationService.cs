using LIS.Data;
using LIS.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace LIS.Services;

public class StaffRegistrationService
{
    private const string PasswordProtectorPurpose = "LIS.StaffRegistration.Password.v1";

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UserService _userService;
    private readonly AuditLogService _auditLogService;

    public StaffRegistrationService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IDataProtectionProvider dataProtectionProvider,
        UserManager<ApplicationUser> userManager,
        UserService userService,
        AuditLogService auditLogService)
    {
        _contextFactory = contextFactory;
        _dataProtectionProvider = dataProtectionProvider;
        _userManager = userManager;
        _userService = userService;
        _auditLogService = auditLogService;
    }

    public static bool IsAllowedSelfServiceRole(UserRole role) =>
        role is UserRole.Doctor or UserRole.HeadNurse or UserRole.LabManager;

    /// <summary>
    /// Stores a pending registration. Does not create an Identity account.
    /// </summary>
    public async Task<(bool Success, string Message)> SubmitAsync(
        string email,
        string fullName,
        string nric,
        string phoneNumber,
        string? mmcNumber,
        string password,
        UserRole role,
        int hospitalId)
    {
        email = email.Trim();
        fullName = fullName.Trim();
        nric = nric.Trim();
        phoneNumber = phoneNumber.Trim();
        mmcNumber = string.IsNullOrWhiteSpace(mmcNumber) ? null : mmcNumber.Trim();

        if (!IsAllowedSelfServiceRole(role))
            return (false, "This registration path is only for Doctor, Head Nurse, or Lab Manager.");

        await using var context = await _contextFactory.CreateDbContextAsync();

        var hospitalExists = await context.Hospitals.AnyAsync(h => h.Id == hospitalId && h.IsActive);
        if (!hospitalExists)
            return (false, "Please select a valid hospital.");

        if (await context.Users.AnyAsync(u => u.Email == email))
            return (false, "An account with this email already exists. Sign in or contact support.");

        if (await context.Doctors.AnyAsync(d =>
                d.Email != null &&
                d.Email.Trim().ToLower() == email.ToLowerInvariant()))
            return (false, "This email is already assigned to a doctor in the system. Sign in or contact support.");

        if (await context.StaffRegistrationRequests.AnyAsync(r =>
                r.Email == email && r.Status == StaffRegistrationStatus.Pending))
            return (false, "A registration with this email is already pending approval.");

        // Validate the password against the configured Identity password policy now,
        // so the applicant sees any errors immediately on the registration form.
        var tempUser = new ApplicationUser { UserName = email, Email = email };
        foreach (var validator in _userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_userManager, tempUser, password);
            if (!result.Succeeded)
                return (false, string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        var protector = _dataProtectionProvider.CreateProtector(PasswordProtectorPurpose);
        var payload = protector.Protect(password);

        context.StaffRegistrationRequests.Add(new StaffRegistrationRequest
        {
            Email = email,
            FullName = fullName,
            Nric = nric,
            PhoneNumber = phoneNumber,
            MmcNumber = mmcNumber,
            HospitalId = hospitalId,
            RequestedRole = role,
            ProtectedPassword = payload,
            Status = StaffRegistrationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return (true, "Your registration was submitted. An administrator must approve it before you can sign in.");
    }

    public async Task<int> GetPendingRegistrationCountAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.StaffRegistrationRequests
            .AsNoTracking()
            .CountAsync(r => r.Status == StaffRegistrationStatus.Pending, cancellationToken);
    }

    public async Task<List<StaffRegistrationRequest>> GetPendingAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.StaffRegistrationRequests
            .AsNoTracking()
            .Include(r => r.Hospital)
            .Where(r => r.Status == StaffRegistrationStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> ApproveAsync(int requestId, ApplicationUser actor)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var request = await context.StaffRegistrationRequests
            .Include(r => r.Hospital)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
            return (false, "Registration request not found.");

        if (request.Status != StaffRegistrationStatus.Pending)
            return (false, "This request has already been processed.");

        if (await _userManager.Users.AnyAsync(u => u.Email == request.Email))
            return (false, "A user with this email already exists.");

        if (await context.Doctors.AnyAsync(d =>
                d.Email != null &&
                d.Email.Trim().ToLower() == request.Email.Trim().ToLowerInvariant()))
            return (false, "A doctor with this email already exists.");

        string plainPassword;
        try
        {
            var protector = _dataProtectionProvider.CreateProtector(PasswordProtectorPurpose);
            plainPassword = protector.Unprotect(request.ProtectedPassword);
        }
        catch
        {
            return (false, "Unable to restore the submitted password. Ask the user to register again.");
        }

        Doctor? doctorEntity = null;
        if (request.RequestedRole == UserRole.Doctor)
        {
            doctorEntity = new Doctor
            {
                Name = request.FullName,
                Email = request.Email,
                HospitalId = request.HospitalId,
                Qualifications = "MBBS",
                Specialty = "General Practice",
                IsActive = true
            };
            context.Doctors.Add(doctorEntity);
            await context.SaveChangesAsync();
        }

        var user = new ApplicationUser
        {
            FullName = request.FullName,
            Email = request.Email,
            Role = request.RequestedRole,
            HospitalId = request.HospitalId,
            DoctorId = doctorEntity?.Id,
            IsActive = true
        };

        var (ok, errors) = await _userService.CreateUserAsync(user, plainPassword, actor);
        if (!ok)
        {
            if (doctorEntity != null)
            {
                context.Doctors.Remove(doctorEntity);
                await context.SaveChangesAsync();
            }

            // Do not surface Identity password-policy wording here — applicants must satisfy rules at registration.
            var onlyPasswordPolicy =
                errors.Length > 0 &&
                errors.All(e => e.Contains("assword", StringComparison.OrdinalIgnoreCase));
            if (onlyPasswordPolicy)
            {
                return (false,
                    "This request cannot be approved with the password on file. Reject it and ask the applicant to submit a new registration.");
            }

            return (false, string.Join(" ", errors));
        }

        if (doctorEntity != null)
        {
            doctorEntity.UserId = user.Id;
            await context.SaveChangesAsync();
        }

        request.Status = StaffRegistrationStatus.Approved;
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedByUserId = actor.Id;
        request.RejectionReason = null;
        context.StaffRegistrationRequests.Update(request);
        await context.SaveChangesAsync();

        await _auditLogService.WriteAsync(
            "StaffRegistrationApproved",
            "StaffRegistrationRequest",
            request.Id.ToString(),
            actor,
            metadata: new { request.Email, Role = request.RequestedRole.ToString(), request.HospitalId });

        return (true, "Account created. The user can sign in with the password they chose at registration.");
    }

    public async Task<(bool Success, string Message)> RejectAsync(int requestId, string? reason, ApplicationUser actor)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var request = await context.StaffRegistrationRequests.FindAsync(requestId);
        if (request == null)
            return (false, "Registration request not found.");

        if (request.Status != StaffRegistrationStatus.Pending)
            return (false, "This request has already been processed.");

        request.Status = StaffRegistrationStatus.Rejected;
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedByUserId = actor.Id;
        request.RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        await context.SaveChangesAsync();

        await _auditLogService.WriteAsync(
            "StaffRegistrationRejected",
            "StaffRegistrationRequest",
            request.Id.ToString(),
            actor,
            metadata: new { request.Email, request.RejectionReason });

        return (true, "Registration rejected.");
    }
}
