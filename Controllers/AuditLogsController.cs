using LIS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIS.Controllers;

[ApiController]
[Route("audit-logs")]
[Authorize(Policy = "ViewAuditLogs")]
public class AuditLogsController : ControllerBase
{
    private readonly AuditLogService _auditLogService;

    public AuditLogsController(AuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] string? performedByUserId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var logs = await _auditLogService.GetAsync(new AuditLogFilter
        {
            Action = action,
            EntityType = entityType,
            PerformedByUserId = performedByUserId,
            From = from,
            To = to
        });

        return Ok(logs);
    }
}
