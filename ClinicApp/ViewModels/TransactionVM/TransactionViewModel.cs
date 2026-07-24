using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.TransactionVM
{
    [QueryProperty(nameof(PatientId), "patientId")]
    [QueryProperty(nameof(PatientName), "patientName")]
    public partial class TransactionViewModel : ObservableObject
    {
        readonly SupabaseDataService _supabase;
        readonly DatabaseService _database;

        public ObservableCollection<SupabaseBill> Bills { get; } = new();
        public ObservableCollection<SupabaseBill> UnpaidBills { get; } = new();

        [ObservableProperty] string patientId = string.Empty;
        [ObservableProperty] string patientName = string.Empty;
        [ObservableProperty] bool isBusy;
        [ObservableProperty] bool isRefreshing;
        [ObservableProperty] decimal totalBilled;
        [ObservableProperty] decimal totalPaid;
        [ObservableProperty] decimal totalBalance;
        [ObservableProperty] bool hasBalance;

        public TransactionViewModel(SupabaseDataService supabase, DatabaseService database)
        {
            _supabase = supabase;
            _database = database;
        }

        partial void OnPatientIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
                MainThread.BeginInvokeOnMainThread(async () =>
                    await LoadBillsAsync());
        }

        [RelayCommand]
        public async Task LoadBillsAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var all = await _supabase.GetBillsForPatientAsync(PatientId);

                Bills.Clear();
                UnpaidBills.Clear();

                foreach (var b in all)
                {
                    Bills.Add(b);
                    if (b.Status != "paid")
                        UnpaidBills.Add(b);
                }

                TotalBilled = Bills.Sum(b => b.TotalAmount);
                TotalPaid = Bills.Sum(b => b.AmountPaid);
                TotalBalance = Bills.Sum(b => b.Balance);
                HasBalance = TotalBalance > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TransactionVM] {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        public async Task OnAppearing()
        {
            await LoadBillsAsync();
        }

        [RelayCommand]
        async Task Refresh()
        {
            IsRefreshing = true;
            await LoadBillsAsync();
        }

        [RelayCommand]
        async Task ViewReceipt(SupabaseBill bill)
        {
            if (bill == null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(ReceiptPage)}" +
                $"?billId={bill.Id}" +
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
    }
}