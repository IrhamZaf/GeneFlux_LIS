namespace LIS.Models;

public class Doctor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? MMCNo { get; set; }
    public string? Qualifications { get; set; }
    public string? Specialty { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? SignatureText { get; set; }
    public int HospitalId { get; set; }
    public Hospital Hospital { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation: linked user account (nullable - not all doctors need login)
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    // Navigation
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}
