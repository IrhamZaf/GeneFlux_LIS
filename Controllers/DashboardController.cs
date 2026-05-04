using LIS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIS.Controllers;

[ApiController]
[Route("dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;
    private readonly CurrentUserService _currentUserService;

    public DashboardController(DashboardService dashboardService, CurrentUserService currentUserService)
    {
        _dashboardService = dashboardService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard([FromQuery] int? hospitalId, CancellationToken cancellationToken)
    {
        var user = await _currentUserService.GetCurrentUserAsync(User);
        if (user == null)
            return Unauthorized();

        return Ok(await _dashboardService.GetDashboardAsync(user, hospitalId, cancellationToken));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] int? hospitalId, CancellationToken cancellationToken)
    {
        var user = await _currentUserService.GetCurrentUserAsync(User);
        if (user == null)
            return Unauthorized();

        return Ok(await _dashboardService.GetStatsAsync(user, hospitalId, cancellationToken));
    }

    [HttpGet("recent-reports")]
    public async Task<IActionResult> GetRecentReports([FromQuery] int? hospitalId, CancellationToken cancellationToken)
    {
        var user = await _currentUserService.GetCurrentUserAsync(User);
        if (user == null)
            return Unauthorized();

        return Ok(await _dashboardService.GetRecentReportsAsync(user, hospitalId, cancellationToken));
    }

    [HttpGet("recent-patients")]
    public async Task<IActionResult> GetRecentPatients([FromQuery] int? hospitalId, CancellationToken cancellationToken)
    {
        var user = await _currentUserService.GetCurrentUserAsync(User);
        if (user == null)
            return Unauthorized();

        return Ok(await _dashboardService.GetRecentPatientsAsync(user, hospitalId, cancellationToken));
    }
}
