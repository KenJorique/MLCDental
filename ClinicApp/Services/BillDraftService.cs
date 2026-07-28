using ClinicApp.ViewModels.TransactionVM;

namespace ClinicApp.Services;

public class BillDraftService
{
    public List<ServiceLineItem> Services { get; } = new();

    public string PatientId { get; set; } = string.Empty;

    public string PatientName { get; set; } = string.Empty;

    public decimal Total =>
        Services.Sum(x => x.Subtotal);

    public void Clear()
    {
        Services.Clear();
        PatientId = "";
        PatientName = "";
    }
}