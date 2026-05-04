namespace LIS.Models;

public class DropdownValue
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty; // Hospital, Doctor, Test, DateRange
    public string Value { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
