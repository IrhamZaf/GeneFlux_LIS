using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using LIS.Data;
using LIS.Models;
using LIS.Services;
using Microsoft.EntityFrameworkCore;

namespace LIS.Pages.Reports
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly PdfExportService _pdfService;

        public IndexModel(ApplicationDbContext context, PdfExportService pdfService)
        {
            _context = context;
            _pdfService = pdfService;
        }

        public IList<Report> Reports { get; set; } = default!;

        public async Task OnGetAsync(string? status)
        {
            var query = _context.Reports
                .Include(r => r.Hospital)
                .Include(r => r.Doctor)
                .Include(r => r.Patient)
                .Include(r => r.Test)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<ReportStatus>(status, out var reportStatus))
                {
                    query = query.Where(r => r.Status == reportStatus);
                }
            }

            Reports = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        }

        public async Task<IActionResult> OnGetDownloadAsync(int id)
        {
            var report = await _context.Reports
                .Include(r => r.Hospital)
                .Include(r => r.Doctor)
                .Include(r => r.Patient)
                .Include(r => r.Test)
                .Include(r => r.TestResults)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound();
            }

            var pdf = _pdfService.GenerateReportPdf(report);
            return File(pdf, "application/pdf", $"Report_{report.ReferenceNumber}.pdf");
        }
    }
}
