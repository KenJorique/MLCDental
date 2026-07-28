using ClinicApp.Helpers;
using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.TransactionVM
{
    [QueryProperty(nameof(PatientId), "patientId")]
    [QueryProperty(nameof(PatientName), "patientName")]
    [QueryProperty(nameof(AppointmentEntryId), "appointmentEntryId")]
    [QueryProperty(nameof(SupabaseBookingId), "supabaseBookingId")]
    [QueryProperty(nameof(SupabaseEntryId), "supabaseEntryId")]
    public partial class CreateBillViewModel : ObservableObject
    {
        readonly SupabaseDataService _supabase;
            readonly BillDraftService _draft;


        BillDraft Draft = new();
        public ObservableCollection<ServiceLineItem> SelectedServices { get; } = new();
        public ObservableCollection<AvailableServiceItem> AvailableServices { get; } = new();

        [ObservableProperty] string patientId = string.Empty;
        [ObservableProperty] string patientName = string.Empty;
        [ObservableProperty] string appointmentEntryId = string.Empty;
        [ObservableProperty] bool isBusy;
        [ObservableProperty] bool hasError;
        [ObservableProperty] string supabaseBookingId = string.Empty;
        [ObservableProperty] string errorMessage = string.Empty;
        [ObservableProperty] decimal totalAmount;
        [ObservableProperty] string notes = string.Empty;
        [ObservableProperty] string createdBillId = string.Empty;
        [ObservableProperty] string createdBillNumber = string.Empty;
        [ObservableProperty] string phone = string.Empty;

        // Payment overlay
        [ObservableProperty] bool hasInstallmentService;
        [ObservableProperty] bool isInstallment;
        [ObservableProperty] string supabaseEntryId = string.Empty;
        [ObservableProperty] string serviceSearch = string.Empty;
            
        public bool CanCreateBill =>
            SelectedServices.Count > 0 && !IsBusy;

        public CreateBillViewModel(
       SupabaseDataService supabase,
       BillDraftService draft)
        {
            _supabase = supabase;
            _draft = draft;
        }

        public ObservableCollection<AvailableServiceItem> FilteredServices { get; } = new();

        partial void OnServiceSearchChanged(string value)
        {
            FilterServices(value);
        }

        private void FilterServices(string query)
        {
            FilteredServices.Clear();
            var results = string.IsNullOrWhiteSpace(query)
                ? AvailableServices
                : AvailableServices.Where(s =>
                    s.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

            foreach (var s in results)
                FilteredServices.Add(s);
        }

        // Update LoadServicesAsync to also populate FilteredServices:
        public async Task LoadServicesAsync()
        {
            if (AvailableServices.Count > 0)
            {
                FilterServices(ServiceSearch);
                RefreshAddButtonStates();
                return;
            }

            IsBusy = true;
            HasError = false;
            try
            {
                var services = await _supabase.GetServicesAsync();
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AvailableServices.Clear();
                    FilteredServices.Clear();
                    foreach (var s in services)
                    {
                        var item = new AvailableServiceItem(s);
                        AvailableServices.Add(item);
                        FilteredServices.Add(item);
                    }
                    RefreshAddButtonStates();
                });
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load services: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        partial void OnPatientIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
                MainThread.BeginInvokeOnMainThread(async () =>
                    await LoadServicesAsync());
        }


        [RelayCommand]
        void AddService(AvailableServiceItem serviceItem)
        {
            if (serviceItem == null) return;
            var service = serviceItem.Service;

            var existing = SelectedServices.FirstOrDefault(s => s.ServiceId == service.Id);
            if (existing != null)
                return; // already added — use the +/- in the list above to change quantity

            var item = new ServiceLineItem
            {
                ServiceId = service.Id,
                ServiceName = service.Name,
                UnitPrice = service.BasePrice,
                Quantity = 1,
                ShowTeethInput = serviceItem.RequiresTeeth,
                IsInstallmentEligible = ToothAwareServices.IsInstallmentEligible(service.Name)
            };
            item.RefreshSubtotal();
            SelectedServices.Add(item);

            HasInstallmentService = SelectedServices.Any(s => s.IsInstallmentEligible);

            RecalculateTotal();
            OnPropertyChanged(nameof(CanCreateBill));
            RefreshAddButtonStates();
        }

        [RelayCommand]
        void RemoveService(ServiceLineItem item)
        {
            if (item == null) return;
            SelectedServices.Remove(item);
            HasInstallmentService = SelectedServices.Any(s => s.IsInstallmentEligible);
            RecalculateTotal();
            OnPropertyChanged(nameof(CanCreateBill));
            RefreshAddButtonStates();
        }

        private void RefreshAddButtonStates()
        {
            var addedIds = SelectedServices.Select(s => s.ServiceId).ToHashSet();

            foreach (var item in AvailableServices)
                item.IsAddDisabled = addedIds.Contains(item.Id);
        }


         
        [RelayCommand]
        void IncreaseQty(ServiceLineItem item)
        {
            if (item == null) return;
            item.Quantity++;
            item.RefreshSubtotal();
            RecalculateTotal();
        }

        [RelayCommand]
        void DecreaseQty(ServiceLineItem item)
        {
            if (item == null || item.Quantity <= 1) return;
            item.Quantity--;
            item.RefreshSubtotal();
            RecalculateTotal();
        }

        void RecalculateTotal()
        {
            TotalAmount = SelectedServices.Sum(s => s.Subtotal);
        }

        [RelayCommand]
        async Task CreateBill()
        {
            if (!CanCreateBill)
                return;

            var missingTeeth = SelectedServices
                .Where(s => s.ShowTeethInput &&
                            string.IsNullOrWhiteSpace(s.ToothNumbers))
                .ToList();

            if (missingTeeth.Any())
            {
                var names = string.Join(", ",
                    missingTeeth.Select(s => s.ServiceName));

                bool proceed = await Shell.Current.DisplayAlert(
                    "Missing Tooth Numbers",
                    $"No teeth entered for:\n{names}\n\nProceed without tooth numbers?",
                    "Proceed", "Cancel");

                if (!proceed)
                    return;
            }

            IsBusy = true;
            HasError = false;

            try
            {
                Draft.PatientId = PatientId;
                Draft.PatientId = PatientId;
                Draft.Phone = Phone;
                Draft.PatientName = PatientName;
                Draft.IsInstallment = IsInstallment;
                Draft.Notes = Notes;
                Draft.AppointmentEntryId = AppointmentEntryId;
                Draft.SupabaseEntryId = SupabaseEntryId;
                Draft.Subtotal = SelectedServices.Sum(x => x.Subtotal);
                Draft.DiscountPercent = 0m;
                Draft.DiscountAmount = 0m;
                Draft.Total = Draft.Subtotal;
                Draft.SupabaseBookingId = SupabaseBookingId;
                Draft.Services.Clear();

                foreach (var item in SelectedServices)
                    Draft.Services.Add(item);

                BillDraftStore.Current = Draft;

                await Shell.Current.GoToAsync(nameof(ServiceSummaryPage));
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"[CreateBill] {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }







        [RelayCommand]
        async Task Cancel() =>
            await Shell.Current.GoToAsync("..");
    }

    public partial class ServiceLineItem : ObservableObject

    {
        public string ServiceId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }

        [ObservableProperty] int quantity = 1;
        [ObservableProperty] decimal subtotal;
        [ObservableProperty] string toothNumbers = string.Empty;
        [ObservableProperty] bool showTeethInput;
        [ObservableProperty] bool isInstallmentEligible;

        // Parsed tooth list
        public List<int> ParsedTeethNumbers =>
            ToothNumbers
                .Split(new[] { ',', ' ', ';' },
                       StringSplitOptions.RemoveEmptyEntries)
                .Select(t => int.TryParse(t.Trim(), out var n) ? n : -1)
                .Where(n => n >= 1 && n <= 32)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

        public string TeethDisplay =>
            ParsedTeethNumbers.Count == 0
                ? ""
                : $"Teeth: {string.Join(", ", ParsedTeethNumbers)}";

        public string UnitPriceDisplay => $"₱{UnitPrice:N2}";
        public string SubtotalDisplay => $"₱{Subtotal:N2}";

        public void RefreshSubtotal()
        {
            Subtotal = UnitPrice * Quantity;
        }

        partial void OnQuantityChanged(int value) =>
            RefreshSubtotal();

        partial void OnToothNumbersChanged(string value) =>
            OnPropertyChanged(nameof(TeethDisplay));
    }
    public partial class AvailableServiceItem : ObservableObject
    {
        public SupabaseService Service { get; }
        public string Id => Service.Id;
        public string Name => Service.Name;
        public string PriceDisplay => Service.PriceDisplay;
        public bool RequiresTeeth { get; }

        [ObservableProperty] bool isAddDisabled;

        public AvailableServiceItem(SupabaseService service)
        {
            Service = service;
            RequiresTeeth = ToothAwareServices.NeedsTeethInput(service.Name);
        }
    }

}

