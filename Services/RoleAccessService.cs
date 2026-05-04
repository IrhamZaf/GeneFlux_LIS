using LIS.Models;

namespace LIS.Services;

public class RoleAccessService
{
    public bool CanCreateReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin;

    public bool CanEditDraftReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin;

    public bool CanDeleteDraftReports(UserRole role) => role == UserRole.SuperAdmin;

    public bool CanSubmitReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin;

    public bool CanApproveReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin;

    public bool CanRejectReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin;

    public bool CanArchiveReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin;

    public bool CanDownloadReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin or UserRole.HeadNurse or UserRole.LabManager or UserRole.Doctor;

    public bool CanManageSettings(UserRole role) => role == UserRole.SuperAdmin;

    public bool CanAccessReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin or UserRole.Doctor or UserRole.HeadNurse or UserRole.LabManager;

    public bool CanAccessPatientDirectory(UserRole role) => role is UserRole.Doctor or UserRole.HeadNurse or UserRole.LabManager;

    public bool CanAccessPatientWorkflow(UserRole role, string? workflow)
    {
        if (!CanAccessPatientDirectory(role))
            return false;

        return NormalizeWorkflow(workflow) switch
        {
            "patients" or "directory" or "search" or "view" or "download" => true,
            _ => false
        };
    }

    public bool IsReadOnlyRole(UserRole role) => role is UserRole.HeadNurse or UserRole.LabManager;

    public string GetPrimaryWorkspaceHref(UserRole role) => role switch
    {
        UserRole.SuperAdmin or UserRole.LabAdmin => "/reports",
        UserRole.Doctor or UserRole.HeadNurse or UserRole.LabManager => "/reports",
        _ => "/"
    };

    public string GetPrimaryWorkspaceLabel(UserRole role) => role switch
    {
        UserRole.SuperAdmin or UserRole.LabAdmin => "Open reports",
        UserRole.Doctor or UserRole.HeadNurse or UserRole.LabManager => "Open reports",
        _ => "Open dashboard"
    };

    public string GetWorkflowGuardMessage(UserRole role, string? workflow)
    {
        return NormalizeWorkflow(workflow) switch
        {
            "reports" => "This report workspace is not available for your role.",
            "administration" or "settings" => "Administration is reserved for SuperAdmin users.",
            _ => "This workflow is not available for your role."
        };
    }

    public string GetRoleSummary(UserRole role) => role switch
    {
        UserRole.SuperAdmin => "System-wide access to dashboard, reports, hospitals, users, and administration.",
        UserRole.LabAdmin => "Full operational access to reports and workflow transitions.",
        UserRole.Doctor => "View and download approved reports attributed to you.",
        UserRole.HeadNurse => "Read-only access to all hospital reports.",
        UserRole.LabManager => "Read-only access to all hospital reports.",
        _ => "Authenticated access."
    };

    public string GetRoleDisplayName(UserRole role) => role switch
    {
        UserRole.SuperAdmin => "Super Admin",
        UserRole.LabAdmin => "Lab Admin",
        UserRole.Doctor => "Doctor",
        UserRole.HeadNurse => "Head Nurse",
        UserRole.LabManager => "Lab Manager",
        _ => "User"
    };

    public string GetRoleThemeClass(UserRole role) => role switch
    {
        UserRole.SuperAdmin => "bg-label-primary",
        UserRole.LabAdmin => "bg-label-info",
        UserRole.Doctor => "bg-label-success",
        UserRole.HeadNurse => "bg-label-warning",
        UserRole.LabManager => "bg-label-danger",
        _ => "bg-label-secondary"
    };

    public IReadOnlyList<RoleFlowItem> GetFlowItems(UserRole role)
    {
        var items = new List<RoleFlowItem>
        {
            new("Dashboard", "Overview of operational activity and report workload.", "/", "icon-base ti tabler-dashboard", GetRoleThemeClass(role)),
            new("Reports", "Unified report workspace with status-based actions and search.", "/reports", "icon-base ti tabler-file-report", "bg-label-success")
        };

        if (CanAccessPatientDirectory(role))
            items.Add(new("Patients", "Review patient records and approved report history.", "/patients", "icon-base ti tabler-users", "bg-label-info"));

        return items;
    }

    /// <summary>Sidebar administration entries. <paramref name="pendingStaffRegistrations"/> is shown as badges on User management / Staff registrations.</summary>
    public IReadOnlyList<AdministrationMenuEntry> GetAdministrationMenu(UserRole role, int pendingStaffRegistrations)
    {
        var list = new List<AdministrationMenuEntry>();

        if (role == UserRole.SuperAdmin)
        {
            list.Add(new AdministrationMenuLink(
                "Hospitals",
                "Manage hospitals and review hospital-level administration details.",
                "/administration/hospitals",
                "icon-base ti tabler-building-hospital",
                "bg-label-secondary"));
        }

        var userMgmtChildren = new List<AdministrationMenuChildLink>();
        if (role == UserRole.SuperAdmin)
        {
            userMgmtChildren.Add(new AdministrationMenuChildLink(
                "Users",
                "Create users, assign roles, and manage hospital access.",
                "/administration/users",
                "icon-base ti tabler-users",
                "bg-label-secondary"));
        }

        if (role is UserRole.SuperAdmin or UserRole.LabAdmin)
        {
            var staffBadge = pendingStaffRegistrations > 0 ? pendingStaffRegistrations : (int?)null;
            userMgmtChildren.Add(new AdministrationMenuChildLink(
                "Staff registrations",
                "Approve self-service sign-ups from doctors, head nurses, and lab managers.",
                "/administration/registration-requests",
                "icon-base ti tabler-user-plus",
                "bg-label-warning",
                staffBadge));
        }

        if (userMgmtChildren.Count > 0)
        {
            var parentBadge = pendingStaffRegistrations > 0 ? pendingStaffRegistrations : (int?)null;
            list.Add(new AdministrationMenuGroup(
                "User management",
                "icon-base ti tabler-user-cog",
                userMgmtChildren,
                parentBadge));
        }

        return list;
    }

    private static string NormalizeWorkflow(string? workflow)
    {
        if (string.IsNullOrWhiteSpace(workflow))
            return "reports";

        return workflow.Trim().ToLowerInvariant();
    }
}

public sealed record RoleFlowItem(string Title, string Description, string Href, string Icon, string LabelClass);

public abstract record AdministrationMenuEntry;

public sealed record AdministrationMenuLink(
    string Title,
    string Description,
    string Href,
    string Icon,
    string LabelClass) : AdministrationMenuEntry;

public sealed record AdministrationMenuGroup(
    string Title,
    string Icon,
    IReadOnlyList<AdministrationMenuChildLink> Children,
    int? ParentBadgeCount = null) : AdministrationMenuEntry;

public sealed record AdministrationMenuChildLink(
    string Title,
    string Description,
    string Href,
    string Icon,
    string LabelClass,
    int? BadgeCount = null);
