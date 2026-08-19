using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.TransactionVM;

[QueryProperty(nameof(PatientId), "patientId")]
[QueryProperty(nameof(PatientName), "patientName")]
public partial class TransactionViewModel : ObservableObject
{
    readonly SupabaseDataService _supabase;
    readonly DatabaseService _database;

    public ObservableCollection<LedgerItem> PendingPayments { get; }
    = new();

    public ObservableCollection<SupabaseBill> Bills { get; } = new();
    public ObservableCollection<SupabaseBill> UnpaidBills { get; } = new();

    // One card per bill — replaces the old single unified ledger list.
    // Each card carries its own payment history, fetched per-bill in
    // LoadBillsAsync below.
    public ObservableCollection<BillCardItem> BillCards { get; } = new();

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

    // Pill colors for the patient-summary status badge — back to the
    // rounded-badge design for this card specifically. The left-accent-
    // strip treatment stays only on the individual bill cards below.
    public Color PaymentStatusColor => PaymentStatus switch
    {
        "Paid" => Color.FromArgb("#2E7D32"),
        "Partially Paid" => Color.FromArgb("#E65100"),
        "Unpaid" => Color.FromArgb("#C62828"),
        _ => Color.FromArgb("#888888")
    };

    public Color PaymentStatusBgColor => PaymentStatus switch
    {
        "Paid" => Color.FromArgb("#E8F5E9"),
        "Partially Paid" => Color.FromArgb("#FFF3E0"),
        "Unpaid" => Color.FromArgb("#FCEAEA"),
        _ => Color.FromArgb("#F5F5F5")
    };

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

            Bills.Clear();
            foreach (var bill in all)
                Bills.Add(bill);

            // Build one card per bill, newest bill first. Each card
            // fetches and owns its own payment history so a multi-bill
            // patient's payments never get mixed up across bills.
            BillCards.Clear();
            foreach (var bill in all.OrderByDescending(b => b.VisitDate))
            {
                var payments = await _supabase.GetPaymentsForBillAsync(bill.Id);

                // Oldest-first display: first payment made appears at the
                // top of the table, most recent at the bottom.
                var chronological = payments.OrderBy(p => p.PaymentDate).ToList();
                var rows = new List<PaymentRowItem>();
                var runningBalance = bill.TotalAmount;

                foreach (var p in chronological)
                {
                    runningBalance -= p.Amount;
                    rows.Add(new PaymentRowItem
                    {
                        PaymentId = p.Id,
                        BillId = bill.Id,
                        Date = p.PaymentDate,
                        Amount = p.Amount,
                        RemainingBalance = runningBalance
                    });
                }

                BillCards.Add(new BillCardItem(bill, rows));
            }

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
            OnPropertyChanged(nameof(PaymentStatusColor));
            OnPropertyChanged(nameof(PaymentStatusBgColor));
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

    // Add Payment button inside an individual bill card — scoped to
    // that specific bill so it's unambiguous which bill the payment
    // applies to when a patient has several (spec section 4).
    [RelayCommand]
    private async Task AddPaymentForBill(SupabaseBill bill)
    {
        if (bill == null)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(AdditionalPaymentPage)}" +
            $"?billId={bill.Id}" +
            $"&patientId={Uri.EscapeDataString(PatientId)}" +
            $"&patientName={Uri.EscapeDataString(PatientName)}");
    }

    // Tapping a payment row opens Bill Details (not a standalone
    // receipt) — see the note at the top of this response for why.
    [RelayCommand]
    private async Task OpenPayment(PaymentRowItem item)
    {
        if (item == null)
            return;

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
