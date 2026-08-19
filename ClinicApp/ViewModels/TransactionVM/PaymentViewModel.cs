using ClinicApp.Helpers;
using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.TransactionVM;

// First-payment flow ONLY — reached exclusively from
// BillSummaryViewModel.Proceed(). Nothing about this bill exists in
// Supabase yet when this page opens: no bills row, no bill_items, no
// dental chart/tooth records, no treatment history, and no supply
// deduction. All of that gets written in ONE place — RecordPayment below,
// via BillingService.CreateBillAsync plus the supply-deduction loop right
// after it — and only once the entered amount actually clears validation
// and Record Payment is tapped. Simply opening this page and going back to
// Bill Summary (or backing out of the app entirely) writes nothing at all;
// there's no draft-vs-database reconciliation to worry about, because
// there's nothing in the database to reconcile against until payment
// genuinely happens.
public partial class PaymentViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;
    private readonly BillingService _billing;

    public PaymentViewModel(SupabaseDataService supabase, BillingService billing)
    {
        _supabase = supabase;
        _billing = billing;
    }

    [ObservableProperty]
    private string patientName = string.Empty;

    [ObservableProperty]
    private decimal paymentAmount;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    // Set the first time CreateBillAsync succeeds within this page's
    // lifetime. Guards against a narrower version of the old duplicate-bill
    // bug: if the bill gets created successfully but RecordPaymentAsync
    // then fails (e.g. a network hiccup) and staff tap Record Payment
    // again, this makes the retry reuse the bill that already exists
    // instead of creating a second one (and skips deducting supplies a
    // second time too — see below).
    private string? _pendingBillId;

    public void LoadDraft()
    {
        var draft = BillDraftStore.Current;

        PaymentAmount = 0;
        HasError = false;
        _pendingBillId = null;

        PatientName = draft?.PatientName ?? string.Empty;

        OnPropertyChanged(nameof(IsInstallment));
        OnPropertyChanged(nameof(InstallmentDisplay));
        OnPropertyChanged(nameof(DueDateDisplay));
        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(DiscountDisplay));
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(MinimumDueTodayDisplay));
        OnPropertyChanged(nameof(BalanceDisplay));
    }

    private BillDraft? Draft => BillDraftStore.Current;

    public bool IsInstallment => Draft?.IsInstallment ?? false;

    public string InstallmentDisplay => Draft?.InstallmentSummary ?? string.Empty;

    // No real due date exists yet — nothing's been created. This is a
    // preview only, using the same "+1 month from today" rule
    // BillingService.CreateBillAsync itself uses when it sets the real
    // DueDate at creation time, so what's shown here matches what the
    // bill will actually get once Record Payment is tapped.
    public string DueDateDisplay =>
        IsInstallment
            ? DateTime.Now.AddMonths(1).ToString("MMM dd, yyyy")
            : "—";

    public string SubtotalDisplay => $"₱{Draft?.Subtotal ?? 0:N2}";
    public string DiscountDisplay => $"₱{Draft?.DiscountAmount ?? 0:N2}";
    public string TotalDisplay => $"₱{Draft?.Total ?? 0:N2}";

    // "Due Today" — draft.AmountDueToday is already the exact figure
    // BillingService.CreateBillAsync will use as the new bill's
    // MinimumDueToday, computed client-side in BillSummaryViewModel with
    // no DB round-trip needed (unlike AdditionalPaymentViewModel's
    // existing-bill case, where it has to be fetched live from bill_items
    // that already exist in Supabase).
    public decimal MinimumDueToday => Draft?.AmountDueToday ?? 0;

    public string MinimumDueTodayDisplay => $"₱{MinimumDueToday:N2}";

    // Shown only in the "amount too large" warning text — before creation,
    // Balance and Total are the same thing (nothing's been paid yet).
    public string BalanceDisplay => TotalDisplay;

    public string PaymentAmountDisplay => $"₱{PaymentAmount:N2}";

    // No "nothing due yet" case here (unlike AdditionalPaymentViewModel) —
    // this is always, by definition, the very first payment on a bill
    // that doesn't exist yet, so the minimum is always genuinely required.
    public bool IsBelowMinimum =>
        MinimumDueToday > 0 && PaymentAmount < MinimumDueToday;

    public bool IsAmountTooLarge =>
        Draft != null && Draft.Total > 0 && PaymentAmount > Draft.Total * 2;

    private decimal RequiredAmount =>
        MinimumDueToday > 0
            ? MinimumDueToday
            : Math.Min(PaymentAmount, Draft?.Total ?? 0);

    public decimal Change =>
        !IsAmountTooLarge && PaymentAmount > RequiredAmount
            ? PaymentAmount - RequiredAmount
            : 0;

    public string ChangeDisplay => $"₱{Change:N2}";

    public bool HasChange => Change > 0;

    partial void OnPaymentAmountChanged(decimal value)
    {
        if (value < 0)
        {
            PaymentAmount = 0;
            return;
        }

        OnPropertyChanged(nameof(PaymentAmountDisplay));
        OnPropertyChanged(nameof(IsBelowMinimum));
        OnPropertyChanged(nameof(IsAmountTooLarge));
        OnPropertyChanged(nameof(Change));
        OnPropertyChanged(nameof(ChangeDisplay));
        OnPropertyChanged(nameof(HasChange));
        if (HasError) HasError = false;
    }

    [RelayCommand]
    private async Task RecordPayment()
    {
        var draft = Draft;
        if (draft == null)
            return;

        if (PaymentAmount <= 0)
        {
            await Shell.Current.DisplayAlert(
                "Enter an Amount",
                "Enter how much the patient is paying.",
                "OK");
            return;
        }

        if (IsBelowMinimum)
        {
            await Shell.Current.DisplayAlert(
                "Payment Too Low",
                $"Minimum payment today is {MinimumDueTodayDisplay}.",
                "OK");
            return;
        }

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
            var billId = _pendingBillId;

            // Only actually create the bill (and everything that comes
            // with it — bill_items, tooth/chart records, treatment
            // history, and supply deduction) the first time through. If
            // this is a retry after RecordPaymentAsync failed below on a
            // previous attempt, _pendingBillId is already set and this
            // whole step is skipped — the bill already exists (and
            // supplies were already deducted) from that first attempt.
            if (billId == null)
            {
                var billResult = await _billing.CreateBillAsync(
                    draft, draft.AppointmentEntryId, draft.SupabaseEntryId);

                if (!billResult.Success || billResult.Bill == null)
                {
                    HasError = true;
                    ErrorMessage = billResult.ErrorMessage ?? "Failed to create bill.";
                    return;
                }

                billId = billResult.Bill.Id;
                _pendingBillId = billId;

                // Auto-deduct linked supplies for every service on this
                // bill — moved here from BillSummaryViewModel.Proceed()
                // now that bill creation itself happens here instead of on
                // Bill Summary. Runs only on this first successful
                // creation (guarded by the same billId == null check
                // above), so a retry after a later RecordPaymentAsync
                // failure won't deduct stock a second time.
                var lowStockItems = new List<string>();
                foreach (var service in draft.Services)
                {
                    var (_, insufficient) = await _supabase.DeductSuppliesForServiceAsync(
                        service.ServiceId, draft.PatientId, draft.PatientName, service.Quantity);
                    lowStockItems.AddRange(insufficient);
                }

                if (lowStockItems.Count > 0)
                {
                    await Shell.Current.DisplayAlert(
                        "Low Stock Warning",
                        $"These items are now low/out of stock: {string.Join(", ", lowStockItems.Distinct())}",
                        "OK");
                }
            }

            var amountToRecord = Math.Min(RequiredAmount, draft.Total);

            var (success, error) =
                await _supabase.RecordPaymentAsync(billId, amountToRecord);

            if (!success)
            {
                HasError = true;
                ErrorMessage = error ?? "Failed to record payment.";
                return;
            }

            var amountReceived = PaymentAmount;
            var change = Change;

            // Done with this draft — clear it so nothing stale lingers if
            // this ViewModel instance somehow gets revisited. Captured
            // above BEFORE clearing: Change is a computed property that
            // reads Draft (via RequiredAmount -> MinimumDueToday -> Draft),
            // so evaluating it after this line would silently collapse to
            // the wrong figure once Draft is gone.
            BillDraftStore.Current = null;

            await Shell.Current.GoToAsync(
                $"../{nameof(ReceiptPage)}" +
                $"?billId={billId}" +
                $"&patientName={Uri.EscapeDataString(draft.PatientName)}" +
                $"&patientId={Uri.EscapeDataString(draft.PatientId)}" +
                $"&appointmentEntryId={Uri.EscapeDataString(draft.AppointmentEntryId ?? string.Empty)}" +
                $"&supabaseEntryId={Uri.EscapeDataString(draft.SupabaseEntryId ?? string.Empty)}" +
                $"&supabaseBookingId={Uri.EscapeDataString(draft.SupabaseBookingId ?? string.Empty)}" +
                $"&amountReceived={amountReceived}" +
                $"&change={change}");
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
}
