using ClinicApp.Models;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.TransactionVM;

// Pays down an EXISTING bill's balance (from the Ledger or Bill Details "Add Payment" button).
// Kept separate from PaymentViewModel, which only handles the first payment on a brand-new bill.
[QueryProperty(nameof(BillId), "billId")]
[QueryProperty(nameof(PatientId), "patientId")]
[QueryProperty(nameof(PatientName), "patientName")]
public partial class AdditionalPaymentViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;

    // Injects the shared data service.
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

    // Every payment on this bill, newest first — full trail, not just the latest.
    public ObservableCollection<SupabasePayment> PaymentHistory { get; } = new();

    public bool HasPaymentHistory => PaymentHistory.Count > 0;
    public bool HasNoPaymentHistory => !HasPaymentHistory;

    // Loads the bill once BillId is set via navigation.
    partial void OnBillIdChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await LoadBillAsync());
        }
    }

    // Loads the bill and its payment history, then refreshes computed display properties.
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

    // Remaining amount owed (Total − AmountPaid), the headline figure on this page.
    public string BalanceDisplay => Bill == null ? "₱0.00" : $"₱{Bill.Balance:N2}";

    public bool IsAlreadyPaid => Bill != null && Bill.Balance <= 0;

    public string PaymentAmountDisplay => $"₱{PaymentAmount:N2}";

    // No forced minimum — whatever's typed is required, capped at the remaining balance.
    private decimal RequiredAmount =>
        Bill == null ? 0 : Math.Min(PaymentAmount, Bill.Balance);

    // Flags an amount far larger than what's owed (e.g. an extra typed zero).
    public bool IsAmountTooLarge =>
        Bill != null && Bill.Balance > 0 && PaymentAmount > Bill.Balance * 2;

    public decimal Change =>
        !IsAmountTooLarge && Bill != null && PaymentAmount > RequiredAmount
            ? PaymentAmount - RequiredAmount
            : 0;

    public string ChangeDisplay => $"₱{Change:N2}";

    public bool HasChange => Change > 0;

    // Clamps negative input and refreshes the computed display properties.
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

    // Validates, records the payment, logs it, and moves to the receipt.
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

            await _supabase.LogActivityAsync("Payment", $"{PatientName} paid ₱{amountToRecord:N2}");

            // Uses "../ReceiptPage" so Receipt's back button also skips this page.
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
