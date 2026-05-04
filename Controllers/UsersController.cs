using LIS.Contracts.Settings;
using LIS.Models;
using LIS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIS.Controllers;

[ApiController]
[Route("users")]
[Authorize(Policy = "ManageSettings")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;
    private readonly CurrentUserService _currentUserService;

    public UsersController(UserService userService, CurrentUserService currentUserService)
    {
        _userService = userService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] bool includeInactive = true)
    {
        var users = await _userService.GetAllUsersAsync(includeInactive);
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var actor = await _currentUserService.GetCurrentUserAsync(User);
        if (actor == null)
            return Unauthorized();

        if (!Enum.TryParse<UserRole>(request.Role, out var role))
            return BadRequest("Invalid role.");

        var user = new ApplicationUser
        {
            FullName = request.FullName,
            Email = request.Email,
            Role = role,
            HospitalId = request.HospitalId,
            IsActive = request.IsActive,
            UserHospitals = ResolveUserHospitals(request.HospitalIds, request.HospitalId)
        };

        var (success, errors) = await _userService.CreateUserAsync(user, request.Password, actor);
        return success ? Created($"/users/{user.Id}", user) : BadRequest(errors);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var actor = await _currentUserService.GetCurrentUserAsync(User);
        if (actor == null)
            return Unauthorized();

        var existing = await _userService.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        if (!Enum.TryParse<UserRole>(request.Role, out var role))
            return BadRequest("Invalid role.");

        existing.FullName = request.FullName;
        existing.Email = request.Email;
        existing.Role = role;
        existing.HospitalId = request.HospitalId;
        existing.IsActive = request.IsActive;
        existing.UserHospitals = ResolveUserHospitals(request.HospitalIds, request.HospitalId);

        var (success, errors) = await _userService.UpdateUserAsync(existing, actor);
        return success ? NoContent() : BadRequest(errors);
    }

    private static List<UserHospital> ResolveUserHospitals(IEnumerable<int> hospitalIds, int? fallbackHospitalId)
    {
        var resolvedIds = hospitalIds
            .Append(fallbackHospitalId ?? 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        return resolvedIds
            .Select(id => new UserHospital { HospitalId = id })
            .ToList();
    }
}
