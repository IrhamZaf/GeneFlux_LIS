namespace LIS.Contracts.Administration;

public class HospitalFormResult
{
    public int? HospitalId { get; set; }
    public UpdateHospitalRequest Request { get; set; } = new();
}
