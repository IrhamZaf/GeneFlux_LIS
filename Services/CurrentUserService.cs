using System.Security.Claims;
using LIS.Data;
using LIS.Models;
using Microsoft.EntityFrameworkCore;

namespace LIS.Services;

public class CurrentUserService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public CurrentUserService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ApplicationUser?> GetCurrentUserAsync(ClaimsPrincipal principal)
    {
        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
            return null;

        return await GetCurrentUserByUserNameAsync(userName);
    }

    public async Task<ApplicationUser?> GetByIdAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users
            .Include(u => u.Hospital)
            .Include(u => u.Doctor)
            .Include(u => u.UserHospitals)
                .ThenInclude(uh => uh.Hospital)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public IReadOnlyList<int> GetAccessibleHospitalIds(ApplicationUser user)
    {
        var ids = user.UserHospitals
            .Select(uh => uh.HospitalId)
            .Distinct()
            .ToList();

        if (ids.Count == 0 && user.HospitalId.HasValue)
            ids.Add(user.HospitalId.Value);

        return ids;
    }

    public IReadOnlyList<Hospital> GetAccessibleHospitals(ApplicationUser user)
    {
        var hospitals = user.UserHospitals
            .Select(uh => uh.Hospital)
            .Where(h => h != null)
            .DistinctBy(h => h.Id)
            .OrderBy(h => h.Name)
            .ToList();

        if (hospitals.Count == 0 && user.Hospital != null)
            hospitals.Add(user.Hospital);

        return hospitals;
    }

    public int? GetDefaultHospitalId(ApplicationUser user)
    {
        if (user.HospitalId.HasValue)
            return user.HospitalId.Value;

        return GetAccessibleHospitalIds(user).FirstOrDefault();
    }

    public bool HasHospitalAccess(ApplicationUser user, int hospitalId)
    {
        if (user.Role is UserRole.SuperAdmin or UserRole.LabAdmin)
            return true;

        return GetAccessibleHospitalIds(user).Contains(hospitalId);
    }

    public async Task<CurrentUserContext?> GetCurrentUserContextAsync(ClaimsPrincipal principal)
    {
        var user = await GetCurrentUserAsync(principal);
        if (user == null)
            return null;

        return BuildCurrentUserContext(user);
    }

    public CurrentUserContext BuildCurrentUserContext(ApplicationUser user)
    {
        var hospitals = GetAccessibleHospitals(user)
            .Select(h => new UserHospitalScope(h.Id, h.Name))
            .ToList();

        return new CurrentUserContext
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = user.Role,
            DoctorId = user.DoctorId,
            HospitalIds = hospitals.Select(h => h.Id).ToList(),
            Hospitals = hospitals,
            DefaultHospitalId = GetDefaultHospitalId(user),
            RequiresHospitalSelection = user.Role != UserRole.SuperAdmin && hospitals.Count > 1
        };
    }

    private async Task<ApplicationUser?> GetCurrentUserByUserNameAsync(string userName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users
            .Include(u => u.Hospital)
            .Include(u => u.Doctor)
            .Include(u => u.UserHospitals)
                .ThenInclude(uh => uh.Hospital)
            .FirstOrDefaultAsync(u => u.NormalizedUserName == userName.ToUpperInvariant());
    }
}

public sealed class CurrentUserContext
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public int? DoctorId { get; set; }
    public List<int> HospitalIds { get; set; } = new();
    public List<UserHospitalScope> Hospitals { get; set; } = new();
    public int? DefaultHospitalId { get; set; }
    public bool RequiresHospitalSelection { get; set; }
}

public sealed record UserHospitalScope(int Id, string Name);
