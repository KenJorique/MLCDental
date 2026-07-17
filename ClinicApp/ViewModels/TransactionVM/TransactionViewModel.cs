using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    [QueryProperty(nameof(PatientId), "patientId")]
    [QueryProperty(nameof(PatientName), "patientName")]
    public partial class TransactionViewModel : ObservableObject
    {
        readonly SupabaseDataService _supabaseData;

        public ObservableCollection<SupabaseTreatmentRecord> TreatmentRecords { get; } = new();
        public ObservableCollection<SupabaseTransaction> Transactions { get; } = new();

        [ObservableProperty] private string patientId = string.Empty;
        [ObservableProperty] private string patientName = string.Empty;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private bool isRefreshing;
        [ObservableProperty] private decimal totalBalance;
        [ObservableProperty] private decimal totalPaid;
        [ObservableProperty] private decimal totalDue;
        [ObservableProperty] private bool hasUnpaid;

        // Add treatment record form
        [ObservableProperty] private string selectedService = string.Empty;
        [ObservableProperty] private decimal servicePrice;
        [ObservableProperty] private string visitNotes = string.Empty;
        [ObservableProperty] private bool showAddTreatment;

        // Payment form
        [ObservableProperty] private SupabaseTransaction? selectedTransaction;
        [ObservableProperty] private decimal paymentAmount;
        [ObservableProperty] private bool showPaymentForm;

        public TransactionViewModel(SupabaseDataService supabaseData)
        {
            _supabaseData = supabaseData;
        }

        partial void OnPatientIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
                MainThread.BeginInvokeOnMainThread(async () =>
                    await LoadDataAsync());
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var treatmentTask = _supabaseData
                    .GetTreatmentRecordsAsync(PatientId);
                var transactionTask = _supabaseData
                    .GetTransactionsAsync(PatientId);

                await Task.WhenAll(treatmentTask, transactionTask);

                TreatmentRecords.Clear();
                foreach (var r in treatmentTask.Result)
                    TreatmentRecords.Add(r);

                Transactions.Clear();
                foreach (var t in transactionTask.Result)
                    Transactions.Add(t);

                // Calculate summary
                TotalDue = Transactions.Sum(t => t.TotalAmount);
                TotalPaid = Transactions.Sum(t => t.AmountPaid);
                TotalBalance = Transactions.Sum(t => t.Balance);
                HasUnpaid = TotalBalance > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TransactionVM] LoadData: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        void OpenAddTreatment() => ShowAddTreatment = true;

        [RelayCommand]
        void CloseAddTreatment()
        {
            ShowAddTreatment = false;
            SelectedService = string.Empty;
            ServicePrice = 0;
            VisitNotes = string.Empty;
        }

        [RelayCommand]
        async Task SaveTreatmentRecord()
        {
            if (string.IsNullOrWhiteSpace(SelectedService))
            {
                await Shell.Current.DisplayAlert("Required",
                    "Please select a service.", "OK");
                return;
            }

            if (ServicePrice <= 0)
            {
                await Shell.Current.DisplayAlert("Required",
                    "Please enter the service price.", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                // 1. Save treatment record
                var record = new SupabaseTreatmentRecord
                {
                    PatientId = PatientId,
                    ServiceName = SelectedService,
                    ServicePrice = ServicePrice,
                    VisitDate = DateTime.UtcNow,
                    Notes = VisitNotes.Trim(),
                    RecordedBy = "Clinic Staff"
                };
                var saved = await _supabaseData.AddTreatmentRecordAsync(record);

                // 2. Create transaction linked to treatment record
                var transaction = new SupabaseTransaction
                {
                    PatientId = PatientId,
                    TreatmentRecordId = saved?.Id,
                    ServiceName = SelectedService,
                    TotalAmount = ServicePrice,
                    AmountPaid = 0,
                    PaymentStatus = "unpaid",
                    RecordedBy = "Clinic Staff"
                };
                await _supabaseData.AddTransactionAsync(transaction);

                CloseAddTreatmentCommand.Execute(null);
                await LoadDataAsync();

                await Shell.Current.DisplayAlert("Saved",
                    $"Treatment record added. Balance: ₱{ServicePrice:N2}", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        void OpenPayment(SupabaseTransaction transaction)
        {
            SelectedTransaction = transaction;
            PaymentAmount = transaction.Balance;
            ShowPaymentForm = true;
        }

        [RelayCommand]
        void ClosePayment()
        {
            ShowPaymentForm = false;
            SelectedTransaction = null;
            PaymentAmount = 0;
        }

        [RelayCommand]
        async Task RecordPayment()
        {
            if (SelectedTransaction == null) return;

            if (PaymentAmount <= 0)
            {
                await Shell.Current.DisplayAlert("Invalid",
                    "Payment amount must be greater than zero.", "OK");
                return;
            }

            if (PaymentAmount > SelectedTransaction.Balance)
            {
                await Shell.Current.DisplayAlert("Invalid",
                    $"Payment cannot exceed balance of " +
                    $"₱{SelectedTransaction.Balance:N2}.", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                var success = await _supabaseData.RecordPaymentAsync(
                    SelectedTransaction.Id, PaymentAmount);

                if (success)
                {
                    var remaining = SelectedTransaction.Balance - PaymentAmount;
                    ClosePaymentCommand.Execute(null);
                    await LoadDataAsync();

                    var msg = remaining <= 0
                        ? "Payment complete. Balance fully settled."
                        : $"Payment recorded. Remaining balance: ₱{remaining:N2}";

                    await Shell.Current.DisplayAlert("Payment Recorded", msg, "OK");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error",
                        "Failed to record payment.", "OK");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        async Task Refresh()
        {
            IsRefreshing = true;
            await LoadDataAsync();
        }
    }
}