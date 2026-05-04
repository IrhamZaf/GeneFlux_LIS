namespace LIS.Models;

public class Hospital
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactNumber { get; set; }
    public string? ContactEmail { get; set; }
    public string? LogoPath { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
    public ICollection<Report> Reports { get; set; } = new List<Report>();
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<UserHospital> UserHospitals { get; set; } = new List<UserHospital>();
}
