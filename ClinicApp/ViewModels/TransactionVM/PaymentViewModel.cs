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

    // ── lets the UI highlight "Full Balance" chip when it matches ──
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

    [ObservableProperty]
    private bool hasLoadedMinimumDue;

    partial void OnBillChanged(SupabaseBill? value)
    {
        // Starts at 0, not pre-filled — staff types the amount received.
        PaymentAmount = 0;
        HasLoadedMinimumDue = false;

        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(DiscountDisplay));
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(PaidDisplay));
        OnPropertyChanged(nameof(BalanceDisplay));
        OnPropertyChanged(nameof(BillNumber));
        OnPropertyChanged(nameof(DueDateDisplay));
        OnPropertyChanged(nameof(LastPaymentDateDisplay));
        OnPropertyChanged(nameof(IsFullPaymentSelected));

        // Fetched fresh here (not in LoadBillAsync) because Bill can also
        // get set via the "just created" fast path in OnBillIdChanged,
        // which skips LoadBillAsync entirely — this covers both paths.
        if (value != null)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                LiveMinimumDue = await _supabase.GetMinimumDueForBillAsync(value.Id);
                HasLoadedMinimumDue = true;
                OnPropertyChanged(nameof(MinimumDueToday));
                OnPropertyChanged(nameof(MinimumDueTodayDisplay));
                OnPropertyChanged(nameof(IsBelowMinimum));
                OnPropertyChanged(nameof(IsNothingDue));
            });
        }
        else
        {
            LiveMinimumDue = 0;
        }

        OnPropertyChanged(nameof(MinimumDueToday));
        OnPropertyChanged(nameof(MinimumDueTodayDisplay));
        OnPropertyChanged(nameof(IsBelowMinimum));
        OnPropertyChanged(nameof(IsNothingDue));
    }

    // Live-fetched per visit — see OnBillChanged. Before the fetch
    // completes, falls back to Bill.Balance so staff don't briefly see a
    // ₱0 minimum while it's loading. Once loaded, 0 is trusted as genuine
    // (bill fully paid, or next installment not due yet) — it does NOT
    // fall back to Balance anymore, since that was the actual bug: it let
    // staff record a payment for an amount that wasn't really due.
    public decimal MinimumDueToday =>
        Bill == null ? 0 : HasLoadedMinimumDue ? LiveMinimumDue : Bill.Balance;

    public string MinimumDueTodayDisplay => $"₱{MinimumDueToday:N2}";

    public string PaymentAmountDisplay => $"₱{PaymentAmount:N2}";

    // True once we've genuinely confirmed nothing is owed right now —
    // either the bill is fully paid, or (for installment bills) the next
    // payment simply isn't due yet. Blocks RecordPayment entirely; this is
    // what stops the "go back to Payment page after already paying and
    // submit again" bug, regardless of what amount gets typed.
    public bool IsNothingDue =>
        Bill != null && HasLoadedMinimumDue &&
        (Bill.Balance <= 0 || MinimumDueToday <= 0);

    // Blocks ANY amount below the minimum, including 0 — there's no
    // "enter 0 to skip payment for now" escape hatch. No longer disables
    // the button — the check happens as an alert on tap instead (see
    // RecordPayment), rather than a persistent banner while typing.
    public bool IsBelowMinimum =>
        Bill != null && !IsNothingDue && PaymentAmount < MinimumDueToday;

    // Flags amounts that exceed the WHOLE bill balance — almost always a
    // typo (an extra zero, etc.) rather than a genuine intent to overpay.
    public bool IsAmountTooLarge =>
        Bill != null && PaymentAmount > Bill.Balance;

    // Anything typed beyond the minimum isn't recorded as extra payment —
    // it's handled like a cash register: the excess is Change, and only
    // MinimumDueToday actually gets recorded. Staff use the separate
    // "Add Payment" flow (on Bill Details / Receipt) if the patient
    // genuinely wants to pay more toward the balance. Suppressed when the
    // amount is flagged as too large — the warning takes over instead of
    // showing a huge, likely-mistaken Change figure.
    public decimal Change =>
        PaymentAmount > MinimumDueToday && !IsAmountTooLarge
            ? PaymentAmount - MinimumDueToday
            : 0;

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

        OnPropertyChanged(nameof(PaymentAmountDisplay));
        OnPropertyChanged(nameof(IsFullPaymentSelected));
        OnPropertyChanged(nameof(IsBelowMinimum));
        OnPropertyChanged(nameof(IsAmountTooLarge));
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

        // Blocks re-submitting after already fully paying, or before the
        // next installment is genuinely due (e.g. going back to this page
        // right after paying the downpayment, same visit, same day).
        if (IsNothingDue)
        {
            var message = Bill.Balance <= 0
                ? "This bill is already fully paid."
                : $"Nothing is due right now. Next payment is due {DueDateDisplay}.";

            await Shell.Current.DisplayAlert("Nothing Due", message, "OK");
            return;
        }

        // No more "0 = skip payment" exception — every submission,
        // including 0, is checked against the minimum. Shown as an alert
        // on tap now, not a persistent banner while typing (that was
        // redundant with the "Minimum due today" hint already on screen).
        if (IsBelowMinimum)
        {
            await Shell.Current.DisplayAlert(
                "Payment Too Low",
                $"Minimum payment today is {MinimumDueTodayDisplay}.",
                "OK");
            return;
        }

        // Amounts beyond the whole bill balance are almost always a typo
        // (an extra zero, etc.) — ask for confirmation rather than
        // silently recording it or showing an oddly huge "Change" figure.
        if (IsAmountTooLarge)
        {
            bool proceed = await Shell.Current.DisplayAlert(
                "Check Amount",
                $"You entered {PaymentAmountDisplay}, but the total balance " +
                $"is only {BalanceDisplay}. Continue anyway?",
                "Yes, Continue", "Cancel");

            if (!proceed)
                return;
        }

        IsBusy = true;
        HasError = false;

        try
        {
            // Whatever staff typed beyond the minimum is Change, not part
            // of the recorded payment — see the Change property above.
            // Capped at Bill.Balance too, defensively, so this can never
            // record more than what's genuinely still owed.
            var amountToRecord = Math.Min(MinimumDueToday, Bill.Balance);

            var (success, error) =
                await _supabase.RecordPaymentAsync(Bill.Id, amountToRecord);

            if (!success)
            {
                HasError = true;
                ErrorMessage = error ?? "Failed to record payment.";
                return;
            }

            // ".." pops THIS page (Payment) off the back-stack as part of
            // navigating to Receipt, so the back button from Receipt skips
            // right past Payment instead of ever landing back on it — that
            // was the actual ask: prevent back-navigation reuse, not gate
            // by date. (IsNothingDue above is a separate safety net for
            // when Payment page is reached fresh from elsewhere, like
            // Pay Now on the ledger, before anything is genuinely due.)
            await Shell.Current.GoToAsync(
    $"../{nameof(ReceiptPage)}" +
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