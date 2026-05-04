using Microsoft.AspNetCore.Identity;

namespace LIS.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    // Preserved as the user's default hospital selection for existing UI flows.
    public int? HospitalId { get; set; }
    public Hospital? Hospital { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation: if this user is a doctor, link to Doctor record
    public int? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }

    public ICollection<UserHospital> UserHospitals { get; set; } = new List<UserHospital>();
    public ICollection<Report> CreatedReports { get; set; } = new List<Report>();
    public ICollection<Report> UpdatedReports { get; set; } = new List<Report>();
}
