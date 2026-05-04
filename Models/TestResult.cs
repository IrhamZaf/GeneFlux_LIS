namespace LIS.Models;

public class TestResult
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;
    public string TestName { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string? ResultDetail { get; set; }
    public int SortOrder { get; set; }
}
