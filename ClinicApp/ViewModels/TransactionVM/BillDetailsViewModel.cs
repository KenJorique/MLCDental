using ClinicApp.Models.SupabaseModels;
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

    // Injects the shared data service.
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

    // Loads the bill, its items, and its payments. Called once from OnAppearing — not from a BillId watcher, to avoid a duplicate-load race.
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

    // Opens Additional Payment for this existing bill.
    [RelayCommand]
    private async Task AddPayment()
    {
        if (Bill == null || Bill.Balance <= 0)
            return;

        // Existing bill, so this goes to AdditionalPaymentPage, not the new-bill PaymentPage.
        await Shell.Current.GoToAsync(
            $"{nameof(AdditionalPaymentPage)}" +
            $"?billId={Bill.Id}" +
            $"&patientId={Uri.EscapeDataString(PatientId)}" +
            $"&patientName={Uri.EscapeDataString(PatientName)}");
    }

    // Opens the receipt for this bill.
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

    // Expands/collapses the tapped bill item.
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