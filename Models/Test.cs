namespace LIS.Models;

public class Test
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TestMethod { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}
