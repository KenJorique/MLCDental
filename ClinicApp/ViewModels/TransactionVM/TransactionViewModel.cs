using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.TransactionVM;

[QueryProperty(nameof(PatientId), "patientId")]
[QueryProperty(nameof(PatientName), "patientName")]
public partial class TransactionViewModel : ObservableObject
{
    readonly SupabaseDataService _supabase;
    readonly DatabaseService _database;
    public ObservableCollection<LedgerItem> Ledger { get; }
    = new();

    public ObservableCollection<SupabaseBill> Bills { get; } = new();
    public ObservableCollection<SupabaseBill> UnpaidBills { get; } = new();

    [ObservableProperty]
    string patientId = string.Empty;

    [ObservableProperty]
    string patientName = string.Empty;

    [ObservableProperty]
    bool isBusy;

    [ObservableProperty]
    bool isRefreshing;

    [ObservableProperty]
    decimal totalBilled;

    [ObservableProperty]
    decimal totalPaid;

    [ObservableProperty]
    decimal totalBalance;

    [ObservableProperty]
    bool hasBalance;

    [ObservableProperty]
    decimal outstandingBalance;

    [ObservableProperty]
    string paymentStatus = string.Empty;

    [ObservableProperty]
    DateTime? lastPaymentDate;

    [ObservableProperty]
    DateTime? dueDate;

    public int OverdueBillsCount => Bills.Count(b => b.IsOverdue);

    public string OverdueSummary =>
        OverdueBillsCount > 0
            ? $"{OverdueBillsCount} overdue"
            : "No overdue bills";
    public TransactionViewModel(
        SupabaseDataService supabase,
        DatabaseService database)
    {
        _supabase = supabase;
        _database = database;
    }

    public string LedgerSummary =>
        $"{Bills.Count} bill(s)";

    public string OutstandingDisplay =>
        $"₱{OutstandingBalance:N2}";

    public string TotalPaidDisplay =>
        $"₱{TotalPaid:N2}";

    public string LastPaymentDisplay =>
        LastPaymentDate == null
            ? "No payments"
            : LastPaymentDate.Value.ToString("MMM dd, yyyy");

    public string DueDateDisplay =>
        DueDate == null
            ? "--"
            : DueDate.Value.ToString("MMM dd, yyyy");

