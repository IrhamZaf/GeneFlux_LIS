using LIS.Models;

namespace LIS.Services;

public class RoleAccessService
{
    public bool CanCreateReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin;

    public bool CanEditDraftReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin;

    public bool CanDeleteDraftReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin;

    public bool CanSubmitReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin;

    public bool CanApproveReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin;

    public bool CanRejectReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin;

    public bool CanArchiveReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin;

    public bool CanDownloadReports(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin or UserRole.HeadNurse or UserRole.LabManager or UserRole.Doctor;

    public bool CanManageSettings(UserRole role) => role == UserRole.SuperAdmin;

    /// <summary>Hospitals, user accounts, registrations, and activity log — same for Super Admin and Lab Admin. System Settings remain Super Admin only (<see cref="CanManageSettings"/>).</summary>
    public bool CanAccessAdministration(UserRole role) => role is UserRole.SuperAdmin or UserRole.LabAdmin;

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

    /// <summary>Role-specific home landing (analytics hero + scoped dashboard).</summary>
    public string GetLandingHref(UserRole role) => role switch
    {
        UserRole.SuperAdmin => "/home/geneflux-super-admin",
        UserRole.LabAdmin => "/home/geneflux-lab-admin",
        UserRole.Doctor => "/home/clinical-doctor",
        UserRole.HeadNurse or UserRole.LabManager => "/home/hospital-reporting",
        _ => "/home/clinical-doctor"
    };

    public string GetPrimaryWorkspaceHref(UserRole role) => role switch
    {
        UserRole.SuperAdmin or UserRole.LabAdmin => "/reports",
        UserRole.Doctor or UserRole.HeadNurse or UserRole.LabManager => "/reports",
        _ => GetLandingHref(role)
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
            "administration" => CanAccessAdministration(role)
                ? "This view is not available from this navigation path."
                : "Hospital and user administration is reserved for Super Admin and Lab Admin users.",
            "settings" => CanManageSettings(role)
                ? "This view is not available from this navigation path."
                : "System settings (roles, organization, configuration) are reserved for Super Admin users.",
            _ => "This workflow is not available for your role."
        };
    }

    public string GetRoleSummary(UserRole role) => role switch
    {
        UserRole.SuperAdmin => "System-wide access to dashboard, reports, hospitals, users, and administration.",
        UserRole.LabAdmin => "Same scope as Super Admin for dashboard, reports, and administration (hospitals, user accounts, requests, activity log). System Settings and dropdown master data are Super Admin only.",
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
            new("Dashboard", "Overview of operational activity and report workload.", GetLandingHref(role), "icon-base ti tabler-dashboard", GetRoleThemeClass(role)),
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

        if (CanAccessAdministration(role))
        {
            list.Add(new AdministrationMenuLink(
                "Hospitals",
                "Manage hospitals and review hospital-level administration details.",
                "/administration/hospitals",
                "icon-base ti tabler-building-hospital",
                "bg-label-secondary"));
        }

        var userMgmtChildren = new List<AdministrationMenuChildLink>();
        if (CanAccessAdministration(role))
        {
            userMgmtChildren.Add(new AdministrationMenuChildLink(
                "User Accounts",
                "Create users, assign roles, and manage hospital access.",
                "/administration/users",
                "icon-base ti tabler-users",
                "bg-label-secondary"));

            var staffBadge = pendingStaffRegistrations > 0 ? pendingStaffRegistrations : (int?)null;
            userMgmtChildren.Add(new AdministrationMenuChildLink(
                "Pending Request",
                "Approve self-service sign-ups from doctors, head nurses, and lab managers.",
                "/administration/registration-requests",
                "icon-base ti tabler-user-plus",
                "bg-label-warning",
                staffBadge));

            userMgmtChildren.Add(new AdministrationMenuChildLink(
                "System Activity Log",
                "Review audit history for administrative actions.",
                "/administration/activity-log",
                "icon-base ti tabler-history",
                "bg-label-secondary"));
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
