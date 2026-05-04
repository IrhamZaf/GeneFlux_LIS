using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using LIS.Data;
using LIS.Models;
using Microsoft.EntityFrameworkCore;

namespace LIS.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public int TotalReports { get; set; }
        public int DraftReports { get; set; }
        public int ApprovedReports { get; set; }
        public int TotalDoctors { get; set; }

        public async Task OnGetAsync()
        {
            TotalReports = await _context.Reports.CountAsync();
            DraftReports = await _context.Reports.CountAsync(r => r.Status == ReportStatus.Draft);
            ApprovedReports = await _context.Reports.CountAsync(r => r.Status == ReportStatus.Approved);
            TotalDoctors = await _context.Doctors.CountAsync();
        }
    }
}