    partial void OnPatientIdChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await LoadBillsAsync());
        }
    }

    [RelayCommand]
    public async Task LoadBillsAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;

        try
        {
            System.Diagnostics.Debug.WriteLine(
           $"[TransactionVM] Loading bills for PatientId='{PatientId}'");
            var all = await _supabase.GetBillsForPatientAsync(PatientId);
            System.Diagnostics.Debug.WriteLine(
           $"[TransactionVM] GetBillsForPatientAsync returned {all.Count} bill(s)");

            foreach (var b in all)
                System.Diagnostics.Debug.WriteLine(
                    $"[TransactionVM]   Bill Id={b.Id} PatientId={b.PatientId} Total={b.TotalAmount}");
            
            Ledger.Clear();
            Bills.Clear();
            foreach (var bill in all)
                Bills.Add(bill);          // ← was missing entirely

            var items = new List<LedgerItem>();

            var paymentTasks = all.ToDictionary(
                bill => bill.Id,
                bill => _supabase.GetPaymentsForBillAsync(bill.Id));

            await Task.WhenAll(paymentTasks.Values);

            foreach (var bill in all)
            {
                items.Add(new LedgerItem
                {
                    BillId = bill.Id,
                    IsBill = true,
                    IsOverdue = bill.IsOverdue,
                    Title = "Bill Created",
                    Subtitle = bill.VisitDate.ToString("MMM dd, yyyy hh:mm tt"),
                    Reference = bill.BillNumber ?? bill.Id,
                    Amount = bill.TotalAmount,
                    RemainingBalance = bill.TotalAmount,
                    Date = bill.VisitDate
                });

                var payments = paymentTasks[bill.Id].Result;
                var runningBalance = bill.TotalAmount;

                foreach (var payment in payments.OrderBy(p => p.PaymentDate))
                {
                    runningBalance -= payment.Amount;
                    if (runningBalance < 0)
                        runningBalance = 0;

                    items.Add(new LedgerItem
                    {
                        BillId = bill.Id,
                        PaymentId = payment.Id,
                        IsPayment = true,
                        IsOverdue = bill.IsOverdue,
                        Title = "Payment",
                        Subtitle = payment.PaymentDate.ToString("MMM dd, yyyy hh:mm tt"),
                        Reference = bill.BillNumber ?? bill.Id,
                        Amount = payment.Amount,
                        RemainingBalance = runningBalance,
                        Date = payment.PaymentDate
                    });
                }
            }

            foreach (var item in items.OrderByDescending(x => x.Date))
                Ledger.Add(item);

            TotalBilled = Bills.Sum(x => x.TotalAmount);
            TotalPaid = Bills.Sum(x => x.AmountPaid);
            TotalBalance = Bills.Sum(x => x.Balance);

            OutstandingBalance = TotalBalance;
            HasBalance = OutstandingBalance > 0;

            if (OutstandingBalance == 0 && TotalBilled > 0)
                PaymentStatus = "Paid";
            else if (TotalPaid > 0)
                PaymentStatus = "Partially Paid";
            else
                PaymentStatus = "Unpaid";

            var latestPaidBill = Bills
                .Where(x => x.AmountPaid > 0)
                .OrderByDescending(x => x.VisitDate)
                .FirstOrDefault();
            LastPaymentDate = latestPaidBill?.VisitDate;

            var oldestUnpaidBill = Bills
                .Where(x => x.Balance > 0)
                .OrderBy(x => x.VisitDate)
                .FirstOrDefault();
            DueDate = oldestUnpaidBill?.VisitDate.AddDays(30);

            OnPropertyChanged(nameof(LedgerSummary));
            OnPropertyChanged(nameof(OutstandingDisplay));
            OnPropertyChanged(nameof(TotalPaidDisplay));
            OnPropertyChanged(nameof(LastPaymentDisplay));
            OnPropertyChanged(nameof(DueDateDisplay));
            OnPropertyChanged(nameof(NextDueDisplay));
            OnPropertyChanged(nameof(OverdueBillsCount));
            OnPropertyChanged(nameof(OverdueSummary));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TransactionVM] {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }   
    }

    [RelayCommand]
    async Task Refresh()
    {
        IsRefreshing = true;
        await LoadBillsAsync();
    }

    [RelayCommand]
    async Task OnAppearing()
    {
        await LoadBillsAsync();
    }

    [RelayCommand]
    async Task ViewDetails(SupabaseBill bill)
    {
        if (bill == null)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(BillDetailsPage)}" +
            $"?billId={bill.Id}" +
            $"&patientId={Uri.EscapeDataString(PatientId)}" +
            $"&patientName={Uri.EscapeDataString(PatientName)}");
    }

    [RelayCommand]
    async Task CreateNewBill()
    {
        await Shell.Current.GoToAsync(
            $"{nameof(Views.CreateBillPage)}" +
            $"?patientId={Uri.EscapeDataString(PatientId)}" +
            $"&patientName={Uri.EscapeDataString(PatientName)}");
    }

    [RelayCommand]
    private async Task OpenLedgerItem(LedgerItem item)
    {
        if (item == null)
            return;

        if (item.IsPayment)
        {
            await Shell.Current.GoToAsync(
                $"{nameof(ReceiptPage)}" +
                $"?billId={item.BillId}" +
                $"&patientId={Uri.EscapeDataString(PatientId)}" +
                $"&patientName={Uri.EscapeDataString(PatientName)}");
            return;
        }

        await Shell.Current.GoToAsync(
            $"{nameof(BillDetailsPage)}" +
            $"?billId={item.BillId}" +
            $"&patientId={Uri.EscapeDataString(PatientId)}" +
            $"&patientName={Uri.EscapeDataString(PatientName)}");
    }

    public string NextDueDisplay
    {
        get
        {
            var nextDue = Bills
                .Where(x => x.DueDate.HasValue)
                .OrderBy(x => x.DueDate)
                .FirstOrDefault();

            return nextDue?.DueDateDisplay ?? "—";
        }
    }
}