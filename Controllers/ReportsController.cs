using LIS.Contracts.Reports;
using LIS.Models;
using LIS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LIS.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "ViewReports")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _reportService;
    private readonly PatientService _patientService;
    private readonly CurrentUserService _currentUserService;

    public ReportsController(ReportService reportService, PatientService patientService, CurrentUserService currentUserService)
    {
        _reportService = reportService;
        _patientService = patientService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetReports([FromQuery] int? hospitalId, [FromQuery] ReportStatus? status, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var actor = await _currentUserService.GetCurrentUserAsync(User);
        if (actor == null)
            return Unauthorized();

        var filter = new ReportFilter
        {
            UserRole = actor.Role,
            UserId = actor.Id,
            UserEmail = actor.Email,
            DoctorId = actor.DoctorId,
            HospitalId = actor.HospitalId,
            AccessibleHospitalIds = _currentUserService.GetAccessibleHospitalIds(actor).ToList(),
            FilterHospitalId = hospitalId,
            Status = status,
            SearchTerm = search,
            Page = 1,
            PageSize = 100
        };

        var (reports, totalCount) = await _reportService.GetReportsAsync(filter);
        return Ok(new { totalCount, items = reports });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetReport(int id)
    {
        var actor = await _currentUserService.GetCurrentUserAsync(User);
        if (actor == null)
            return Unauthorized();

        var report = await _reportService.GetByIdAsync(id, actor);
        return report == null ? NotFound() : Ok(report);
    }

    [HttpPost]
    [Authorize(Policy = "ManageReports")]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest request)
    {
        var actor = await _currentUserService.GetCurrentUserAsync(User);
        if (actor == null)
            return Unauthorized();

        if (!IsValidRequest(request, out var validationMessage))
            return BadRequest(validationMessage);

        var patient = await UpsertPatientAsync(request);
        var report = new Report
        {
            HospitalId = request.HospitalId,
            DoctorId = request.DoctorId,
            PatientId = patient.Id,
            TestId = request.TestId,
            SpecimenType = request.SpecimenType,
            SampleCollectionDate = request.SampleCollectionDate,
            ReceivedAtLabDate = request.ReceivedAtLabDate
        };

        try
        {
            var created = await _reportService.CreateAsync(report, MapResults(request.Results), actor);
            return CreatedAtAction(nameof(GetReport), new { id = created.Id }, new { created.Id, created.ReferenceNumber, created.Status });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "ManageReports")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateReportRequest request)
    {
        var actor = await _currentUserService.GetCurrentUserAsync(User);
        if (actor == null)
            return Unauthorized();

        if (!IsValidRequest(request, out var validationMessage))
            return BadRequest(validationMessage);

        var existing = await _reportService.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var patient = await UpsertPatientAsync(request, existing.PatientId);
        existing.HospitalId = request.HospitalId;
        existing.DoctorId = request.DoctorId;
        existing.PatientId = patient.Id;
        existing.TestId = request.TestId;
        existing.SpecimenType = request.SpecimenType;
        existing.SampleCollectionDate = request.SampleCollectionDate;
        existing.ReceivedAtLabDate = request.ReceivedAtLabDate;

        try
        {
            await _reportService.UpdateAsync(existing, MapResults(request.Results), actor);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = "ManageReports")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeReportStatusRequest request)
    {
        var actor = await _currentUserService.GetCurrentUserAsync(User);
        if (actor == null)
            return Unauthorized();

        try
        {
            Report? result = request.TargetStatus switch
            {
                ReportStatus.PendingReview => await _reportService.SubmitForReviewAsync(id, actor),
                ReportStatus.Approved => await _reportService.ApproveAsync(id, actor),
                ReportStatus.Draft => await _reportService.RejectAsync(id, actor),
                ReportStatus.Archived => await _reportService.ArchiveAsync(id, actor),
                _ => null
            };

            if (result == null)
                return NotFound();

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<Patient> UpsertPatientAsync(CreateReportRequest request, int existingPatientId = 0)
    {
        return await _patientService.CreateOrUpdateAsync(new Patient
        {
            Id = existingPatientId,
            Name = request.PatientName,
            IdentityType = request.IdentityType,
            NRIC = request.IdentityType == IdentityType.NRIC ? request.Nric : null,
            PassportNo = request.IdentityType == IdentityType.Passport ? request.PassportNo : null,
            MRN = request.Mrn,
            Sex = request.Sex
        });
    }

    private static bool IsValidRequest(CreateReportRequest request, out string validationMessage)
    {
        if (request.HospitalId <= 0 || request.DoctorId <= 0 || request.TestId <= 0)
        {
            validationMessage = "HospitalId, DoctorId, and TestId are required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.PatientName))
        {
            validationMessage = "PatientName is required.";
            return false;
        }

        validationMessage = string.Empty;
        return true;
    }

    private static List<TestResult> MapResults(IEnumerable<ReportResultRequest> results)
    {
        return results.Select((result, index) => new TestResult
        {
            TestName = result.TestName,
            Result = result.Result,
            ResultDetail = result.ResultDetail,
            SortOrder = index + 1
        }).ToList();
    }
}

