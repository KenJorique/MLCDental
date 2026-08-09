using ClinicApp.Helpers;
using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Behaviors;
using ClinicApp.Views;
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

        // Set by CreateBillPage right after it creates + shows the sheet, so
        // CreateBill()/Cancel() below can dismiss it before navigating away.
        public CreateBillSummarySheet? Sheet { get; set; }

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
        [ObservableProperty] int scrollTrigger;

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
            ScrollTrigger++;
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

        // Single entry point for the +/- toggle button on CreateBillPage. Always takes the
        // AvailableServiceItem (never a plain string), so the Button's Command/CommandParameter
        // never need to switch types via DataTrigger — only Text/BackgroundColor do. Switching
        // Command *type* via DataTrigger was the cause of the ArgumentException: MAUI doesn't
        // apply Command and CommandParameter as one atomic unit, so there's a moment where the
        // old parameter is checked against the new command's expected type.
        [RelayCommand]
        void ToggleService(AvailableServiceItem serviceItem)
        {
            if (serviceItem == null) return;

            if (serviceItem.IsAddDisabled)
                RemoveServiceById(serviceItem.Id);
            else
                AddService(serviceItem);
        }

        // Used by the +/- toggle button on the Available Services list (CreateBillPage) —
        // that button only has the AvailableServiceItem (with its service Id), not the
        // actual SelectedServices ServiceLineItem, so it removes by matching ServiceId.
        [RelayCommand]
        void RemoveServiceById(string serviceId)
        {
            if (string.IsNullOrEmpty(serviceId)) return;
            var item = SelectedServices.FirstOrDefault(s => s.ServiceId == serviceId);
            if (item == null) return;
            RemoveService(item);
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

                // Fully close the sheet before navigating — it shouldn't stay open
                // underneath the next page.
                if (Sheet != null)
                {
                    await Sheet.DismissAsync();
                    Sheet = null;
                }

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
        async Task Cancel()
        {
            if (Sheet != null)
            {
                await Sheet.DismissAsync();
                Sheet = null;
            }
            await Shell.Current.GoToAsync("..");
        }
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

        // ── Per-service installment plan ──
        // Each installment-eligible service carries its own plan now,
        // instead of one plan for the whole bill. Rule: 50% down today,
        // remaining 50% split evenly over 1–4 months.
        [ObservableProperty] bool isInstallmentSelected;
        [ObservableProperty] int selectedInstallmentMonths = 1;

        public decimal DownpaymentAmount =>
            IsInstallmentEligible && IsInstallmentSelected
                ? Math.Round(Subtotal * 0.5m, 2)
                : 0m;

        public decimal RemainingAfterDownpayment =>
            Subtotal - DownpaymentAmount;

        public decimal MonthlyPaymentAmount =>
            IsInstallmentEligible && IsInstallmentSelected && SelectedInstallmentMonths > 0
                ? Math.Round(RemainingAfterDownpayment / SelectedInstallmentMonths, 2)
                : 0m;

        // What this service actually adds to "due today" — full price if
        // not on a plan, just the 50% downpayment if it is.
        public decimal AmountDueToday =>
            IsInstallmentEligible && IsInstallmentSelected
                ? DownpaymentAmount
                : Subtotal;

        public string DownpaymentDisplay => $"₱{DownpaymentAmount:N2}";
        public string MonthlyPaymentDisplay => $"₱{MonthlyPaymentAmount:N2}";
        public string AmountDueTodayDisplay => $"₱{AmountDueToday:N2}";
        public string RemainingAfterDownpaymentDisplay => $"₱{RemainingAfterDownpayment:N2}";

        // Preview amounts for each of the 4 grid buttons — these show what
        // the monthly payment WOULD be for that option, independent of
        // which one is currently selected (so all 4 can be shown at once).
        public string MonthlyFor(int months) =>
            months > 0 ? $"₱{Math.Round(RemainingAfterDownpayment / months, 2):N2}" : "₱0.00";

        public string MonthlyFor1Display => MonthlyFor(1);
        public string MonthlyFor2Display => MonthlyFor(2);
        public string MonthlyFor3Display => MonthlyFor(3);
        public string MonthlyFor4Display => MonthlyFor(4);

        [RelayCommand]
        void SelectMonths(int months) => SelectedInstallmentMonths = months;

        public string InstallmentPlanSummary =>
            IsInstallmentSelected
                ? $"{DownpaymentDisplay} down, then {MonthlyPaymentDisplay} x {SelectedInstallmentMonths} mo."
                : string.Empty;

        partial void OnIsInstallmentSelectedChanged(bool value) =>
            RaiseInstallmentDisplaysChanged();

        partial void OnSelectedInstallmentMonthsChanged(int value) =>
            RaiseInstallmentDisplaysChanged();

        void RaiseInstallmentDisplaysChanged()
        {
            OnPropertyChanged(nameof(DownpaymentAmount));
            OnPropertyChanged(nameof(RemainingAfterDownpayment));
            OnPropertyChanged(nameof(MonthlyPaymentAmount));
            OnPropertyChanged(nameof(AmountDueToday));
            OnPropertyChanged(nameof(DownpaymentDisplay));
            OnPropertyChanged(nameof(MonthlyPaymentDisplay));
            OnPropertyChanged(nameof(AmountDueTodayDisplay));
            OnPropertyChanged(nameof(RemainingAfterDownpaymentDisplay));
            OnPropertyChanged(nameof(MonthlyFor1Display));
            OnPropertyChanged(nameof(MonthlyFor2Display));
            OnPropertyChanged(nameof(MonthlyFor3Display));
            OnPropertyChanged(nameof(MonthlyFor4Display));
            OnPropertyChanged(nameof(InstallmentPlanSummary));
        }

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
            RaiseInstallmentDisplaysChanged();
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