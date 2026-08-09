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

    public string RemainingAfterPaymentDisplay
    {
        get
        {
            if (Bill == null) return "₱0.00";
            var toRecord = PaymentAmount > MinimumDueToday ? MinimumDueToday : PaymentAmount;
            var remaining = Bill.Balance - toRecord;
            if (remaining < 0) remaining = 0;
            return $"₱{remaining:N2}";
        }
    }

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

    [ObservableProperty]
    private decimal liveMinimumDue;

    partial void OnBillChanged(SupabaseBill? value)
    {
        // Starts at 0, not pre-filled — staff types the amount received.
        PaymentAmount = 0;

        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(DiscountDisplay));
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(PaidDisplay));
        OnPropertyChanged(nameof(BalanceDisplay));
        OnPropertyChanged(nameof(BillNumber));
        OnPropertyChanged(nameof(DueDateDisplay));
        OnPropertyChanged(nameof(LastPaymentDateDisplay));
        OnPropertyChanged(nameof(RemainingAfterPaymentDisplay));
        OnPropertyChanged(nameof(IsFullPaymentSelected));

        // Fetched fresh here (not in LoadBillAsync) because Bill can also
        // get set via the "just created" fast path in OnBillIdChanged,
        // which skips LoadBillAsync entirely — this covers both paths.
        if (value != null)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                LiveMinimumDue = await _supabase.GetMinimumDueForBillAsync(value.Id);
                OnPropertyChanged(nameof(MinimumDueToday));
                OnPropertyChanged(nameof(MinimumDueTodayDisplay));
                OnPropertyChanged(nameof(IsBelowMinimum));
                OnPropertyChanged(nameof(RemainingAfterPaymentDisplay));
            });
        }
        else
        {
            LiveMinimumDue = 0;
        }

        OnPropertyChanged(nameof(MinimumDueToday));
        OnPropertyChanged(nameof(MinimumDueTodayDisplay));
        OnPropertyChanged(nameof(IsBelowMinimum));
    }

    public decimal MinimumDueToday =>
        Bill == null ? 0 : LiveMinimumDue > 0 ? LiveMinimumDue : Bill.Balance;

    public string MinimumDueTodayDisplay => $"₱{MinimumDueToday:N2}";

    // Blocks ANY amount below the minimum
    public bool IsBelowMinimum =>
        Bill != null && PaymentAmount < MinimumDueToday;

    // Anything typed beyond the minimum isn't recorded as extra payment —
    // it's handled like a cash register: the excess is Change, and only
    // MinimumDueToday actually gets recorded. Staff use the separate
    // "Add Payment" flow (on Bill Details / Receipt) if the patient
    // genuinely wants to pay more toward the balance.
    public decimal Change =>
        PaymentAmount > MinimumDueToday ? PaymentAmount - MinimumDueToday : 0;

    public string ChangeDisplay => $"₱{Change:N2}";

    public bool HasChange => Change > 0;

    // ── keep the live preview in sync as the staff types ──
    partial void OnPaymentAmountChanged(decimal value)
    {
        // Block negative amounts — Keyboard="Numeric" mostly prevents this
        // on-screen, but this covers pasted input / physical keyboards too.
        if (value < 0)
        {
            PaymentAmount = 0;
            return;
        }

        OnPropertyChanged(nameof(RemainingAfterPaymentDisplay));
        OnPropertyChanged(nameof(IsFullPaymentSelected));
        OnPropertyChanged(nameof(IsBelowMinimum));
        OnPropertyChanged(nameof(Change));
        OnPropertyChanged(nameof(ChangeDisplay));
        OnPropertyChanged(nameof(HasChange));
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

    [RelayCommand]
    private async Task RecordPayment()
    {
        if (Bill == null)
            return;

        IsBusy = true;
        HasError = false;

        try
        {
            // No more "0 = skip payment" exception — every submission,
            // including 0, is checked against the minimum below.
            if (IsBelowMinimum)
            {
                HasError = true;
                ErrorMessage = $"Minimum payment today is {MinimumDueTodayDisplay}.";
                return;
            }

            // Whatever staff typed beyond the minimum is Change, not part
            // of the recorded payment — see the Change property above.
            var amountToRecord = MinimumDueToday;

            var (success, error) =
                await _supabase.RecordPaymentAsync(Bill.Id, amountToRecord);

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