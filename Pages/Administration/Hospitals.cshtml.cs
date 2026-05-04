using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using LIS.Data;
using LIS.Models;
using Microsoft.EntityFrameworkCore;

namespace LIS.Pages.Administration
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class HospitalsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public HospitalsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Hospital> Hospitals { get; set; } = default!;

        public async Task OnGetAsync()
        {
            Hospitals = await _context.Hospitals
                .OrderBy(h => h.Name)
                .ToListAsync();
        }
    }
}
