using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using LIS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LIS.Pages.Administration
{
    [Authorize(Roles = "SuperAdmin")]
    public class UsersModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public IList<UserDisplayModel> Users { get; set; } = default!;

        public class UserDisplayModel
        {
            public string Id { get; set; } = string.Empty;
            public string? Email { get; set; }
            public string? FullName { get; set; }
            public string Roles { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            var users = await _userManager.Users.ToListAsync();
            Users = new List<UserDisplayModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                Users.Add(new UserDisplayModel
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Roles = string.Join(", ", roles)
                });
            }
        }
    }
}
