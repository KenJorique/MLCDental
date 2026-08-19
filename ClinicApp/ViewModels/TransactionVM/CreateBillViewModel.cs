using ClinicApp.Helpers;
using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Behaviors;
using ClinicApp.Views;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

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
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanCreateBill))]
        bool isBusy;
        [ObservableProperty] bool hasError;
        [ObservableProperty] string supabaseBookingId = string.Empty;
        [ObservableProperty] string errorMessage = string.Empty;
        [ObservableProperty] decimal totalAmount;
        [ObservableProperty] string notes = string.Empty;
        [ObservableProperty] string createdBillId = string.Empty;
        [ObservableProperty] string createdBillNumber = string.Empty;
        [ObservableProperty] string phone = string.Empty;

        // Separate from IsBusy on purpose: IsBusy drives the full-screen "Saving..."
        // overlay during CreateBill(), while this drives only the small spinner in the
        // Available Services list during LoadServicesAsync(). They used to share IsBusy,
        // which meant clicking Proceed lit up both indicators at once.
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanCreateBill))]
        bool isLoadingServices;

        // Payment overlay
        [ObservableProperty] bool hasInstallmentService;
        [ObservableProperty] bool isInstallment;
        [ObservableProperty] string supabaseEntryId = string.Empty;
        [ObservableProperty] string serviceSearch = string.Empty;
        [ObservableProperty] int scrollTrigger;

        // Bottom summary panel — plain page content (see CreateBillPage.xaml), not a
        // separate modal. Drives the tap-to-expand services list; Total/Proceed render
        // unconditionally regardless of this state.
        [ObservableProperty] bool isServicesExpanded;

        public bool HasSelectedServices => SelectedServices.Count > 0;
        public string ServicesCountLabel => $"Added services ({SelectedServices.Count})";
        public string ToggleLabelText => IsServicesExpanded ? "Hide" : "Show";
        public string ToggleIconGlyph => IsServicesExpanded ? "\ue5ce" : "\ue5cf"; // expand_less / expand_more

        partial void OnIsServicesExpandedChanged(bool value)
        {
            OnPropertyChanged(nameof(ToggleLabelText));
            OnPropertyChanged(nameof(ToggleIconGlyph));
        }

        [RelayCommand]
        void ToggleServicesExpanded() => IsServicesExpanded = !IsServicesExpanded;

        public bool CanCreateBill =>
            SelectedServices.Count > 0 && !IsBusy && !IsLoadingServices;

        public CreateBillViewModel(
       SupabaseDataService supabase,
       BillDraftService draft)
        {
            _supabase = supabase;
            _draft = draft;
        }

        [ObservableProperty] ObservableCollection<AvailableServiceItem> filteredServices = new();

        CancellationTokenSource? _searchDebounce;

        // Debounced: filtering doesn't run until 200ms after the last keystroke, so fast
        // typing doesn't trigger a rebuild on every single character.
        partial void OnServiceSearchChanged(string value)
        {
            _searchDebounce?.Cancel();
            _searchDebounce = new CancellationTokenSource();
            var token = _searchDebounce.Token;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Task.Delay(200, token);
                    if (!token.IsCancellationRequested)
                        FilterServices(value);
                }
                catch (TaskCanceledException) { }
            });
        }

        // Builds and swaps in a whole new collection rather than Clear()-ing and
        // Add()-ing one item at a time. BindableLayout isn't virtualized, so each
        // individual Add() previously forced its own full re-render — swapping the
        // whole ItemsSource reference is a single update instead of many.
        private void FilterServices(string query)
        {
            var results = string.IsNullOrWhiteSpace(query)
                ? AvailableServices
                : AvailableServices.Where(s =>
                    s.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

            FilteredServices = new ObservableCollection<AvailableServiceItem>(results);
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

            IsLoadingServices = true;
            HasError = false;
            try
            {
                var services = await _supabase.GetServicesAsync();
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AvailableServices.Clear();
                    foreach (var s in services)
                    {
                        var item = new AvailableServiceItem(s);
                        AvailableServices.Add(item);
                    }
                    FilteredServices = new ObservableCollection<AvailableServiceItem>(AvailableServices);
                    RefreshAddButtonStates();
                });
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load services: {ex.Message}";
            }
            finally { IsLoadingServices = false; }
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
            OnPropertyChanged(nameof(HasSelectedServices));
            OnPropertyChanged(nameof(ServicesCountLabel));
            RefreshAddButtonStates();

            // Auto-expand on the very first service, so it's immediately visible instead
            // of requiring an extra tap right after adding something for the first time.
            if (SelectedServices.Count == 1)
                IsServicesExpanded = true;
        }

        [RelayCommand]
        void RemoveService(ServiceLineItem item)
        {
            if (item == null) return;
            SelectedServices.Remove(item);
            HasInstallmentService = SelectedServices.Any(s => s.IsInstallmentEligible);
            RecalculateTotal();
            OnPropertyChanged(nameof(CanCreateBill));
            OnPropertyChanged(nameof(HasSelectedServices));
            OnPropertyChanged(nameof(ServicesCountLabel));
            RefreshAddButtonStates();

            if (SelectedServices.Count == 0)
                IsServicesExpanded = false;
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

            // Separate from the missing-entirely check above: these have *something*
            // typed, but it's either not a real tooth number (e.g. "100", "-1") or
            // doesn't match how many were expected for the quantity selected.
            var invalidTeeth = SelectedServices
                .Where(s => s.ShowTeethInput &&
                            !string.IsNullOrWhiteSpace(s.ToothNumbers) &&
                            s.HasToothValidationMessage)
                .ToList();

            if (invalidTeeth.Any())
            {
                var names = string.Join(", ",
                    invalidTeeth.Select(s => s.ServiceName));

                bool proceed = await Shell.Current.DisplayAlert(
                    "Check Tooth Numbers",
                    $"Tooth numbers look incomplete or invalid for:\n{names}\n\nProceed anyway?",
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
        async Task Cancel()
        {
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

        // Raw tokens as typed, before the 1-32 filter above — used to detect entries
        // like "100" or "-1" that ParsedTeethNumbers silently drops rather than flags.
        List<string> RawToothTokens =>
            ToothNumbers
                .Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToList();

        public bool HasInvalidToothNumbers =>
            ShowTeethInput &&
            RawToothTokens.Any(t => !int.TryParse(t, out var n) || n < 1 || n > 32);

        // True once the count of valid, distinct tooth numbers matches Quantity — the
        // expected case is one tooth number per unit (e.g. Quantity 2 needs 2 numbers).
        public bool ToothCountMatchesQuantity =>
            !ShowTeethInput || ParsedTeethNumbers.Count == Quantity;

        // Single message surfaced under the tooth-number field. Invalid-number check
        // takes priority over the count check, since fixing invalid entries usually
        // fixes the count too.
        public string ToothValidationMessage
        {
            get
            {
                if (!ShowTeethInput || string.IsNullOrWhiteSpace(ToothNumbers))
                    return string.Empty;

                if (HasInvalidToothNumbers)
                    return "Enter valid tooth numbers only (1–32).";

                if (!ToothCountMatchesQuantity)
                    return ParsedTeethNumbers.Count < Quantity
                        ? $"Enter {Quantity} tooth number(s) — {ParsedTeethNumbers.Count} entered so far."
                        : $"Too many tooth numbers — enter exactly {Quantity}.";

                return string.Empty;
            }
        }

        public bool HasToothValidationMessage => !string.IsNullOrEmpty(ToothValidationMessage);

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

        partial void OnQuantityChanged(int value)
        {
            RefreshSubtotal();
            RaiseToothValidationChanged();
        }

        partial void OnToothNumbersChanged(string value)
        {
            OnPropertyChanged(nameof(TeethDisplay));
            RaiseToothValidationChanged();
        }

        void RaiseToothValidationChanged()
        {
            OnPropertyChanged(nameof(HasInvalidToothNumbers));
            OnPropertyChanged(nameof(ToothCountMatchesQuantity));
            OnPropertyChanged(nameof(ToothValidationMessage));
            OnPropertyChanged(nameof(HasToothValidationMessage));
        }
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