using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.ViewModels.PatientsRelatedVM;
using ClinicApp.Views;
using ClinicApp.Views.PatientsRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.TransactionVM
{
    [QueryProperty(nameof(BillId), "billId")]
    [QueryProperty(nameof(PatientName), "patientName")]
    [QueryProperty(nameof(PatientId), "patientId")]
    [QueryProperty(nameof(AppointmentEntryId), "appointmentEntryId")]
    [QueryProperty(nameof(SupabaseEntryId), "supabaseEntryId")]
    [QueryProperty(nameof(SupabaseBookingId), "supabaseBookingId")]
    public partial class ReceiptViewModel : ObservableObject
    {
        readonly SupabaseDataService _supabase;

        [ObservableProperty] string billId = string.Empty;
        [ObservableProperty] string appointmentEntryId = string.Empty;
        [ObservableProperty] string supabaseEntryId = string.Empty;
        [ObservableProperty] string supabaseBookingId = string.Empty;
        [ObservableProperty] string patientName = string.Empty;
        [ObservableProperty] string patientId = string.Empty;
        [ObservableProperty] bool isBusy;
        [ObservableProperty] SupabaseBill? bill;
        [ObservableProperty] decimal change;

        // Payment entry
        [ObservableProperty] bool showAddPayment;
        [ObservableProperty] decimal additionalPayment;

        public ObservableCollection<SupabaseBillItem> Items { get; } = new();
        public ObservableCollection<SupabasePayment> Payments { get; } = new();

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
                        $"[Receipt] {item.ServiceName} " +
                        $"Qty={item.Quantity} " +
                        $"Subtotal={item.Subtotal}");
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
        void OpenAddPayment()
        {
            AdditionalPayment = Bill?.Balance ?? 0;
            ShowAddPayment = true;
        }

        [RelayCommand]
        async Task ConfirmAdditionalPayment()
        {
            if (AdditionalPayment <= 0 || Bill == null) return;

            IsBusy = true;
            try
            {
                var (success, error) = await _supabase.RecordPaymentAsync(BillId, AdditionalPayment);
                if (!success)
                {
                    await Shell.Current.DisplayAlert("Payment Failed", error ?? "Unknown error", "OK");
                    return;
                }

                ShowAddPayment = false;
                await LoadReceiptAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReceiptVM] Payment: {ex.Message}");
                await Shell.Current.DisplayAlert("Payment Failed", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        void CloseAddPayment() => ShowAddPayment = false;

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