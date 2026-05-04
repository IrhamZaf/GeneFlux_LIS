using LIS.Contracts.Administration;
using LIS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIS.Controllers;

[ApiController]
[Route("hospitals")]
[Authorize(Policy = "ManageSettings")]
public class HospitalsController : ControllerBase
{
    private readonly AdministrationService _administrationService;
    private readonly CurrentUserService _currentUserService;

    public HospitalsController(AdministrationService administrationService, CurrentUserService currentUserService)
    {
        _administrationService = administrationService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetHospitals(CancellationToken cancellationToken)
    {
        return Ok(await _administrationService.GetHospitalsAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetHospital(int id, CancellationToken cancellationToken)
    {
        var hospital = await _administrationService.GetHospitalDetailsAsync(id, cancellationToken);
        return hospital == null ? NotFound() : Ok(hospital);
    }

    [HttpPost]
    public async Task<IActionResult> CreateHospital([FromBody] CreateHospitalRequest request, CancellationToken cancellationToken)
    {
        var actor = await _currentUserService.GetCurrentUserAsync(User);
        if (actor == null)
            return Unauthorized();

        var hospital = await _administrationService.CreateHospitalAsync(request, actor, cancellationToken);
        return CreatedAtAction(nameof(GetHospital), new { id = hospital.Id }, hospital);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateHospital(int id, [FromBody] UpdateHospitalRequest request, CancellationToken cancellationToken)
    {
        var actor = await _currentUserService.GetCurrentUserAsync(User);
        if (actor == null)
            return Unauthorized();

        var updated = await _administrationService.UpdateHospitalAsync(id, request, actor, cancellationToken);
        return updated ? NoContent() : NotFound();
    }
}
