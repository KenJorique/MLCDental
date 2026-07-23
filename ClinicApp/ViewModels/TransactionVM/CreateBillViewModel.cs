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
    [QueryProperty(nameof(SupabaseEntryId), "supabaseEntryId")]
    public partial class CreateBillViewModel : ObservableObject
    {
        readonly SupabaseDataService _supabase;
        readonly DatabaseService _db;

        public ObservableCollection<ServiceLineItem> SelectedServices { get; } = new();
        public ObservableCollection<SupabaseService> AvailableServices { get; } = new();

        [ObservableProperty] string patientId = string.Empty;
        [ObservableProperty] string patientName = string.Empty;
        [ObservableProperty] string appointmentEntryId = string.Empty;
        [ObservableProperty] bool isBusy;
        [ObservableProperty] bool hasError;
        [ObservableProperty] string errorMessage = string.Empty;
        [ObservableProperty] decimal totalAmount;
        [ObservableProperty] string notes = string.Empty;
        [ObservableProperty] string createdBillId = string.Empty;
        [ObservableProperty] string createdBillNumber = string.Empty;

        // Payment overlay
        [ObservableProperty] bool showPayment;
        [ObservableProperty] decimal paymentAmount;
        [ObservableProperty] bool hasInstallmentService;
        [ObservableProperty] bool isInstallment;
        [ObservableProperty] string supabaseEntryId = string.Empty;
        [ObservableProperty] string serviceSearch = string.Empty;
        public bool CanCreateBill =>
            SelectedServices.Count > 0 && !IsBusy;

        public CreateBillViewModel(
            SupabaseDataService supabase,
            DatabaseService db)
        {
            _supabase = supabase;
            _db = db;
        }

        public ObservableCollection<SupabaseService> FilteredServices { get; } = new();

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
                        AvailableServices.Add(s);
                        FilteredServices.Add(s);
                    }
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
        void AddService(SupabaseService service)
        {
            if (service == null) return;

            var existing = SelectedServices
                .FirstOrDefault(s => s.ServiceId == service.Id);

            if (existing != null)
            {
                existing.Quantity++;
                existing.RefreshSubtotal();
            }
            else
            {
                var item = new ServiceLineItem
                {
                    ServiceId = service.Id,
                    ServiceName = service.Name,
                    UnitPrice = service.BasePrice,
                    Quantity = 1,
                    ShowTeethInput = ToothAwareServices
                                              .NeedsTeethInput(service.Name),
                    IsInstallmentEligible = ToothAwareServices
                                              .IsInstallmentEligible(service.Name)
                };
                item.RefreshSubtotal();
                SelectedServices.Add(item);
            }

            // Check if any installment-eligible service was added
            HasInstallmentService = SelectedServices
                .Any(s => s.IsInstallmentEligible);

            RecalculateTotal();
            OnPropertyChanged(nameof(CanCreateBill));
        }

        [RelayCommand]
        void RemoveService(ServiceLineItem item)
        {
            if (item == null) return;
            SelectedServices.Remove(item);
            HasInstallmentService = SelectedServices
                .Any(s => s.IsInstallmentEligible);
            RecalculateTotal();
            OnPropertyChanged(nameof(CanCreateBill));
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
            if (!CanCreateBill) return;

            // Validate teeth input for tooth-aware services
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
                    $"No teeth entered for:\n{names}\n\n" +
                    "Proceed without tooth numbers?",
                    "Proceed", "Cancel");
                if (!proceed) return;
            }

            IsBusy = true;
            HasError = false;
            try
            {
                // 1. Create bill
                var bill = new SupabaseBill
                {
                    PatientId = PatientId,
                    PatientName = PatientName,
                    AppointmentEntryId = AppointmentEntryId,
                    TotalAmount = TotalAmount,
                    AmountPaid = 0,
                    Status = "unpaid",
                    IsInstallment = IsInstallment,
                    VisitDate = DateTime.UtcNow,
                    Notes = Notes
                };

                var saved = await _supabase.CreateBillAsync(bill);
                if (saved == null)
                {
                    HasError = true;
                    ErrorMessage = "Failed to create bill.";
                    return;
                }

                // 2. If this bill came from a booked appointment, clean that up.
                //    This must NOT gate the rest of the flow below.
                if (!string.IsNullOrEmpty(SupabaseEntryId))
                {
                    try
                    {
                        await _supabase.DeleteAppointmentEntryAsync(SupabaseEntryId);

                        // Find booking linked to this entry
                        var entries = await _supabase.GetAppointmentEntriesAsync();
                        var entry = entries.FirstOrDefault(
                            e => e.Id == SupabaseEntryId);
                        if (entry != null &&
                            !string.IsNullOrEmpty(entry.SupabaseBookingId))
                            await _supabase.DeleteBookingAsync(entry.SupabaseBookingId);
                    }
                    catch (Exception deleteEx)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[CreateBill] Delete entry: {deleteEx.Message}");
                        // Don't fail the bill for this
                    }
                }

                CreatedBillId = saved.Id;
                CreatedBillNumber = saved.BillNumber ?? "";

                // 3. Save bill items + apply to dental chart
                foreach (var item in SelectedServices)
                {
                    await _supabase.AddBillItemAsync(new SupabaseBillItem
                    {
                        BillId = saved.Id,
                        ServiceId = item.ServiceId,
                        ServiceName = item.ServiceName,
                        UnitPrice = item.UnitPrice,
                        Quantity = item.Quantity,
                        ToothNumbers = item.ToothNumbers,
                        AffectsTeeth = item.ShowTeethInput &&
                                        item.ParsedTeethNumbers.Count > 0
                    });

                    // Apply to dental chart if teeth were entered
                    if (item.ShowTeethInput &&
                        item.ParsedTeethNumbers.Count > 0)
                    {
                        await ApplyToothConditionsAsync(
                            item.ServiceName,
                            item.ParsedTeethNumbers);
                    }
                }

                // 4. Show payment overlay
                PaymentAmount = TotalAmount;
                ShowPayment = true;
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine(
                    $"[CreateBill] {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        // Apply conditions to dental chart and treatment history
        // Apply conditions to dental chart and treatment history
        private async Task ApplyToothConditionsAsync(
            string serviceName, List<int> teethNumbers)
        {
            try
            {
                var condition = ToothAwareServices.GetCondition(serviceName);
                var localPatientId = await GetLocalPatientIdAsync();

                if (localPatientId <= 0) return;

                // Look up the hex color for this condition, same palette
                // used by DentalChartViewModel, so history entries match
                // the chart's color-coding.
                var hex = ClinicApp.ViewModels.DentalChart.DentalChartViewModel
                    .ConditionColors.TryGetValue(condition, out var c) ? c : "#FFFFFF";

                foreach (var toothNum in teethNumbers)
                {
                    // Save tooth record
                    var record = new ToothRecord
                    {
                        PatientId = localPatientId,
                        ToothNumber = toothNum,
                        Condition = condition,
                        Color = hex,
                        Notes = $"{serviceName} — {DateTime.Now:MMM dd, yyyy}",
                        DateUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };
                    await _db.SaveToothRecord(record);

                    // Add ONE treatment history entry PER tooth, with ToothNumber
                    // and Color set correctly (previously defaulted to 0 / white).
                    var history = new TreatmentHistory
                    {
                        PatientId = localPatientId,
                        ToothNumber = toothNum,
                        ToothName = new ClinicApp.ViewModels.DentalChart.ToothViewModel
                        {
                            ToothNumber = toothNum
                        }.ToothName,
                        Condition = condition,
                        Color = hex,
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Description = $"{serviceName} — Tooth #{toothNum}",
                        Notes = $"Condition applied: {condition}",
                        ActionType = "Added"
                    };
                    await _db.AddTreatmentHistory(history);
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[Chart] Applied '{condition}' to teeth: {string.Join(", ", teethNumbers)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApplyTeeth] {ex.Message}");
            }
        }

        private async Task<int> GetLocalPatientIdAsync()
        {
            try
            {
                // Try by SupabaseId first
                if (!string.IsNullOrEmpty(PatientId))
                {
                    var p = await _db.GetPatientBySupabaseId(PatientId);
                    if (p != null) return p.PatientID;
                }
                return 0;
            }
            catch { return 0; }
        }

        [RelayCommand]
        async Task RecordPayment()
        {
            if (string.IsNullOrEmpty(CreatedBillId)) return;

            IsBusy = true;
            HasError = false;
            try
            {
                // Treat 0 as "skip payment for now"
                if (PaymentAmount <= 0)
                {
                    ShowPayment = false;
                    await Shell.Current.GoToAsync(
       $"{nameof(ReceiptPage)}" +
       $"?billId={CreatedBillId}" +
       $"&patientName={Uri.EscapeDataString(PatientName)}" +
       $"&patientId={Uri.EscapeDataString(PatientId)}");
                    return;
                }

                var (success, error) = await _supabase.RecordPaymentAsync(
                    CreatedBillId, PaymentAmount);

                if (!success)
                {
                    HasError = true;
                    ErrorMessage = error ?? "Failed to record payment.";
                    return;
                }

                ShowPayment = false;

                await Shell.Current.GoToAsync(
                    $"{nameof(ReceiptPage)}" +
                    $"?billId={CreatedBillId}" +
                    $"&patientName={Uri.EscapeDataString(PatientName)}");
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        async Task SkipPayment()
        {
            ShowPayment = false;
            await Shell.Current.GoToAsync(
        $"{nameof(ReceiptPage)}" +
        $"?billId={CreatedBillId}" +
        $"&patientName={Uri.EscapeDataString(PatientName)}" +
        $"&patientId={Uri.EscapeDataString(PatientId)}");
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


}

