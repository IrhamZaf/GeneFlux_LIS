using LIS.Data;
using LIS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LIS.Services;

public class UserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly AuditLogService _auditLogService;

    public UserService(
        UserManager<ApplicationUser> userManager,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        AuditLogService auditLogService)
    {
        _userManager = userManager;
        _contextFactory = contextFactory;
        _auditLogService = auditLogService;
    }

    public async Task<List<ApplicationUser>> GetUsersByRoleAsync(UserRole role)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users
            .Include(u => u.Hospital)
            .Include(u => u.UserHospitals)
                .ThenInclude(uh => uh.Hospital)
            .Where(u => u.Role == role && u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }

    public async Task<List<ApplicationUser>> GetAllUsersAsync(bool includeInactive = true)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Users
            .Include(u => u.Hospital)
            .Include(u => u.UserHospitals)
                .ThenInclude(uh => uh.Hospital)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(u => u.IsActive);

        return await query.OrderBy(u => u.FullName).ToListAsync();
    }

    public async Task<ApplicationUser?> GetByIdAsync(string id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users
            .Include(u => u.Hospital)
            .Include(u => u.UserHospitals)
                .ThenInclude(uh => uh.Hospital)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<(bool Success, string[] Errors)> CreateUserAsync(ApplicationUser user, string password, ApplicationUser? actor = null)
    {
        user.UserName = user.Email;
        user.EmailConfirmed = true;
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var lower = user.Email.Trim().ToLowerInvariant();
            var doctorConflict = await context.Doctors.AnyAsync(d =>
                d.Email != null &&
                !string.IsNullOrWhiteSpace(d.Email) &&
                d.Email.Trim().ToLower() == lower &&
                (!user.DoctorId.HasValue || d.Id != user.DoctorId.Value));
            if (doctorConflict)
            {
                return (false,
                [
                    "This email is already assigned to a doctor profile. Use a different email or link the account to that doctor record."
                ]);
            }
        }

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        await SyncRolesAsync(user, null, user.Role);
        await SyncUserHospitalsAsync(user.Id, ResolveHospitalIds(user));
        await _auditLogService.WriteAsync("UserCreated", "User", user.Id, actor, after: new
        {
            user.FullName,
            user.Email,
            Role = user.Role.ToString(),
            HospitalIds = ResolveHospitalIds(user),
            user.IsActive
        });

        return (true, Array.Empty<string>());
    }

    public async Task<(bool Success, string[] Errors)> UpdateUserAsync(ApplicationUser user, ApplicationUser? actor = null)
    {
        var existing = await _userManager.FindByIdAsync(user.Id);
        if (existing is not ApplicationUser current)
            return (false, ["User not found."]);

        var before = new
        {
            current.FullName,
            current.Email,
            Role = current.Role.ToString(),
            HospitalIds = await GetHospitalIdsAsync(current.Id),
            current.IsActive
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            await using var checkContext = await _contextFactory.CreateDbContextAsync();
            var lower = user.Email.Trim().ToLowerInvariant();
            var doctorConflict = await checkContext.Doctors.AnyAsync(d =>
                d.Email != null &&
                !string.IsNullOrWhiteSpace(d.Email) &&
                d.Email.Trim().ToLower() == lower &&
                (!current.DoctorId.HasValue || d.Id != current.DoctorId.Value));
            if (doctorConflict)
            {
                return (false,
                [
                    "This email is already assigned to a doctor profile. Use a different email or link the account to that doctor record."
                ]);
            }
        }

        var previousRole = current.Role;
        current.FullName = user.FullName;
        current.Email = user.Email;
        current.UserName = user.Email;
        current.Role = user.Role;
        current.HospitalId = user.HospitalId;
        current.IsActive = user.IsActive;
        current.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(current);
        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        await SyncRolesAsync(current, previousRole, current.Role);
        await SyncUserHospitalsAsync(current.Id, ResolveHospitalIds(user));
        await _auditLogService.WriteAsync("UserUpdated", "User", current.Id, actor, before, new
        {
            current.FullName,
            current.Email,
            Role = current.Role.ToString(),
            HospitalIds = ResolveHospitalIds(user),
            current.IsActive
        });

        return (true, Array.Empty<string>());
    }

    public async Task<bool> DeactivateUserAsync(string id, ApplicationUser? actor = null)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is not ApplicationUser appUser)
            return false;

        var before = new
        {
            appUser.FullName,
            appUser.Email,
            appUser.IsActive
        };

        appUser.IsActive = false;
        appUser.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(appUser);

        await _auditLogService.WriteAsync("UserDeactivated", "User", appUser.Id, actor, before, new
        {
            appUser.FullName,
            appUser.Email,
            appUser.IsActive
        });

        return true;
    }

    public async Task<(bool Success, string[] Errors)> ResetPasswordAsync(string userId, string newPassword, ApplicationUser? actor = null)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is not ApplicationUser appUser)
            return (false, ["User not found"]);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (result.Succeeded)
            await _auditLogService.WriteAsync("UserPasswordReset", "User", appUser.Id, actor, metadata: new { appUser.Email });

        return result.Succeeded
            ? (true, Array.Empty<string>())
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    private async Task SyncRolesAsync(ApplicationUser user, UserRole? previousRole, UserRole targetRole)
    {
        var allRoleNames = Enum.GetNames<UserRole>().Concat(["Admin"]).Distinct().ToArray();
        var assignedRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = assignedRoles.Where(role => allRoleNames.Contains(role)).ToArray();
        if (rolesToRemove.Length > 0)
            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);

        var targetRoleName = targetRole.ToString();
        await _userManager.AddToRoleAsync(user, targetRoleName);

        if (targetRole == UserRole.LabAdmin)
            await _userManager.AddToRoleAsync(user, "Admin");
    }

    private static List<int> ResolveHospitalIds(ApplicationUser user)
    {
        var ids = user.UserHospitals
            .Select(uh => uh.HospitalId)
            .Distinct()
            .ToList();

        if (ids.Count == 0 && user.HospitalId.HasValue)
            ids.Add(user.HospitalId.Value);

        return ids;
    }

    private async Task<List<int>> GetHospitalIdsAsync(string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UserHospitals
            .Where(uh => uh.UserId == userId)
            .Select(uh => uh.HospitalId)
            .OrderBy(id => id)
            .ToListAsync();
    }

    private async Task SyncUserHospitalsAsync(string userId, IReadOnlyCollection<int> hospitalIds)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var distinctIds = hospitalIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var existing = await context.UserHospitals
            .Where(uh => uh.UserId == userId)
            .ToListAsync();

        context.UserHospitals.RemoveRange(existing.Where(uh => !distinctIds.Contains(uh.HospitalId)));

        var existingIds = existing.Select(uh => uh.HospitalId).ToHashSet();
        foreach (var hospitalId in distinctIds.Where(id => !existingIds.Contains(id)))
        {
            context.UserHospitals.Add(new UserHospital
            {
                UserId = userId,
                HospitalId = hospitalId
            });
        }

        var identityUser = await _userManager.FindByIdAsync(userId);
        if (identityUser is ApplicationUser appUser)
        {
            appUser.HospitalId = distinctIds.Count > 0 ? distinctIds[0] : null;
            appUser.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(appUser);
        }

        await context.SaveChangesAsync();
    }
}
