using ClinicApp.Helpers;
using ClinicApp.Services;
using ClinicApp.Behaviors;
using ClinicApp.Views;
using ClinicApp.Views.TransactionRelated;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ClinicApp.Models.TransactionModels;
using ClinicApp.Models.SupabaseModels;
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

        // Separate from IsBusy: this drives only the small spinner in Available Services during LoadServicesAsync(), not the full-screen "Saving..." overlay.
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanCreateBill))]
        bool isLoadingServices;

        // Payment overlay
        [ObservableProperty] bool hasInstallmentService;
        [ObservableProperty] bool isInstallment;
        [ObservableProperty] string supabaseEntryId = string.Empty;
        [ObservableProperty] string serviceSearch = string.Empty;
        [ObservableProperty] int scrollTrigger;

        // Bottom summary panel is plain page content (see CreateBillPage.xaml); this only drives the tap-to-expand services list.
        [ObservableProperty] bool isServicesExpanded;

        public bool HasSelectedServices => SelectedServices.Count > 0;
        public string ServicesCountLabel => $"Added services ({SelectedServices.Count})";
        public string ToggleLabelText => IsServicesExpanded ? "Hide" : "Show";
        public string ToggleIconGlyph => IsServicesExpanded ? "\ue5ce" : "\ue5cf"; // expand_less / expand_more

        // Refreshes the toggle label/icon whenever the expanded state flips.
        partial void OnIsServicesExpandedChanged(bool value)
        {
            OnPropertyChanged(nameof(ToggleLabelText));
            OnPropertyChanged(nameof(ToggleIconGlyph));
        }

        // Flips the services list between expanded and collapsed.
        [RelayCommand]
        void ToggleServicesExpanded() => IsServicesExpanded = !IsServicesExpanded;

        // Proceed is only enabled once at least one service is selected and no operation is in flight.
        public bool CanCreateBill =>
            SelectedServices.Count > 0 && !IsBusy && !IsLoadingServices;

        // Injects the shared data service and draft service.
        public CreateBillViewModel(
       SupabaseDataService supabase,
       BillDraftService draft)
        {
            _supabase = supabase;
            _draft = draft;
        }

        [ObservableProperty] ObservableCollection<AvailableServiceItem> filteredServices = new();

        CancellationTokenSource? _searchDebounce;

        // Debounced: filtering waits 200ms after the last keystroke, so fast typing doesn't rebuild on every character.
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

        // Swaps in a whole new collection instead of Clear()/Add()-ing, since BindableLayout isn't virtualized and would re-render on every Add().
        private void FilterServices(string query)
        {
            var results = string.IsNullOrWhiteSpace(query)
                ? AvailableServices
                : AvailableServices.Where(s =>
                    s.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

            FilteredServices = new ObservableCollection<AvailableServiceItem>(results);
        }

        // Loads available services once, then reuses the cache; also (re)builds FilteredServices.
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

        // Loads services once the patient ID arrives via navigation.
        partial void OnPatientIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
                MainThread.BeginInvokeOnMainThread(async () =>
                    await LoadServicesAsync());
        }


        // Adds a service to the bill at quantity 1 (no-op if it's already on the bill).
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

            // Auto-expand on the first service, so it's immediately visible without an extra tap.
            if (SelectedServices.Count == 1)
                IsServicesExpanded = true;
        }

        // Removes a service from the bill outright (no confirmation — used by the list above CreateBillPage's Proceed button).
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

        // Single entry point for the +/- toggle button: always takes AvailableServiceItem so Command/CommandParameter never need to switch types via DataTrigger.
        [RelayCommand]
        void ToggleService(AvailableServiceItem serviceItem)
        {
            if (serviceItem == null) return;

            if (serviceItem.IsAddDisabled)
                RemoveServiceById(serviceItem.Id);
            else
                AddService(serviceItem);
        }

        // Used by the Available Services toggle button, which only has an AvailableServiceItem (service Id), so it removes by matching ServiceId.
        [RelayCommand]
        void RemoveServiceById(string serviceId)
        {
            if (string.IsNullOrEmpty(serviceId)) return;
            var item = SelectedServices.FirstOrDefault(s => s.ServiceId == serviceId);
            if (item == null) return;
            RemoveService(item);
        }

        // Marks each available service's Add button disabled if it's already on the bill.
        private void RefreshAddButtonStates()
        {
            var addedIds = SelectedServices.Select(s => s.ServiceId).ToHashSet();

            foreach (var item in AvailableServices)
                item.IsAddDisabled = addedIds.Contains(item.Id);
        }



        // Increases a selected service's quantity by one and refreshes its subtotal.
        [RelayCommand]
        void IncreaseQty(ServiceLineItem item)
        {
            if (item == null) return;
            item.Quantity++;
            item.RefreshSubtotal();
            RecalculateTotal();
        }

        // Decreases a selected service's quantity by one, never below 1.
        [RelayCommand]
        void DecreaseQty(ServiceLineItem item)
        {
            if (item == null || item.Quantity <= 1) return;
            item.Quantity--;
            item.RefreshSubtotal();
            RecalculateTotal();
        }

        // Recomputes the bill's total from all selected services' subtotals.
        void RecalculateTotal()
        {
            TotalAmount = SelectedServices.Sum(s => s.Subtotal);
        }

        // Validates tooth numbers, then creates the bill and its draft-linked records.
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

                var popup = new ConfirmationPopup(
                    "Missing Tooth Numbers",
                    $"No teeth entered for:\n{names}\n\nProceed without tooth numbers?",
                    "Proceed", PopupAction.Positive);
                var result = await Shell.Current.CurrentPage.ShowPopupAsync(popup);
                bool proceed = result is bool b && b;

                if (!proceed)
                    return;
            }

            // Separate from the missing-entirely check above: these have something typed, but it's invalid (e.g. "100", "-1") or doesn't match the quantity.
            var invalidTeeth = SelectedServices
                .Where(s => s.ShowTeethInput &&
                            !string.IsNullOrWhiteSpace(s.ToothNumbers) &&
                            s.HasToothValidationMessage)
                .ToList();

            if (invalidTeeth.Any())
            {
                var names = string.Join(", ",
                    invalidTeeth.Select(s => s.ServiceName));

                var popup = new ConfirmationPopup(
                    "Check Tooth Numbers",
                    $"Tooth numbers look incomplete or invalid for:\n{names}\n\nProceed anyway?",
                    "Proceed", PopupAction.Positive);
                var result = await Shell.Current.CurrentPage.ShowPopupAsync(popup);
                bool proceed = result is bool b && b;

                if (!proceed)
                    return;
            }

            IsBusy = true;
            HasError = false;

            try
            {
                Draft.PatientId = PatientId;

                // Backfill a blank PatientId here so both follow-up creation and billing get a real ID instead of failing on an empty-string UUID.
                if (string.IsNullOrWhiteSpace(Draft.PatientId))
                {
                    var resolved = !string.IsNullOrWhiteSpace(Phone)
                        ? await _supabase.GetPatientByPhoneAsync(Phone)
                        : null;
                    resolved ??= !string.IsNullOrWhiteSpace(PatientName)
                        ? await _supabase.GetPatientByNameAsync(PatientName)
                        : null;

                    if (resolved != null)
                        Draft.PatientId = resolved.Id;
                    else
                        System.Diagnostics.Debug.WriteLine(
                            $"[CreateBill] WARNING: could not resolve a patient ID for '{PatientName}' — bill/follow-up will be unlinked.");
                }

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

                await Shell.Current.GoToAsync(nameof(BillSummaryPage));
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

        // Navigates back without saving anything.
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

        // Each installment-eligible service carries its own plan: 50% down today, remaining 50% split evenly over 1-4 months.
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

        // What this service adds to "due today": full price, or just the 50% downpayment if it's on a plan.
        public decimal AmountDueToday =>
            IsInstallmentEligible && IsInstallmentSelected
                ? DownpaymentAmount
                : Subtotal;

        public string DownpaymentDisplay => $"₱{DownpaymentAmount:N2}";
        public string MonthlyPaymentDisplay => $"₱{MonthlyPaymentAmount:N2}";
        public string AmountDueTodayDisplay => $"₱{AmountDueToday:N2}";
        public string RemainingAfterDownpaymentDisplay => $"₱{RemainingAfterDownpayment:N2}";

        // Preview amounts for each of the 4 grid buttons: what the monthly payment would be for that option, independent of which is selected.
        public string MonthlyFor(int months) =>
            months > 0 ? $"₱{Math.Round(RemainingAfterDownpayment / months, 2):N2}" : "₱0.00";

        public string MonthlyFor1Display => MonthlyFor(1);
        public string MonthlyFor2Display => MonthlyFor(2);
        public string MonthlyFor3Display => MonthlyFor(3);
        public string MonthlyFor4Display => MonthlyFor(4);

        // Picks which installment-length button (1-4 months) is currently selected.
        [RelayCommand]
        void SelectMonths(int months) => SelectedInstallmentMonths = months;

        public string InstallmentPlanSummary =>
            IsInstallmentSelected
                ? $"{DownpaymentDisplay} down, then {MonthlyPaymentDisplay} x {SelectedInstallmentMonths} mo."
                : string.Empty;

        // Refreshes installment displays when the plan is turned on/off.
        partial void OnIsInstallmentSelectedChanged(bool value) =>
            RaiseInstallmentDisplaysChanged();

        // Refreshes installment displays when the chosen month count changes.
        partial void OnSelectedInstallmentMonthsChanged(int value) =>
            RaiseInstallmentDisplaysChanged();

        // Notifies all computed installment properties/displays at once.
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

        // Raw tokens as typed, before the 1-32 filter above — catches entries like "100" or "-1" that ParsedTeethNumbers silently drops.
        List<string> RawToothTokens =>
            ToothNumbers
                .Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToList();

        public bool HasInvalidToothNumbers =>
            ShowTeethInput &&
            RawToothTokens.Any(t => !int.TryParse(t, out var n) || n < 1 || n > 32);

        // True once the count of valid, distinct tooth numbers matches Quantity (one tooth number expected per unit).
        public bool ToothCountMatchesQuantity =>
            !ShowTeethInput || ParsedTeethNumbers.Count == Quantity;

        // Single message surfaced under the tooth-number field; invalid-number check takes priority since fixing it usually fixes the count too.
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

        // Recomputes this line's subtotal from unit price x quantity, and refreshes installment displays.
        public void RefreshSubtotal()
        {
            Subtotal = UnitPrice * Quantity;
            RaiseInstallmentDisplaysChanged();
        }

        // Refreshes subtotal and tooth validation whenever quantity changes.
        partial void OnQuantityChanged(int value)
        {
            RefreshSubtotal();
            RaiseToothValidationChanged();
        }

        // Refreshes the teeth display and validation whenever the typed tooth numbers change.
        partial void OnToothNumbersChanged(string value)
        {
            OnPropertyChanged(nameof(TeethDisplay));
            RaiseToothValidationChanged();
        }

        // Notifies all tooth-validation-related computed properties at once.
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

        // Wraps a Supabase service row and flags whether it needs tooth-number input.
        public AvailableServiceItem(SupabaseService service)
        {
            Service = service;
            RequiresTeeth = ToothAwareServices.NeedsTeethInput(service.Name);
        }
    }

}