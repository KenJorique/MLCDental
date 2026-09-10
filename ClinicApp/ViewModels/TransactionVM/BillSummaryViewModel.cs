using ClinicApp.Helpers;
using ClinicApp.Models;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Models.TransactionModels;
using ClinicApp.Services;
using ClinicApp.Views.AppointmentRelated;
using ClinicApp.Views.TransactionRelated;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using static ClinicApp.Helpers.BillDraftStore;

namespace ClinicApp.ViewModels.TransactionVM;

public partial class BillSummaryViewModel : ObservableObject
{
    readonly SupabaseDataService _supabase;

    public ObservableCollection<ServiceLineItem> Services { get; } = new();

    [ObservableProperty]
    string patientName = "";

    [ObservableProperty]
    decimal subtotal;

    [ObservableProperty]
    decimal discountPercent;

    [ObservableProperty]
    decimal discountAmount;

    // Flat peso discount option — when on, the entered amount is used directly instead of DiscountPercent (capped to what's eligible).
    [ObservableProperty]
    bool isSpecialDiscount;

    [ObservableProperty]
    decimal specialDiscountAmount;

    [ObservableProperty]
    decimal total;

    // Installment is a per-service decision now (see ServiceLineItem.IsInstallmentSelected / SelectedInstallmentMonths).
    [ObservableProperty]
    decimal amountDueToday;

    [ObservableProperty]
    bool isBusy;

    // ── Follow-up detection — dentist sets the next-session date right here, before moving on to payment ──
    public ObservableCollection<FollowUpDisplayItem> PendingFollowUps { get; } = new();

    [ObservableProperty]
    bool showFollowUpSheet;

    FollowUpRequiredSheet? _followUpSheet;

    // True if either a percent or a flat discount is active.
    public bool HasDiscount => DiscountPercent > 0 || SpecialDiscountAmount > 0;

    // True if any service on the bill is eligible for an installment plan.
    public bool HasInstallmentService =>
        Services.Any(x => x.IsInstallmentEligible);

    // Discount is disabled only when every service on the bill is installment-eligible.
    public bool CanApplyDiscount =>
        Services.Any(x => !x.IsInstallmentEligible);

    // True on a mixed bill where discount applies to only part of it — drives the "Excludes installment items" hint.
    public bool HasMixedInstallmentAndRegular =>
        HasInstallmentService && CanApplyDiscount;

    // True if the bill currently has at least one service line.
    public bool HasServices => Services.Count > 0;

    // Number of service lines on the bill.
    public int TotalItems => Services.Count;

    public string SubtotalDisplay => $"₱{Subtotal:N2}";
    public string DiscountDisplay => $"₱{DiscountAmount:N2}";
    public string TotalDisplay => $"₱{Total:N2}";
    public string AmountDueTodayDisplay => $"₱{AmountDueToday:N2}";

    // Injects the shared data service, then loads the current draft.
    public BillSummaryViewModel(SupabaseDataService supabase)
    {
        _supabase = supabase;
        LoadDraft();
    }

