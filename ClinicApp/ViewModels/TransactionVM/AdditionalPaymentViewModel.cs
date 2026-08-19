using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.TransactionVM;

// Handles paying down an EXISTING bill's remaining balance — reached from
// the Transaction/Ledger page's "Add payment" pill, or from BillDetailsPage's
// "Add Payment" button. Deliberately kept separate from PaymentViewModel
// (which is only for the very first payment on a brand-new bill, straight
// out of Bill Summary): the two screens show different information (Balance
// + last-paid-date here, vs. Due Today there) and have different validation
// rules (no forced minimum here — see IsAlreadyPaid below), so branching one
// ViewModel for both would mean juggling two sets of rules in the same
// place. Splitting them keeps each one simple and keeps the first-payment
// flow from BillSummaryViewModel completely unaffected by this one.
[QueryProperty(nameof(BillId), "billId")]
[QueryProperty(nameof(PatientId), "patientId")]
[QueryProperty(nameof(PatientName), "patientName")]
public partial class AdditionalPaymentViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;

    public AdditionalPaymentViewModel(SupabaseDataService supabase)
    {
        _supabase = supabase;
    }

    [ObservableProperty]
    private string billId = string.Empty;

    [ObservableProperty]
    private string patientId = string.Empty;

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

    // Every payment recorded on this bill so far, newest first — shown as
    // a payment history list rather than just the single most recent one,
    // so staff can see the full trail (useful on installment bills with
    // several advance/partial payments) before recording another.
    public ObservableCollection<SupabasePayment> PaymentHistory { get; } = new();

    public bool HasPaymentHistory => PaymentHistory.Count > 0;
    public bool HasNoPaymentHistory => !HasPaymentHistory;

    partial void OnBillIdChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await LoadBillAsync());
        }
    }

    private async Task LoadBillAsync()
    {
        IsBusy = true;

        try
        {
            Bill = await _supabase.GetBillByIdAsync(BillId);

            var payments = await _supabase.GetPaymentsForBillAsync(BillId);

            PaymentHistory.Clear();
            foreach (var p in payments.OrderByDescending(p => p.PaymentDate))
                PaymentHistory.Add(p);

            OnPropertyChanged(nameof(BillNumber));
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(DiscountDisplay));
            OnPropertyChanged(nameof(TotalDisplay));
            OnPropertyChanged(nameof(PaidDisplay));
            OnPropertyChanged(nameof(BalanceDisplay));
            OnPropertyChanged(nameof(HasPaymentHistory));
            OnPropertyChanged(nameof(HasNoPaymentHistory));
            OnPropertyChanged(nameof(IsAlreadyPaid));
        }
        finally
        {
            IsBusy = false;
        }
    }

    public string BillNumber => Bill?.BillNumber ?? "";

    public string SubtotalDisplay => Bill == null ? "₱0.00" : $"₱{Bill.Subtotal:N2}";
    public string DiscountDisplay => Bill == null ? "₱0.00" : $"₱{Bill.DiscountAmount:N2}";
    public string TotalDisplay => Bill == null ? "₱0.00" : $"₱{Bill.TotalAmount:N2}";
    // "Amount Paid" — matches the label already used for this same figure
    // on BillDetailsPage, so the wording is consistent across the app.
    public string PaidDisplay => Bill == null ? "₱0.00" : $"₱{Bill.AmountPaid:N2}";

    // "Balance" is the headline figure on this page — the whole remaining
    // amount owed on the bill (Total − AmountPaid), not a per-visit minimum
    // like PaymentPage's "Due Today". Bill.Balance already IS Total −
    // AmountPaid (kept in sync by RecordPaymentAsync), so no separate
    // calculation is needed here.
    public string BalanceDisplay => Bill == null ? "₱0.00" : $"₱{Bill.Balance:N2}";

    public bool IsAlreadyPaid => Bill != null && Bill.Balance <= 0;

    public string PaymentAmountDisplay => $"₱{PaymentAmount:N2}";

    // No forced minimum on this page — whatever staff typed is what's
    // required, capped at the remaining balance. Anything beyond the
    // balance is Change, same cash-register behavior as PaymentPage.
    private decimal RequiredAmount =>
        Bill == null ? 0 : Math.Min(PaymentAmount, Bill.Balance);

    // Flags an amount wildly larger than what's owed (an extra zero,
    // etc.) — same generous-multiple approach as PaymentViewModel, so a
    // normal "gave more cash, get change back" amount doesn't trip it.
    public bool IsAmountTooLarge =>
        Bill != null && Bill.Balance > 0 && PaymentAmount > Bill.Balance * 2;

    public decimal Change =>
        !IsAmountTooLarge && Bill != null && PaymentAmount > RequiredAmount
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
        OnPropertyChanged(nameof(IsAmountTooLarge));
        OnPropertyChanged(nameof(Change));
        OnPropertyChanged(nameof(ChangeDisplay));
        OnPropertyChanged(nameof(HasChange));
        if (HasError) HasError = false;
    }

    [RelayCommand]
    private async Task RecordPayment()
    {
        if (Bill == null)
            return;

        if (IsAlreadyPaid)
        {
            await Shell.Current.DisplayAlert(
                "Already Paid",
                "This bill is already fully paid.",
                "OK");
            return;
        }

        if (PaymentAmount <= 0)
        {
            await Shell.Current.DisplayAlert(
                "Enter an Amount",
                "Enter how much the patient is paying.",
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
            var amountToRecord = Math.Min(RequiredAmount, Bill.Balance);

            var (success, error) =
                await _supabase.RecordPaymentAsync(Bill.Id, amountToRecord);

            if (!success)
            {
                HasError = true;
                ErrorMessage = error ?? "Failed to record payment.";
                return;
            }

            // Same ".." pop-then-navigate pattern as PaymentViewModel, so
            // Receipt's back button skips past this page too. No
            // appointmentEntryId/supabaseEntryId/supabaseBookingId here —
            // those only apply to the fresh-bill-from-appointment flow;
            // ReceiptViewModel.Done() already no-ops cleanly when they're
            // blank.
            await Shell.Current.GoToAsync(
                $"../{nameof(ReceiptPage)}" +
                $"?billId={Bill.Id}" +
                $"&patientName={Uri.EscapeDataString(PatientName)}" +
                $"&patientId={Uri.EscapeDataString(PatientId)}" +
                $"&amountReceived={PaymentAmount}" +
                $"&change={Change}");
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
