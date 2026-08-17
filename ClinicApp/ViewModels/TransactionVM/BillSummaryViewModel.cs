using ClinicApp.Helpers;
using ClinicApp.Models;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using static ClinicApp.Helpers.BillDraftStore;

namespace ClinicApp.ViewModels.TransactionVM;

public partial class BillSummaryViewModel : ObservableObject
{
    public ObservableCollection<ServiceLineItem> Services { get; } = new();

    [ObservableProperty]
    string patientName = "";

    [ObservableProperty]
    decimal subtotal;

    [ObservableProperty]
    decimal discountPercent;

    [ObservableProperty]
    decimal discountAmount;

    // Special: a flat peso amount off (e.g. ₱150 off) rather than a
    // percentage. When on, DiscountPercent is unused — the entered amount
    // becomes the discount directly (capped so it can't exceed what's
    // actually eligible for discount).
    [ObservableProperty]
    bool isSpecialDiscount;

    [ObservableProperty]
    decimal specialDiscountAmount;

    [ObservableProperty]
    decimal total;

    // NOTE: installment is now a PER-SERVICE decision (see ServiceLineItem.
    // IsInstallmentSelected / SelectedInstallmentMonths) rather than one
    // toggle for the whole bill. "AmountDueToday" below is the sum of each
    // item's own contribution.

    [ObservableProperty]
    decimal amountDueToday;

    [ObservableProperty]
    bool isBusy;

    public bool HasDiscount => DiscountPercent > 0 || SpecialDiscountAmount > 0;

    public bool HasInstallmentService =>
        Services.Any(x => x.IsInstallmentEligible);

    // Discount is only fully disabled when EVERY service on the bill is
    // installment-eligible — i.e. there'd be nothing left for it to apply
    // to. A mixed bill (some installment, some not) still allows a
    // discount; it just applies only to the non-installment item(s).
    public bool CanApplyDiscount =>
        Services.Any(x => !x.IsInstallmentEligible);

    // Only true on a genuinely mixed bill — some installment, some not —
    // where the discount is still usable but doesn't cover everything.
    // Drives the "Excludes installment items" hint text.
    public bool HasMixedInstallmentAndRegular =>
        HasInstallmentService && CanApplyDiscount;

    public bool HasServices => Services.Count > 0;

    public int TotalItems => Services.Count;

    public string SubtotalDisplay => $"₱{Subtotal:N2}";
    public string DiscountDisplay => $"₱{DiscountAmount:N2}";
    public string TotalDisplay => $"₱{Total:N2}";
    public string AmountDueTodayDisplay => $"₱{AmountDueToday:N2}";

    public BillSummaryViewModel()
    {
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
    // toggle lives on the item itself (bound directly in the CollectionView
    // template) — ObservableCollection only raises CollectionChanged for
    // Add/Remove, not for a property changing on an item already inside it,
    // so we listen to each item directly to know when to recalculate.
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

    partial void OnSpecialDiscountAmountChanged(decimal value)
    {
        CalculateTotals();
    }

    partial void OnIsSpecialDiscountChanged(bool value)
    {
        CalculateTotals();
    }

    private void CalculateTotals()
    {
        Subtotal = Services.Sum(x => x.Subtotal);

        // Discount excludes any service that's eligible for installment,
        // regardless of whether the patient actually chose a plan for it.
        var discountEligibleSubtotal = Services
            .Where(x => !x.IsInstallmentEligible)
            .Sum(x => x.Subtotal);

        DiscountAmount = !CanApplyDiscount
            ? 0m
            : IsSpecialDiscount
                ? Math.Min(SpecialDiscountAmount, discountEligibleSubtotal)
                : Math.Round(discountEligibleSubtotal * DiscountPercent, 2);

        Total = Subtotal - DiscountAmount;

        // Due today = sum of each item's own contribution (full price, or
        // 50% down if on a plan), minus the discount.
        AmountDueToday = Services.Sum(x => x.AmountDueToday) - DiscountAmount;

        if (BillDraftStore.Current != null)
        {
            BillDraftStore.Current.Subtotal = Subtotal;
            BillDraftStore.Current.DiscountPercent = DiscountPercent;
            BillDraftStore.Current.DiscountAmount = DiscountAmount;
            BillDraftStore.Current.Total = Total;
            BillDraftStore.Current.AmountDueToday = AmountDueToday;

            // Bridging fields for the bill-level Supabase columns until the
            // payment-allocation rework moves this fully to bill_items.
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
        OnPropertyChanged(nameof(CanApplyDiscount));
        OnPropertyChanged(nameof(HasMixedInstallmentAndRegular));
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

        // Bill creation (and everything CreateBillAsync writes alongside
        // it — bill_items, dental chart/tooth records, treatment history,
        // and supply deduction) is now deferred to the moment Record
        // Payment succeeds on PaymentPage, not here. This is genuinely
        // just navigation — the draft in BillDraftStore.Current carries
        // everything PaymentPage needs, so there's nothing to pass in the
        // URL and nothing written to Supabase yet.
        await Shell.Current.GoToAsync(nameof(PaymentPage));
    }
}
