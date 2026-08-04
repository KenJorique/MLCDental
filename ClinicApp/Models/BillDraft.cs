using ClinicApp.ViewModels.TransactionVM;
using System.Collections.ObjectModel;

namespace ClinicApp.Models;

public class BillDraft
{
    public string PatientId { get; set; } = "";

    public string PatientName { get; set; } = "";

    public ObservableCollection<ServiceLineItem> Services { get; set; }
        = new();

    public bool IsInstallment { get; set; }

    public decimal DiscountPercent { get; set; }

    public string DiscountName { get; set; } = "";

    public string Notes { get; set; } = "";

    public string? SupabaseBookingId { get; set; }
    public string? AppointmentEntryId { get; set; }
    public string? SupabaseEntryId { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public string Phone { get; set; } = string.Empty;
    public int InstallmentMonths { get; set; }
    public decimal MonthlyPayment { get; set; }

    public string InstallmentSummary =>
        IsInstallment && InstallmentMonths > 0
            ? $"{InstallmentMonths} months @ ₱{MonthlyPayment:N2}/month"
            : string.Empty;
}