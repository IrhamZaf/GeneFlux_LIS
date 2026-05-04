namespace LIS.Contracts.Settings;

public class CreateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? HospitalId { get; set; }
    public List<int> HospitalIds { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class UpdateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? HospitalId { get; set; }
    public List<int> HospitalIds { get; set; } = new();
    public bool IsActive { get; set; }
}

public class UpdateSettingsRequest
{
    public int HospitalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactNumber { get; set; }
    public string? ContactEmail { get; set; }
    public string? LogoPath { get; set; }
    public List<SettingValueRequest> ReportConfiguration { get; set; } = new();
    public List<SettingValueRequest> Retention { get; set; } = new();
    public List<string>? PermissionCodes { get; set; }
    public string? RoleId { get; set; }
}

public class SettingValueRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
