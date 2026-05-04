using LIS.Data;
using LIS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LIS.Services;

public class SettingsService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AuditLogService _auditLogService;

    public SettingsService(IDbContextFactory<ApplicationDbContext> contextFactory, RoleManager<IdentityRole> roleManager, AuditLogService auditLogService)
    {
        _contextFactory = contextFactory;
        _roleManager = roleManager;
        _auditLogService = auditLogService;
    }

    public async Task<SettingsSnapshot> GetSnapshotAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var hospitals = await context.Hospitals
            .OrderBy(h => h.Name)
            .ToListAsync();

        var permissions = await context.Permissions
            .Where(p => p.IsActive)
            .OrderBy(p => p.Code)
            .ToListAsync();

        var roles = await _roleManager.Roles
            .OrderBy(r => r.Name)
            .ToListAsync();

        var rolePermissions = await context.RolePermissions
            .Include(rp => rp.Permission)
            .ToListAsync();

        var reportConfiguration = await GetCategoryAsync(context, "ReportConfiguration");
        var retention = await GetCategoryAsync(context, "Retention");

        return new SettingsSnapshot
        {
            Hospitals = hospitals,
            Roles = roles.Select(role => new RolePermissionView
            {
                RoleId = role.Id,
                RoleName = role.Name ?? string.Empty,
                PermissionCodes = rolePermissions
                    .Where(rp => rp.RoleId == role.Id)
                    .Select(rp => rp.Permission.Code)
                    .OrderBy(code => code)
                    .ToList()
            }).ToList(),
            Permissions = permissions,
            ReportConfiguration = reportConfiguration,
            Retention = retention
        };
    }

    public async Task UpdateHospitalAsync(Hospital updatedHospital, ApplicationUser actor)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Hospitals.FindAsync(updatedHospital.Id)
            ?? throw new KeyNotFoundException("Hospital not found.");

        var before = new
        {
            existing.Name,
            existing.Address,
            existing.ContactNumber,
            existing.ContactEmail,
            existing.LogoPath
        };

        existing.Name = updatedHospital.Name;
        existing.Address = updatedHospital.Address;
        existing.ContactNumber = updatedHospital.ContactNumber;
        existing.ContactEmail = updatedHospital.ContactEmail;
        existing.LogoPath = updatedHospital.LogoPath;

        await context.SaveChangesAsync();

        await _auditLogService.WriteAsync("OrganizationUpdated", "Hospital", existing.Id.ToString(), actor, before, new
        {
            existing.Name,
            existing.Address,
            existing.ContactNumber,
            existing.ContactEmail,
            existing.LogoPath
        });
    }

    public async Task UpdateSettingsAsync(SettingsUpdateRequest request, ApplicationUser actor)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        foreach (var item in request.ReportConfiguration)
            await UpsertSettingAsync(context, "ReportConfiguration", item.Key, item.Value, actor.Id);

        foreach (var item in request.Retention)
            await UpsertSettingAsync(context, "Retention", item.Key, item.Value, actor.Id);

        await context.SaveChangesAsync();

        await _auditLogService.WriteAsync("SystemSettingsUpdated", "SystemSetting", "global", actor, metadata: request);
    }

    public async Task UpdateRolePermissionsAsync(string roleId, IEnumerable<string> permissionCodes, ApplicationUser actor)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var role = await _roleManager.FindByIdAsync(roleId) ?? throw new KeyNotFoundException("Role not found.");

        var permissions = await context.Permissions
            .Where(p => permissionCodes.Contains(p.Code) && p.IsActive)
            .ToListAsync();

        var existing = await context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync();

        var before = await context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Include(rp => rp.Permission)
            .Select(rp => rp.Permission.Code)
            .OrderBy(code => code)
            .ToListAsync();

        context.RolePermissions.RemoveRange(existing);
        foreach (var permission in permissions)
        {
            context.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permission.Id
            });
        }

        await context.SaveChangesAsync();

        await _auditLogService.WriteAsync("RolePermissionsUpdated", "Role", roleId, actor, before, permissions.Select(p => p.Code).OrderBy(code => code).ToList(), new
        {
            RoleName = role.Name
        });
    }

    private static async Task<List<SettingEntry>> GetCategoryAsync(ApplicationDbContext context, string category)
    {
        return await context.SystemSettings
            .Where(s => s.Category == category)
            .OrderBy(s => s.Key)
            .Select(s => new SettingEntry
            {
                Key = s.Key,
                Value = s.Value,
                ValueType = s.ValueType
            })
            .ToListAsync();
    }

    private static async Task UpsertSettingAsync(ApplicationDbContext context, string category, string key, string value, string userId)
    {
        var existing = await context.SystemSettings.FirstOrDefaultAsync(s => s.Category == category && s.Key == key);
        if (existing == null)
        {
            context.SystemSettings.Add(new SystemSetting
            {
                Category = category,
                Key = key,
                Value = value,
                ValueType = InferValueType(value),
                UpdatedAt = DateTime.UtcNow,
                UpdatedByUserId = userId
            });
        }
        else
        {
            existing.Value = value;
            existing.ValueType = InferValueType(value);
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedByUserId = userId;
        }
    }

    private static string InferValueType(string value)
    {
        if (bool.TryParse(value, out _))
            return "bool";
        if (int.TryParse(value, out _))
            return "int";
        return "string";
    }
}

public class SettingsSnapshot
{
    public List<Hospital> Hospitals { get; set; } = new();
    public List<RolePermissionView> Roles { get; set; } = new();
    public List<Permission> Permissions { get; set; } = new();
    public List<SettingEntry> ReportConfiguration { get; set; } = new();
    public List<SettingEntry> Retention { get; set; } = new();
}

public class RolePermissionView
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public List<string> PermissionCodes { get; set; } = new();
}

public class SettingEntry
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string ValueType { get; set; } = "string";
}

public class SettingsUpdateRequest
{
    public List<SettingEntry> ReportConfiguration { get; set; } = new();
    public List<SettingEntry> Retention { get; set; } = new();
}
