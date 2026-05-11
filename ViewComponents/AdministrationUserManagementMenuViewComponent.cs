using LIS.Services;
using Microsoft.AspNetCore.Mvc;

namespace LIS.ViewComponents;

public class AdministrationUserManagementMenuViewComponent : ViewComponent
{
    private readonly StaffRegistrationService _staffRegistrationService;

    public AdministrationUserManagementMenuViewComponent(StaffRegistrationService staffRegistrationService)
    {
        _staffRegistrationService = staffRegistrationService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var showUsers = User.IsInRole("SuperAdmin") || User.IsInRole("LabAdmin") || User.IsInRole("Admin");
        var showStaff = User.IsInRole("SuperAdmin") || User.IsInRole("LabAdmin") || User.IsInRole("Admin");
        var showHospitals = User.IsInRole("SuperAdmin") || User.IsInRole("LabAdmin") || User.IsInRole("Admin");

        if (!showUsers && !showStaff && !showHospitals)
            return Content(string.Empty);

        var pending = showStaff ? await _staffRegistrationService.GetPendingRegistrationCountAsync() : 0;

        var path = HttpContext?.Request.Path.Value ?? "";
        var staffActive = path.Contains("registration-requests", StringComparison.OrdinalIgnoreCase);
        var usersActive = path.Contains("/Administration/Users", StringComparison.OrdinalIgnoreCase)
                          || path.Contains("/administration/users", StringComparison.OrdinalIgnoreCase);
        var activityLogActive = path.Contains("activity-log", StringComparison.OrdinalIgnoreCase);
        var userMgmtOpen = staffActive || usersActive || activityLogActive;

        var vm = new AdministrationUserManagementMenuViewModel
        {
            ShowHospitalsLink = showHospitals,
            ShowUserManagementSection = showUsers || showStaff,
            ShowUsersChild = showUsers,
            ShowStaffRegistrationsChild = showStaff,
            ShowActivityLogChild = showUsers,
            PendingRegistrationCount = pending,
            UserManagementMenuOpen = userMgmtOpen,
            UsersActive = usersActive,
            StaffRegistrationsActive = staffActive,
            ActivityLogActive = activityLogActive
        };

        return View(vm);
    }
}

public class AdministrationUserManagementMenuViewModel
{
    public bool ShowHospitalsLink { get; set; }
    public bool ShowUserManagementSection { get; set; }
    public bool ShowUsersChild { get; set; }
    public bool ShowStaffRegistrationsChild { get; set; }
    public int PendingRegistrationCount { get; set; }
    public bool UserManagementMenuOpen { get; set; }
    public bool UsersActive { get; set; }
    public bool StaffRegistrationsActive { get; set; }
    public bool ShowActivityLogChild { get; set; }
    public bool ActivityLogActive { get; set; }
}
