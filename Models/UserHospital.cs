namespace LIS.Models;

public class UserHospital
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int HospitalId { get; set; }
    public Hospital Hospital { get; set; } = null!;
}
