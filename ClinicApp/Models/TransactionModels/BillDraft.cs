using ClinicApp.Models.SupabaseModels;
using ClinicApp.ViewModels.TransactionVM;
using System.Collections.ObjectModel;

namespace ClinicApp.Models.TransactionModels;

// A follow-up session the dentist reviewed on the Bill Summary sheet — not written to Supabase until payment succeeds.
public class PendingFollowUpChoice
{
    public SupabaseTreatmentSequence Row { get; set; } = null!;
    public DateTime? SelectedSlotLocal { get; set; }
    public DateTime? SelectedSlotUtc { get; set; }
}

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

    public decimal AmountDueToday { get; set; }

    // Follow-up sessions chosen (scheduled or deferred) during Bill Summary, persisted only after payment succeeds.
    public List<PendingFollowUpChoice> PendingFollowUps { get; set; } = new();

    public string InstallmentSummary =>
        IsInstallment && InstallmentMonths > 0
            ? $"{InstallmentMonths} months @ ₱{MonthlyPayment:N2}/month"
            : string.Empty;
}