using LIS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIS.Controllers;

[ApiController]
[Route("me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly CurrentUserService _currentUserService;

    public MeController(CurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMe()
    {
        var context = await _currentUserService.GetCurrentUserContextAsync(User);
        return context == null ? Unauthorized() : Ok(context);
    }

    [HttpGet("hospitals")]
    public async Task<IActionResult> GetHospitals()
    {
        var context = await _currentUserService.GetCurrentUserContextAsync(User);
        return context == null ? Unauthorized() : Ok(context.Hospitals);
    }
}