    // Pulls the active BillDraftStore draft into this ViewModel and recalculates totals.
    public void LoadDraft()
    {
        if (BillDraftStore.Current == null)
            return;

        var draft = BillDraftStore.Current;

        PatientName = draft.PatientName;

        // Unsubscribe from any items left over from a previous load before clearing.
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

    // Recalculates totals whenever a service line's installment choice or subtotal changes.
    void OnServiceItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ServiceLineItem.IsInstallmentSelected)
                            or nameof(ServiceLineItem.SelectedInstallmentMonths)
                            or nameof(ServiceLineItem.Subtotal))
        {
            CalculateTotals();
        }
    }

    // Keeps the draft's DiscountPercent in sync and recalculates.
    partial void OnDiscountPercentChanged(decimal value)
    {
        if (BillDraftStore.Current != null)
            BillDraftStore.Current.DiscountPercent = value;

        CalculateTotals();
    }

    // Recalculates whenever the flat discount amount changes.
    partial void OnSpecialDiscountAmountChanged(decimal value)
    {
        CalculateTotals();
    }

    // Recalculates whenever the special-discount toggle changes.
    partial void OnIsSpecialDiscountChanged(bool value)
    {
        CalculateTotals();
    }

    // Recomputes subtotal, discount, total, and amount due today, and mirrors them into the draft.
    private void CalculateTotals()
    {
        Subtotal = Services.Sum(x => x.Subtotal);

        // Discount excludes any installment-eligible service, regardless of whether a plan was actually chosen.
        var discountEligibleSubtotal = Services
            .Where(x => !x.IsInstallmentEligible)
            .Sum(x => x.Subtotal);

        DiscountAmount = !CanApplyDiscount
            ? 0m
            : IsSpecialDiscount
                ? Math.Min(SpecialDiscountAmount, discountEligibleSubtotal)
                : Math.Round(discountEligibleSubtotal * DiscountPercent, 2);

        Total = Subtotal - DiscountAmount;

        // Due today = sum of each item's own contribution (full price, or down payment if on a plan), minus the discount.
        AmountDueToday = Services.Sum(x => x.AmountDueToday) - DiscountAmount;

        if (BillDraftStore.Current != null)
        {
            BillDraftStore.Current.Subtotal = Subtotal;
            BillDraftStore.Current.DiscountPercent = DiscountPercent;
            BillDraftStore.Current.DiscountAmount = DiscountAmount;
            BillDraftStore.Current.Total = Total;
            BillDraftStore.Current.AmountDueToday = AmountDueToday;

            // Bridging fields for bill-level Supabase columns until payment allocation moves fully to bill_items.
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

    // Removes a service line from the bill after confirmation.
    [RelayCommand]
    async Task RemoveService(ServiceLineItem item)
    {
        if (item == null)
            return;

        var popup = new ConfirmationPopup(
            "Remove Service",
            $"Remove \"{item.ServiceName}\" from this bill?",
            "Remove", Colors.Crimson);
        var result = await Shell.Current.CurrentPage.ShowPopupAsync(popup);
        bool confirm = result is bool b && b;

        if (!confirm)
            return;

        item.PropertyChanged -= OnServiceItemPropertyChanged;
        Services.Remove(item);
        BillDraftStore.Current?.Services.Remove(item);

        CalculateTotals();
    }

    // Navigates back without changing the draft.
    [RelayCommand]
    async Task Back()
    {
        await Shell.Current.GoToAsync("..");
    }

    // Proceed is only enabled while there are services and no operation is in flight.
    bool CanProceed() => HasServices && !IsBusy;

    // Checks for services needing a follow-up session, then navigates to PaymentPage once scheduling is resolved.
    [RelayCommand(CanExecute = nameof(CanProceed))]
    async Task Proceed()
    {
        var draft = BillDraftStore.Current;
        if (draft == null)
            return;

        IsBusy = true;
        ProceedCommand.NotifyCanExecuteChanged();

        try
        {
            var newlyOpenedFollowUps = new List<FollowUpDisplayItem>();
            foreach (var line in draft.Services)
            {
                var service = await _supabase.GetServiceByIdAsync(line.ServiceId);
                if (service == null || !service.RequiresMultipleSessions)
                    continue;

                // Read-only preview — nothing is written to Supabase until payment succeeds.
                var previewRow = await _supabase.PreviewNextSessionAsync(
                    draft.PatientId, draft.PatientName, service, draft.SupabaseBookingId);

                if (previewRow != null && previewRow.Status == "awaiting_schedule")
                {
                    var item = new FollowUpDisplayItem(previewRow, _supabase);
                    await item.InitializeAsync();
                    newlyOpenedFollowUps.Add(item);
                }
            }

            if (newlyOpenedFollowUps.Count > 0)
            {
                PendingFollowUps.Clear();
                foreach (var f in newlyOpenedFollowUps)
                    PendingFollowUps.Add(f);

                _followUpSheet = new FollowUpRequiredSheet { BindingContext = this };
                ShowFollowUpSheet = true;
                await _followUpSheet.ShowAsync();
                return;
            }

            await Shell.Current.GoToAsync(nameof(PaymentPage));
        }
        finally
        {
            IsBusy = false;
            ProceedCommand.NotifyCanExecuteChanged();
        }
    }

    // Dismisses the follow-up sheet if it's currently open.
    public async Task CloseFollowUpSheetAsync()
    {
        if (_followUpSheet == null) return;
        var sheet = _followUpSheet;
        _followUpSheet = null;
        try { await sheet.DismissAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BillSummary] CloseFollowUpSheet: {ex.Message}"); }
    }

    // Confirms, then records the dentist's picked slot for this follow-up locally (not booked yet).
    [RelayCommand]
    async Task CreateFollowUpNow(FollowUpDisplayItem item)
    {
        if (item == null || item.SelectedSlot == null) return;

        var popup = new ConfirmationPopup(
            "Confirm Follow-Up",
            $"Book the follow-up for {item.ServiceName} on {item.SelectedSummary}?",
            "Confirm", Color.FromArgb("#2E7D32"));
        var result = await Shell.Current.CurrentPage.ShowPopupAsync(popup);
        if (result is not bool ok || !ok)
            return;

        BillDraftStore.Current?.PendingFollowUps.Add(new PendingFollowUpChoice
        {
            Row = item.Sequence,
            SelectedSlotLocal = item.SelectedSlotLocal,
            SelectedSlotUtc = item.SelectedSlotUtc
        });

        PendingFollowUps.Remove(item);

        if (PendingFollowUps.Count == 0)
        {
            ShowFollowUpSheet = false;
            await CloseFollowUpSheetAsync();
            await Shell.Current.GoToAsync(nameof(PaymentPage));
        }
    }

    // Defers every remaining follow-up — recorded with no chosen slot, so it lands as "awaiting_schedule" once payment succeeds.
    [RelayCommand]
    async Task ContinueToPayment()
    {
        foreach (var item in PendingFollowUps)
        {
            BillDraftStore.Current?.PendingFollowUps.Add(new PendingFollowUpChoice
            {
                Row = item.Sequence,
                SelectedSlotLocal = null,
                SelectedSlotUtc = null
            });
        }

        ShowFollowUpSheet = false;
        await CloseFollowUpSheetAsync();
        await Shell.Current.GoToAsync(nameof(PaymentPage));
    }
}
