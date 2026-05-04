namespace LIS.Models;

public enum StaffRegistrationStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>
/// Self-service signup for Doctor / Head Nurse / Lab Manager.
/// No AspNetUsers row exists until an administrator approves the request.
/// </summary>
public class StaffRegistrationRequest
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    /// <summary>Malaysian national identity card number.</summary>
    public string Nric { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>MMC registration number — only applicable for Doctors.</summary>
    public string? MmcNumber { get; set; }

    /// <summary>Requested hospital affiliation.</summary>
    public int HospitalId { get; set; }
    public Hospital Hospital { get; set; } = null!;

    /// <summary>Doctor, HeadNurse, or LabManager only.</summary>
    public UserRole RequestedRole { get; set; }

    /// <summary>Data-protection payload for the chosen password (not plain text).</summary>
    public string ProtectedPassword { get; set; } = string.Empty;

    public StaffRegistrationStatus Status { get; set; } = StaffRegistrationStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    public string? ProcessedByUserId { get; set; }
    public ApplicationUser? ProcessedByUser { get; set; }

    public string? RejectionReason { get; set; }
}
