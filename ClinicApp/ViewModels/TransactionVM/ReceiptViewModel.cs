using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.TransactionVM
{
    [QueryProperty(nameof(BillId), "billId")]
    [QueryProperty(nameof(PatientName), "patientName")]
    [QueryProperty(nameof(PatientId), "patientId")]
    public partial class ReceiptViewModel : ObservableObject
    {
        readonly SupabaseDataService _supabase;

        [ObservableProperty] string billId = string.Empty;
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
                Items.Clear();
                foreach (var i in items) Items.Add(i);

                var payments = await _supabase.GetPaymentsForBillAsync(BillId);
                Payments.Clear();
                foreach (var p in payments) Payments.Add(p);

                var bills = await _supabase.GetAllBillsAsync();
                Bill = bills.FirstOrDefault(b => b.Id == BillId);

                if (Bill == null)
                {
                    NotFound = true;
                    var idsPreview = string.Join(" | ", bills.Take(3).Select(b => b.Id));
                    DebugInfo = $"Looking for: '{BillId}' (len={BillId?.Length}) | " +
                                $"Found {bills.Count} bills | First IDs: {idsPreview}";
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
            finally { IsBusy = false; }
        }

        [RelayCommand]
        void CloseAddPayment() => ShowAddPayment = false;

        [RelayCommand]
        async Task Done()
        {
            if (!string.IsNullOrEmpty(PatientId))
            {
                // Absolute navigation — resets the stack and always lands on
                // PatientDetailsPage regardless of whether we arrived here via
                // TransactionPage or AppointmentSchedulePage.
                await Shell.Current.GoToAsync(
                    $"//{nameof(Views.PatientsRelated.PatientDetailsPage)}" +
                    $"?patientId={Uri.EscapeDataString(PatientId)}" +
                    $"&patientName={Uri.EscapeDataString(PatientName)}");
            }
            else
            {
                // Fallback if PatientId wasn't passed for some reason
                await Shell.Current.GoToAsync("..");
            }
        }
    }
}