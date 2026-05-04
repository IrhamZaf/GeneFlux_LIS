namespace LIS.Models;

public class Patient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IdentityType IdentityType { get; set; } = IdentityType.NRIC;
    public string? NRIC { get; set; }
    public string? PassportNo { get; set; }
    public string? MRN { get; set; }
    public Sex Sex { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Computed
    public string IdentityNumber => IdentityType == IdentityType.NRIC ? (NRIC ?? "") : (PassportNo ?? "");

    public int? Age
    {
        get
        {
            if (DateOfBirth == null) return null;
            var today = DateTime.Today;
            var age = today.Year - DateOfBirth.Value.Year;
            if (DateOfBirth.Value.Date > today.AddYears(-age)) age--;
            return age;
        }
    }

    // Navigation
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}
