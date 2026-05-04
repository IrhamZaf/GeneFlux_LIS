using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using LIS.Data;
using LIS.Models;
using Microsoft.EntityFrameworkCore;

namespace LIS.Pages.Administration
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class DoctorsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DoctorsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Doctor> Doctors { get; set; } = default!;

        public async Task OnGetAsync()
        {
            Doctors = await _context.Doctors
                .Include(d => d.Hospital)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }
    }
}
