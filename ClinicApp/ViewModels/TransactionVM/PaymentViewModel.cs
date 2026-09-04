using ClinicApp.Models.SupabaseModels;
using ClinicApp.Models.TransactionModels;
using ClinicApp.Helpers;
using ClinicApp.Services;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.TransactionVM;

public partial class PaymentViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;
    private readonly BillingService _billing;

    // Injects the shared data service and billing service.
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

    private string? _pendingBillId;

    // Resets the form and pulls the current bill draft's display values.
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

    public string DueDateDisplay =>
        IsInstallment
            ? DateTime.Now.AddMonths(1).ToString("MMM dd, yyyy")
            : "—";

    public string SubtotalDisplay => $"₱{Draft?.Subtotal ?? 0:N2}";
    public string DiscountDisplay => $"₱{Draft?.DiscountAmount ?? 0:N2}";
    public string TotalDisplay => $"₱{Draft?.Total ?? 0:N2}";

    // Amount due today, computed client-side from the draft (no DB round-trip needed).
    public decimal MinimumDueToday => Draft?.AmountDueToday ?? 0;

    public string MinimumDueTodayDisplay => $"₱{MinimumDueToday:N2}";

    // Before creation, Balance equals Total — nothing's been paid yet.
    public string BalanceDisplay => TotalDisplay;

    public string PaymentAmountDisplay => $"₱{PaymentAmount:N2}";

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

    // Clamps negative input and refreshes the computed display properties.
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

    // Creates the bill (first attempt only) and records the payment, logging both.
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

            // Only creates the bill on the first attempt — a retry after a failed RecordPaymentAsync reuses _pendingBillId.
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

                await _supabase.LogActivityAsync("AppointmentCompleted", $"{draft.PatientName}'s appointment was completed");

                // Deducts linked supplies for every service on this bill (only runs on first creation).
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

            await _supabase.LogActivityAsync("Payment", $"{draft.PatientName} paid ₱{amountToRecord:N2}");

            var amountReceived = PaymentAmount;
            var change = Change;

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
