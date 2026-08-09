using ClinicApp.Helpers;
using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using static ClinicApp.Helpers.BillDraftStore;

namespace ClinicApp.ViewModels.TransactionVM;

public partial class BillSummaryViewModel : ObservableObject
{
    readonly BillingService _billing;

    public ObservableCollection<ServiceLineItem> Services { get; } = new();

    [ObservableProperty]
    string patientName = "";

    [ObservableProperty]
    decimal subtotal;

    [ObservableProperty]
    decimal discountPercent;

    [ObservableProperty]
    decimal discountAmount;

    [ObservableProperty]
    decimal total;

    [ObservableProperty]
    decimal amountDueToday;

    [ObservableProperty]
    bool isBusy;

    [ObservableProperty]
    string createdBillId = "";

    [ObservableProperty]
    string createdBillNumber = "";

    public bool HasDiscount => DiscountPercent > 0;

    public bool HasInstallmentService =>
        Services.Any(x => x.IsInstallmentEligible);

    public bool HasServices => Services.Count > 0;
    public int TotalItems => Services.Count;
    public string SubtotalDisplay => $"₱{Subtotal:N2}";
    public string DiscountDisplay => $"₱{DiscountAmount:N2}";
    public string TotalDisplay => $"₱{Total:N2}";
    public string AmountDueTodayDisplay => $"₱{AmountDueToday:N2}";

    public BillSummaryViewModel(BillingService billing)
    {
        _billing = billing;
        LoadDraft();
    }

    public void LoadDraft()
    {
        if (BillDraftStore.Current == null)
            return;

        var draft = BillDraftStore.Current;

        PatientName = draft.PatientName;

        // Unsubscribe from any items left over from a previous load
        // before clearing, so we don't leak handlers onto stale items.
        foreach (var old in Services)
            old.PropertyChanged -= OnServiceItemPropertyChanged;

        Services.Clear();
        foreach (var item in draft.Services)
        {
            Services.Add(item);
            item.PropertyChanged += OnServiceItemPropertyChanged;
        }

        CalculateTotals();
    }

    // Each item's own IsInstallmentSelected / SelectedInstallmentMonths
    // toggle lives on the item itself (bound directly in the CollectionView template)
    void OnServiceItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ServiceLineItem.IsInstallmentSelected)
                            or nameof(ServiceLineItem.SelectedInstallmentMonths)
                            or nameof(ServiceLineItem.Subtotal))
        {
            CalculateTotals();
        }
    }

    partial void OnDiscountPercentChanged(decimal value)
    {
        if (BillDraftStore.Current != null)
            BillDraftStore.Current.DiscountPercent = value;

        CalculateTotals();
    }

    private void CalculateTotals()
    {
        Subtotal = Services.Sum(x => x.Subtotal);

        // Discount only applies to services NOT on an installment plan —
        var discountEligibleSubtotal = Services
            .Where(x => !(x.IsInstallmentEligible && x.IsInstallmentSelected))
            .Sum(x => x.Subtotal);

        DiscountAmount = Math.Round(discountEligibleSubtotal * DiscountPercent, 2);
        Total = Subtotal - DiscountAmount;

        // Due today = sum of each item's own contribution (full price, or
        // 50% down if on a plan), minus the discount
        AmountDueToday = Services.Sum(x => x.AmountDueToday) - DiscountAmount;

        if (BillDraftStore.Current != null)
        {
            BillDraftStore.Current.Subtotal = Subtotal;
            BillDraftStore.Current.DiscountPercent = DiscountPercent;
            BillDraftStore.Current.DiscountAmount = DiscountAmount;
            BillDraftStore.Current.Total = Total;
            BillDraftStore.Current.AmountDueToday = AmountDueToday;
            BillDraftStore.Current.IsInstallment = HasInstallmentService &&
                Services.Any(x => x.IsInstallmentSelected);
            BillDraftStore.Current.InstallmentMonths = Services
                .Where(x => x.IsInstallmentSelected)
                .Select(x => x.SelectedInstallmentMonths)
                .DefaultIfEmpty(0)
                .Max();
            BillDraftStore.Current.MonthlyPayment = Services
                .Where(x => x.IsInstallmentSelected)
                .Sum(x => x.MonthlyPaymentAmount);
        }

        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(DiscountDisplay));
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(AmountDueTodayDisplay));
        OnPropertyChanged(nameof(HasInstallmentService));
        OnPropertyChanged(nameof(HasServices));
        OnPropertyChanged(nameof(TotalItems));
        OnPropertyChanged(nameof(HasDiscount));
        ProceedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    async Task RemoveService(ServiceLineItem item)
    {
        if (item == null)
            return;

        bool confirm = await Shell.Current.CurrentPage.DisplayAlert(
            "Remove Service",
            $"Remove \"{item.ServiceName}\" from this bill?",
            "Remove",
            "Cancel");

        if (!confirm)
            return;

        item.PropertyChanged -= OnServiceItemPropertyChanged;
        Services.Remove(item);
        BillDraftStore.Current?.Services.Remove(item);

        CalculateTotals();
    }

    [RelayCommand]
    async Task Back()
    {
        await Shell.Current.GoToAsync("..");
    }

    bool CanProceed() => HasServices && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanProceed))]
    async Task Proceed()
    {
        if (BillDraftStore.Current == null)
            return;

        IsBusy = true;
        ProceedCommand.NotifyCanExecuteChanged();

        try
        {
            var draft = BillDraftStore.Current;

            var result = await _billing.CreateBillAsync(
                draft,
                draft.AppointmentEntryId,
                draft.SupabaseEntryId);

            if (!result.Success)
            {
                await Shell.Current.DisplayAlert(
                    "Billing Error",
                    result.ErrorMessage ?? "Unable to create the bill. Please try again.",
                    "OK");

                return;
            }

            if (result.Bill == null)
            {
                await Shell.Current.DisplayAlert(
                    "Billing Error",
                    "Bill was not returned from Supabase.",
                    "OK");

                return;
            }

            CreatedBillStore.Current = result.Bill;

            await Shell.Current.GoToAsync(
                $"{nameof(PaymentPage)}" +
                $"?billId={result.Bill.Id}" +
                $"&patientId={Uri.EscapeDataString(result.Bill.PatientId)}" +
                $"&patientName={Uri.EscapeDataString(result.Bill.PatientName)}" +
                $"&appointmentEntryId={Uri.EscapeDataString(draft.AppointmentEntryId ?? string.Empty)}" +
                $"&supabaseEntryId={Uri.EscapeDataString(draft.SupabaseEntryId ?? string.Empty)}" +
                $"&supabaseBookingId={Uri.EscapeDataString(draft.SupabaseBookingId ?? string.Empty)}");
        }
        finally
        {
            IsBusy = false;
            ProceedCommand.NotifyCanExecuteChanged();
        }
    }
}