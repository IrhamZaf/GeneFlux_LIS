using LIS.Contracts.Settings;
using LIS.Models;
using LIS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIS.Controllers;

[ApiController]
[Route("settings")]
[Authorize(Policy = "ManageSettings")]
public class SettingsController : ControllerBase
{
    private readonly SettingsService _settingsService;
    private readonly CurrentUserService _currentUserService;

    public SettingsController(SettingsService settingsService, CurrentUserService currentUserService)
    {
        _settingsService = settingsService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var snapshot = await _settingsService.GetSnapshotAsync();
        return Ok(snapshot);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
    {
        var actor = await _currentUserService.GetCurrentUserAsync(User);
        if (actor == null)
            return Unauthorized();

        await _settingsService.UpdateHospitalAsync(new Hospital
        {
            Id = request.HospitalId,
            Name = request.Name,
            Address = request.Address,
            ContactNumber = request.ContactNumber,
            ContactEmail = request.ContactEmail,
            LogoPath = request.LogoPath
        }, actor);

        await _settingsService.UpdateSettingsAsync(new SettingsUpdateRequest
        {
            ReportConfiguration = request.ReportConfiguration.Select(x => new SettingEntry { Key = x.Key, Value = x.Value }).ToList(),
            Retention = request.Retention.Select(x => new SettingEntry { Key = x.Key, Value = x.Value }).ToList()
        }, actor);

        if (!string.IsNullOrWhiteSpace(request.RoleId) && request.PermissionCodes != null)
            await _settingsService.UpdateRolePermissionsAsync(request.RoleId, request.PermissionCodes, actor);

        return NoContent();
    }
}
