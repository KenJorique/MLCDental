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
    readonly SupabaseDataService _supabase;

    public ObservableCollection<ServiceLineItem> Services { get; } = new();

    [ObservableProperty] string patientName = "";
    [ObservableProperty] decimal subtotal;
    [ObservableProperty] decimal discountPercent;
    [ObservableProperty] decimal discountAmount;
    [ObservableProperty] decimal total;
    [ObservableProperty] bool isInstallment;
    [ObservableProperty] bool isBusy;
    [ObservableProperty] string createdBillId = "";
    [ObservableProperty] string createdBillNumber = "";
    [ObservableProperty] int installmentMonths = 3;
    [ObservableProperty] decimal monthlyPayment;

    public bool HasDiscount => DiscountPercent > 0;
    public bool HasInstallmentService => Services.Any(x => x.IsInstallmentEligible);
    public bool HasServices => Services.Count > 0;
    public int TotalItems => Services.Count;

    public string InstallmentSummary =>
        IsInstallment && InstallmentMonths > 0
            ? $"{InstallmentMonths} months @ ₱{MonthlyPayment:N2}/month"
            : string.Empty;

    public string SubtotalDisplay => $"₱{Subtotal:N2}";
    public string DiscountDisplay => $"₱{DiscountAmount:N2}";
    public string TotalDisplay => $"₱{Total:N2}";

    public BillSummaryViewModel(BillingService billing, SupabaseDataService supabase)
    {
        _billing = billing;
        _supabase = supabase;
        LoadDraft();
    }

    public void LoadDraft()
    {
        if (BillDraftStore.Current == null)
            return;

        var draft = BillDraftStore.Current;

        PatientName = draft.PatientName;
        IsInstallment = draft.IsInstallment;
        InstallmentMonths = draft.InstallmentMonths > 0 ? draft.InstallmentMonths : 3;

        Services.Clear();
        foreach (var item in draft.Services)
            Services.Add(item);

        CalculateTotals();
    }

    partial void OnIsInstallmentChanged(bool value) => CalculateTotals();
    partial void OnInstallmentMonthsChanged(int value) => CalculateTotals();

    partial void OnDiscountPercentChanged(decimal value)
    {
        if (BillDraftStore.Current != null)
            BillDraftStore.Current.DiscountPercent = value;

        CalculateTotals();
    }

    private void CalculateTotals()
    {
        Subtotal = Services.Sum(x => x.Subtotal);
        DiscountAmount = Math.Round(Subtotal * DiscountPercent, 2);
        Total = Subtotal - DiscountAmount;

        if (IsInstallment && InstallmentMonths > 0)
            MonthlyPayment = Math.Round(Total / InstallmentMonths, 2);
        else
            MonthlyPayment = 0;

        if (BillDraftStore.Current != null)
        {
            BillDraftStore.Current.Subtotal = Subtotal;
            BillDraftStore.Current.DiscountPercent = DiscountPercent;
            BillDraftStore.Current.DiscountAmount = DiscountAmount;
            BillDraftStore.Current.Total = Total;
            BillDraftStore.Current.IsInstallment = IsInstallment;
            BillDraftStore.Current.InstallmentMonths = IsInstallment ? InstallmentMonths : 0;
            BillDraftStore.Current.MonthlyPayment = MonthlyPayment;
        }

        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(DiscountDisplay));
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(InstallmentSummary));
        OnPropertyChanged(nameof(HasInstallmentService));
        OnPropertyChanged(nameof(HasServices));
        OnPropertyChanged(nameof(TotalItems));
        OnPropertyChanged(nameof(HasDiscount));
        ProceedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    async Task RemoveService(ServiceLineItem item)
    {
        if (item == null) return;

        bool confirm = await Shell.Current.CurrentPage.DisplayAlert(
            "Remove Service",
            $"Remove \"{item.ServiceName}\" from this bill?",
            "Remove", "Cancel");

        if (!confirm) return;

        Services.Remove(item);
        BillDraftStore.Current?.Services.Remove(item);
        CalculateTotals();
    }

    [RelayCommand]
    async Task Back() => await Shell.Current.GoToAsync("..");

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

            // ── Auto-deduct linked supplies for every service on this bill ──
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