using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static ClinicApp.Helpers.BillDraftStore;

namespace ClinicApp.ViewModels.TransactionVM;

[QueryProperty(nameof(BillId), "billId")]
[QueryProperty(nameof(PatientId), "patientId")]
[QueryProperty(nameof(PatientName), "patientName")]
[QueryProperty(nameof(AppointmentEntryId), "appointmentEntryId")]
[QueryProperty(nameof(SupabaseEntryId), "supabaseEntryId")]
[QueryProperty(nameof(SupabaseBookingId), "supabaseBookingId")]
public partial class PaymentViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;

    public PaymentViewModel(SupabaseDataService supabase)
    {
        _supabase = supabase;
    }

    [ObservableProperty]
    private string billId = string.Empty;

    [ObservableProperty]
    private string patientId = string.Empty;
    [ObservableProperty] string appointmentEntryId = string.Empty;
    [ObservableProperty] string supabaseEntryId = string.Empty;
    [ObservableProperty] string supabaseBookingId = string.Empty;

    [ObservableProperty]
    private string patientName = string.Empty;

    [ObservableProperty]
    private decimal paymentAmount;

    [ObservableProperty]
    private SupabaseBill? bill;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public string DueDateDisplay =>
        Bill?.DueDateDisplay ?? "—";

    public string LastPaymentDateDisplay =>
        Bill?.LastPaymentDateDisplay ?? "—";

    // ── NEW: live "remaining after this payment" feedback ──
    public string RemainingAfterPaymentDisplay
    {
        get
        {
            if (Bill == null) return "₱0.00";
            var remaining = Bill.Balance - PaymentAmount;
            if (remaining < 0) remaining = 0;
            return $"₱{remaining:N2}";
        }
    }

    // ── NEW: warns staff if typed amount exceeds the balance ──
    public bool IsOverpaying =>
        Bill != null && PaymentAmount > Bill.Balance;

    // ── NEW: lets the UI highlight "Full Balance" chip when it matches ──
    public bool IsFullPaymentSelected =>
        Bill != null && PaymentAmount == Bill.Balance && PaymentAmount > 0;

    partial void OnBillIdChanged(string value)
    {
        if (CreatedBillStore.Current?.Id == value)
        {
            Bill = CreatedBillStore.Current;
            CreatedBillStore.Current = null;
            return;
        }
        if (!string.IsNullOrWhiteSpace(value))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await LoadBillAsync());
        }
    }

    partial void OnBillChanged(SupabaseBill? value)
    {
        PaymentAmount = value?.Balance ?? 0;

        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(DiscountDisplay));
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(PaidDisplay));
        OnPropertyChanged(nameof(BalanceDisplay));
        OnPropertyChanged(nameof(BillNumber));
        OnPropertyChanged(nameof(DueDateDisplay));
        OnPropertyChanged(nameof(LastPaymentDateDisplay));
        OnPropertyChanged(nameof(RemainingAfterPaymentDisplay));
        OnPropertyChanged(nameof(IsOverpaying));
        OnPropertyChanged(nameof(IsFullPaymentSelected));
    }

    // ── NEW: keep the live preview in sync as the staff types ──
    partial void OnPaymentAmountChanged(decimal value)
    {
        OnPropertyChanged(nameof(RemainingAfterPaymentDisplay));
        OnPropertyChanged(nameof(IsOverpaying));
        OnPropertyChanged(nameof(IsFullPaymentSelected));
        if (HasError) HasError = false;
    }

    private async Task LoadBillAsync()
    {
        IsBusy = true;

        try
        {
            Bill = await _supabase.GetBillByIdAsync(BillId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public string BillNumber =>
        Bill?.BillNumber ?? "";

    public string SubtotalDisplay =>
        Bill == null ? "₱0.00" : $"₱{Bill.Subtotal:N2}";

    public string DiscountDisplay =>
        Bill == null ? "₱0.00" : $"₱{Bill.DiscountAmount:N2}";

    public string TotalDisplay =>
        Bill == null ? "₱0.00" : $"₱{Bill.TotalAmount:N2}";

    public string PaidDisplay =>
        Bill == null ? "₱0.00" : $"₱{Bill.AmountPaid:N2}";

    public string BalanceDisplay =>
        Bill == null ? "₱0.00" : $"₱{Bill.Balance:N2}";

    // ── NEW: quick-fill commands ──
    [RelayCommand]
    private void SetQuarterAmount()
    {
        if (Bill == null) return;
        PaymentAmount = Math.Round(Bill.Balance * 0.25m, 2);
    }

    [RelayCommand]
    private void SetHalfAmount()
    {
        if (Bill == null) return;
        PaymentAmount = Math.Round(Bill.Balance * 0.5m, 2);
    }

    [RelayCommand]
    private void SetFullAmount()
    {
        if (Bill == null) return;
        PaymentAmount = Bill.Balance;
    }

    [RelayCommand]
    private async Task RecordPayment()
    {
        if (Bill == null)
            return;

        IsBusy = true;
        HasError = false;

        try
        {
            if (PaymentAmount <= 0)
            {
                await Shell.Current.GoToAsync(
     $"{nameof(ReceiptPage)}" +
     $"?billId={Bill.Id}" +
     $"&patientName={Uri.EscapeDataString(PatientName)}" +
     $"&patientId={Uri.EscapeDataString(PatientId)}" +
     $"&appointmentEntryId={Uri.EscapeDataString(AppointmentEntryId)}" +
     $"&supabaseEntryId={Uri.EscapeDataString(SupabaseEntryId)}" +
     $"&supabaseBookingId={Uri.EscapeDataString(SupabaseBookingId)}");
                return;
            }

            var (success, error) =
                await _supabase.RecordPaymentAsync(Bill.Id, PaymentAmount);

            if (!success)
            {
                HasError = true;
                ErrorMessage = error ?? "Failed to record payment.";
                return;
            }

            await Shell.Current.GoToAsync(
    $"{nameof(ReceiptPage)}" +
    $"?billId={Bill.Id}" +
    $"&patientName={Uri.EscapeDataString(PatientName)}" +
    $"&patientId={Uri.EscapeDataString(PatientId)}" +
    $"&appointmentEntryId={Uri.EscapeDataString(AppointmentEntryId)}" +
    $"&supabaseEntryId={Uri.EscapeDataString(SupabaseEntryId)}" +
    $"&supabaseBookingId={Uri.EscapeDataString(SupabaseBookingId)}");
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SkipPayment()
    {
        if (Bill == null)
            return;

        await Shell.Current.GoToAsync(
    $"{nameof(ReceiptPage)}" +
    $"?billId={Bill.Id}" +
    $"&patientName={Uri.EscapeDataString(PatientName)}" +
    $"&patientId={Uri.EscapeDataString(PatientId)}" +
    $"&appointmentEntryId={Uri.EscapeDataString(AppointmentEntryId)}" +
    $"&supabaseEntryId={Uri.EscapeDataString(SupabaseEntryId)}" +
    $"&supabaseBookingId={Uri.EscapeDataString(SupabaseBookingId)}");
    }
}