using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using ClinicApp.ViewModels.PatientsRelatedVM;
using ClinicApp.Views;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
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

        // Passed from Payment page via navigation params — transient, no DB column needed.
        [ObservableProperty] string amountReceivedRaw = string.Empty;
        [ObservableProperty] string changeRaw = string.Empty;

        public decimal AmountReceived =>
            decimal.TryParse(AmountReceivedRaw, out var v) ? v : 0;

        public decimal Change =>
            decimal.TryParse(ChangeRaw, out var v) ? v : 0;

        public string AmountReceivedDisplay => $"₱{AmountReceived:N2}";
        public string ChangeDisplay => $"₱{Change:N2}";
        public bool HasChange => Change > 0;

        // Raises change notifications for the computed properties, since QueryProperty alone doesn't.
        partial void OnAmountReceivedRawChanged(string value)
        {
            OnPropertyChanged(nameof(AmountReceived));
            OnPropertyChanged(nameof(AmountReceivedDisplay));
        }

        // Same reasoning as above, for the Change value.
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

        // What was actually paid THIS visit, not the bill's cumulative total.
        public string LatestPaymentAmountDisplay =>
            Payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault()?.AmountDisplay
                ?? Bill?.PaidDisplay
                ?? "₱0.00";

        public bool HasInstallmentItems =>
            Items.Any(i => i.IsInstallment);

        public IEnumerable<SupabaseBillItem> InstallmentItems =>
            Items.Where(i => i.IsInstallment);

        // Injects the shared data service.
        public ReceiptViewModel(SupabaseDataService supabase)
        {
            _supabase = supabase;
        }

        // Loads the receipt once BillId is set via navigation.
        partial void OnBillIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
                MainThread.BeginInvokeOnMainThread(async () =>
                    await LoadReceiptAsync());
        }

        [ObservableProperty] bool notFound;

        [ObservableProperty] string debugInfo = string.Empty;


        // Loads the bill, its line items, and its payment history.
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
                await Shell.Current.CurrentPage.ShowPopupAsync(new ConfirmationPopup(
                    "Error loading receipt", ex.Message, "OK", showCancelButton: false));
            }
            finally { IsBusy = false; }
        }

        // Cleans up the source booking/entry, then returns to the patient's transaction page.
        [RelayCommand]
        async Task Done()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(AppointmentEntryId))
                    await _supabase.DeleteAppointmentEntryAsync(AppointmentEntryId);

                if (!string.IsNullOrWhiteSpace(SupabaseBookingId))
                    await _supabase.DeleteBookingAsync(SupabaseBookingId);

                // Pops this flow's pages off the Appointment tab's stack before switching tabs, so it isn't still on top next visit.
                await Shell.Current.Navigation.PopToRootAsync(false);

                await Shell.Current.GoToAsync(
                    $"//PatientListPage/{nameof(TransactionPage)}" +
                    $"?patientId={Uri.EscapeDataString(PatientId)}" +
                    $"&patientName={Uri.EscapeDataString(PatientName)}");
            }
            catch (Exception ex)
            {
                await Shell.Current.CurrentPage.ShowPopupAsync(new ConfirmationPopup(
                    "Error", ex.Message, "OK", showCancelButton: false));
            }
        }
    }
}
