using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.TransactionVM;

[QueryProperty(nameof(BillId), "billId")]
[QueryProperty(nameof(PatientId), "patientId")]
[QueryProperty(nameof(PatientName), "patientName")]
public partial class BillDetailsViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;

    public BillDetailsViewModel(
        SupabaseDataService supabase)
    {
        _supabase = supabase;
    }

    [ObservableProperty]
    string billId = "";

    [ObservableProperty]
    string patientId = "";

    [ObservableProperty]
    string patientName = "";

    [ObservableProperty]
    bool isBusy;
    public string DueDateDisplay =>
    Bill?.DueDateDisplay ?? "—";

    public string LastPaymentDateDisplay =>
        Bill?.LastPaymentDateDisplay ?? "—";

    public bool HasBalance => Bill != null && Bill.Balance > 0;

    [ObservableProperty]
    SupabaseBill? bill;

    public ObservableCollection<SupabaseBillItem> Items { get; }
        = new();

    public ObservableCollection<SupabasePayment> Payments { get; }
        = new();

    // NOTE: BillId is set by Shell's QueryProperty before OnAppearing()
    // runs, and BillDetailsPage.OnAppearing() already calls LoadAsync()
    // explicitly. Also triggering LoadAsync() here on every BillId change
    // meant two concurrent loads raced: both cleared Items/Payments, both
    // awaited their own fetch, then both appended -- producing duplicate
    // rows whenever the second load's Clear() ran after the first load's
    // Add() had already started. Removed so there's a single, predictable
    // trigger (OnAppearing) per page visit.

    public async Task LoadAsync()
    {
        IsBusy = true;

        try
        {
            Items.Clear();
            Payments.Clear();

            Bill = await _supabase.GetBillByIdAsync(BillId);
            OnPropertyChanged(nameof(HasBalance));

            var items =
                await _supabase.GetBillItemsAsync(BillId);

            foreach (var item in items)
                Items.Add(item);

            var payments =
                await _supabase.GetPaymentsForBillAsync(BillId);

            foreach (var payment in payments)
                Payments.Add(payment);

            OnPropertyChanged(nameof(HasBalance));
            OnPropertyChanged(nameof(BillNumber));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(VisitDate));
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(DiscountDisplay));
            OnPropertyChanged(nameof(TotalDisplay));
            OnPropertyChanged(nameof(PaidDisplay));
            OnPropertyChanged(nameof(BalanceDisplay));
            OnPropertyChanged(nameof(DueDateDisplay));
            OnPropertyChanged(nameof(LastPaymentDateDisplay));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddPayment()
    {
        if (Bill == null || Bill.Balance <= 0)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(PaymentPage)}" +
            $"?billId={Bill.Id}" +
            $"&patientId={Uri.EscapeDataString(PatientId)}" +
            $"&patientName={Uri.EscapeDataString(PatientName)}");
    }

    [RelayCommand]
    private async Task ViewReceipt()
    {
        if (Bill == null)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(ReceiptPage)}" +
            $"?billId={Bill.Id}" +
            $"&patientId={Uri.EscapeDataString(PatientId)}" +
            $"&patientName={Uri.EscapeDataString(PatientName)}");
    }

    [RelayCommand]
    private void ToggleItem(SupabaseBillItem item)
    {
        if (item == null)
            return;

        item.IsExpanded = !item.IsExpanded;
    }


    public string BillNumber =>
        Bill?.BillNumber ?? "";

    public string Status =>
        Bill?.StatusDisplay ?? "";

    public string VisitDate =>
        Bill == null
            ? ""
            : Bill.VisitDate.ToString("MMMM dd, yyyy");

    public string SubtotalDisplay =>
        $"₱{Bill?.Subtotal ?? 0:N2}";

    public string DiscountDisplay =>
        $"₱{Bill?.DiscountAmount ?? 0:N2}";

    public string TotalDisplay =>
        $"₱{Bill?.TotalAmount ?? 0:N2}";

    public string PaidDisplay =>
        $"₱{Bill?.AmountPaid ?? 0:N2}";

    public string BalanceDisplay =>
        $"₱{Bill?.Balance ?? 0:N2}";
}