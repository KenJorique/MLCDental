using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.ViewModels.PatientsRelatedVM;
using ClinicApp.Views;
using ClinicApp.Views.PatientsRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;

namespace ClinicApp.ViewModels.TransactionVM
{
    [QueryProperty(nameof(BillId), "billId")]
    [QueryProperty(nameof(PatientName), "patientName")]
    [QueryProperty(nameof(PatientId), "patientId")]
    [QueryProperty(nameof(AppointmentEntryId), "appointmentEntryId")]
    [QueryProperty(nameof(SupabaseEntryId), "supabaseEntryId")]
    [QueryProperty(nameof(SupabaseBookingId), "supabaseBookingId")]
    [QueryProperty(nameof(AmountReceivedRaw), "amountReceived")]
    [QueryProperty(nameof(ChangeRaw), "change")]
    public partial class ReceiptViewModel : ObservableObject
    {
        readonly SupabaseDataService _supabase;

        [ObservableProperty] string billId = string.Empty;
        [ObservableProperty] string appointmentEntryId = string.Empty;
        [ObservableProperty] string supabaseEntryId = string.Empty;
        [ObservableProperty] string supabaseBookingId = string.Empty;
        [ObservableProperty] string patientName = string.Empty;
        [ObservableProperty] string patientId = string.Empty;

        // Passed from Payment page via navigation params — transient,
        // specific to this one payment, so no DB column needed for it.
        [ObservableProperty] string amountReceivedRaw = string.Empty;
        [ObservableProperty] string changeRaw = string.Empty;

        public decimal AmountReceived =>
            decimal.TryParse(AmountReceivedRaw, out var v) ? v : 0;

        public decimal Change =>
            decimal.TryParse(ChangeRaw, out var v) ? v : 0;

        public string AmountReceivedDisplay => $"₱{AmountReceived:N2}";
        public string ChangeDisplay => $"₱{Change:N2}";
        public bool HasChange => Change > 0;

        // FIX (bug #3): AmountReceivedRaw/ChangeRaw are set by Shell AFTER
        // the page/BindingContext is already up, via the QueryProperty
        // attributes above. AmountReceivedDisplay/Change/ChangeDisplay/
        // HasChange are computed (get-only) properties, so nothing told the
        // UI they'd changed when the raw query values arrived — the labels
        // rendered once with the default "" ("₱0.00") and never updated,
        // even though the underlying raw values were set correctly. These
        // two partial methods raise the missing notifications.
        partial void OnAmountReceivedRawChanged(string value)
        {
            OnPropertyChanged(nameof(AmountReceived));
            OnPropertyChanged(nameof(AmountReceivedDisplay));
        }

        partial void OnChangeRawChanged(string value)
        {
            OnPropertyChanged(nameof(Change));
            OnPropertyChanged(nameof(ChangeDisplay));
            OnPropertyChanged(nameof(HasChange));
        }

        [ObservableProperty] bool isBusy;
        [ObservableProperty] SupabaseBill? bill;

        public ObservableCollection<SupabaseBillItem> Items { get; } = new();
        public ObservableCollection<SupabasePayment> Payments { get; } = new();

        // The "hero" figure on the receipt — what was actually paid THIS
        // visit, not the bill's cumulative total. Falls back to the bill's
        // total paid if there's somehow no payment record yet.
        public string LatestPaymentAmountDisplay =>
            Payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault()?.AmountDisplay
                ?? Bill?.PaidDisplay
                ?? "₱0.00";

        public bool HasInstallmentItems =>
            Items.Any(i => i.IsInstallment);

        public IEnumerable<SupabaseBillItem> InstallmentItems =>
            Items.Where(i => i.IsInstallment);

        public ReceiptViewModel(SupabaseDataService supabase)
        {
            _supabase = supabase;
        }

        partial void OnBillIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
                MainThread.BeginInvokeOnMainThread(async () =>
                    await LoadReceiptAsync());
        }

        [ObservableProperty] bool notFound;

        [ObservableProperty] string debugInfo = string.Empty;


        public async Task LoadReceiptAsync()
        {
            IsBusy = true;
            NotFound = false;

            try
            {
                var items = await _supabase.GetBillItemsAsync(BillId);


                System.Diagnostics.Debug.WriteLine(
                    $"[Receipt] Loaded {items.Count} items for bill {BillId}");


                foreach (var item in items)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[DIAG-RECEIPT-READ] {item.ServiceName} " +
                        $"Qty={item.Quantity} Subtotal={item.Subtotal} " +
                        $"IsInstallment={item.IsInstallment} Balance={item.Balance} " +
                        $"AmountPaid={item.AmountPaid} " +
                        $"DueDate={(item.DueDate.HasValue ? item.DueDate.Value.ToString("o") : "NULL")}");
                }


                Items.Clear();

                foreach (var i in items)
                {
                    Items.Add(i);
                }

                var payments = await _supabase.GetPaymentsForBillAsync(BillId);
                Payments.Clear();
                foreach (var p in payments) Payments.Add(p);

                Bill = await _supabase.GetBillByIdAsync(BillId);

                OnPropertyChanged(nameof(LatestPaymentAmountDisplay));
                OnPropertyChanged(nameof(HasInstallmentItems));
                OnPropertyChanged(nameof(InstallmentItems));

                if (Bill != null)
                {
                    DebugInfo = $"Bill loaded: {Bill.BillNumberDisplay}";
                }


            }
            catch (Exception ex)
            {
                NotFound = true;
                DebugInfo = $"Exception: {ex.Message}";
                await Shell.Current.DisplayAlert("Error loading receipt", ex.Message, "OK");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        async Task Done()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(AppointmentEntryId))
                    await _supabase.DeleteAppointmentEntryAsync(AppointmentEntryId);

                if (!string.IsNullOrWhiteSpace(SupabaseBookingId))
                    await _supabase.DeleteBookingAsync(SupabaseBookingId);

                // This whole billing flow (CreateBill -> ServiceSummary ->
                // BillSummary -> Payment -> Receipt) was pushed onto the
                // Appointment tab's own navigation stack, since that's
                // where "In Procedure -> Complete" kicked it off. Switching
                // tabs below does NOT clear that stack -- Shell keeps a
                // separate back stack per tab -- so without this, the
                // Appointment tab would still have this ReceiptPage on
                // top the next time it's tapped. Pop it back to its root
                // first so the tab is clean before we leave it.
                await Shell.Current.Navigation.PopToRootAsync(false);

                await Shell.Current.GoToAsync(
                    $"//PatientListPage/{nameof(TransactionPage)}" +
                    $"?patientId={Uri.EscapeDataString(PatientId)}" +
                    $"&patientName={Uri.EscapeDataString(PatientName)}");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }
}
