using System.ComponentModel.DataAnnotations;
using LIS.Models;
using LIS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LIS.Pages.Account;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly StaffRegistrationService _registrationService;
    private readonly DropdownService _dropdownService;

    public RegisterModel(StaffRegistrationService registrationService, DropdownService dropdownService)
    {
        _registrationService = registrationService;
        _dropdownService = dropdownService;
    }

    [BindProperty]
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [BindProperty]
    [Required]
    [MinLength(2)]
    public string FullName { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "NRIC is required.")]
    [RegularExpression(@"^\d{6}-?\d{2}-?\d{4}$", ErrorMessage = "Enter a valid Malaysian NRIC (e.g. 900101-10-1234).")]
    public string Nric { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^01[0-9]-[0-9]{7,8}$", ErrorMessage = "Enter a valid Malaysian mobile number (e.g. 011-37455884).")]
    public string PhoneNumber { get; set; } = "";

    [BindProperty]
    public string? MmcNumber { get; set; }

    [BindProperty]
    [Required]
    [MinLength(6)]
    public string Password { get; set; } = "";

    [BindProperty]
    [Required]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = "";

    [BindProperty]
    [Required]
    public UserRole SelectedRole { get; set; } = UserRole.Doctor;

    [BindProperty]
    [Range(1, int.MaxValue, ErrorMessage = "Select a hospital.")]
    public int HospitalId { get; set; }

    public SelectList? HospitalOptions { get; set; }

    public string? StatusMessage { get; set; }
    public bool Success { get; set; }

    public async Task OnGetAsync()
    {
        await LoadLookupsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadLookupsAsync();

        if (SelectedRole == UserRole.Doctor && string.IsNullOrWhiteSpace(MmcNumber))
            ModelState.AddModelError(nameof(MmcNumber), "MMC registration number is required for doctors.");

        if (!ModelState.IsValid)
        {
            StatusMessage = "Please correct the errors above.";
            return Page();
        }

        if (!StaffRegistrationService.IsAllowedSelfServiceRole(SelectedRole))
        {
            StatusMessage = "Only Doctor, Head Nurse, or Lab Manager can use self-registration.";
            return Page();
        }

        var (ok, message) = await _registrationService.SubmitAsync(Email, FullName, Nric, PhoneNumber, MmcNumber, Password, SelectedRole, HospitalId);
        Success = ok;
        StatusMessage = message;
        if (ok)
        {
            ModelState.Clear();
            Email = FullName = Nric = PhoneNumber = Password = ConfirmPassword = string.Empty;
            MmcNumber = null;
            HospitalId = 0;
        }

        return Page();
    }

    private async Task LoadLookupsAsync()
    {
        var hospitals = await _dropdownService.GetHospitalsAsync();
        HospitalOptions = new SelectList(hospitals, "Id", "Name");
    }
}
