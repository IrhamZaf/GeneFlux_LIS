using LIS.Models;
using LIS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LIS.Pages.Administration;

[Authorize(Roles = "SuperAdmin,LabAdmin,Admin")]
public class RegistrationRequestsModel : PageModel
{
    private readonly StaffRegistrationService _registrationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public RegistrationRequestsModel(
        StaffRegistrationService registrationService,
        UserManager<ApplicationUser> userManager)
    {
        _registrationService = registrationService;
        _userManager = userManager;
    }

    public IReadOnlyList<StaffRegistrationRequest> Pending { get; private set; } = [];

    [TempData]
    public string? FlashMessage { get; set; }

    public async Task OnGetAsync()
    {
        Pending = await _registrationService.GetPendingAsync();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        var actor = await _userManager.GetUserAsync(User);
        if (actor is not ApplicationUser appUser)
            return Unauthorized();

        var (ok, message) = await _registrationService.ApproveAsync(id, appUser);
        FlashMessage = message;
        return ok ? RedirectToPage() : RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id, string? reason)
    {
        var actor = await _userManager.GetUserAsync(User);
        if (actor is not ApplicationUser appUser)
            return Unauthorized();

        var (ok, message) = await _registrationService.RejectAsync(id, reason, appUser);
        FlashMessage = message;
        return RedirectToPage();
    }
}
