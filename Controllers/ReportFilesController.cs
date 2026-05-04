using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIS.Controllers;

[ApiController]
[Route("api/report-files")]
public class ReportFilesController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public ReportFilesController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet("view")]
    [Authorize(Policy = "ViewReports")]
    public IActionResult ViewPdf([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest();

        var normalized = path.Replace('\\', '/').Trim().TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal))
            return BadRequest();

        if (!normalized.StartsWith("uploads/reports/", StringComparison.OrdinalIgnoreCase))
            return NotFound();

        var fullPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, normalized));
        var root = Path.GetFullPath(_environment.WebRootPath);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return NotFound();

        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        return PhysicalFile(fullPath, "application/pdf");
    }
}
