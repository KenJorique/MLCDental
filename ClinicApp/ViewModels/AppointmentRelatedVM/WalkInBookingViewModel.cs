using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    public partial class WalkInBookingViewModel : ObservableObject
    {
        readonly DatabaseService _db;
        readonly SupabaseDataService _supabase;

        public WalkInBookingViewModel(
            DatabaseService db,
            SupabaseDataService supabase)
        {
            _db = db;
            _supabase = supabase;
        }

        // ── Patient ───────────────────────────────────────────
        [ObservableProperty] string phone = string.Empty;
        [ObservableProperty] string fullName = string.Empty;
        [ObservableProperty] string email = string.Empty;
        [ObservableProperty] bool isExistingPatient;
        [ObservableProperty] bool isNewPatient;
        [ObservableProperty] bool hasPhoneError;
        [ObservableProperty] string phoneErrorMsg = string.Empty;

        // ── Appointment ───────────────────────────────────────
      
        [ObservableProperty] DateTime appointmentDate = DateTime.Today;
        [ObservableProperty] string notes = string.Empty;
        [ObservableProperty] bool isLoadingSlots;
        [ObservableProperty] bool hasNoSlots;
        [ObservableProperty] bool isSunday;
        [ObservableProperty] bool isBusy;

        // ── UI state ──────────────────────────────────────────
        [ObservableProperty] bool hasError;
        [ObservableProperty] string errorMessage = string.Empty;
        [ObservableProperty] bool hasSummary;
        [ObservableProperty] string summaryText = string.Empty;
        [ObservableProperty] string selectedSlotDisplay = string.Empty;

        public DateTime MinDate => DateTime.Today;
        public DateTime MaxDate => DateTime.Today.AddDays(30);

        public ObservableCollection<TimeSlotItem> TimeSlots { get; } = new();

   

        private TimeSlotItem? _selectedSlot;
        private SupabasePatient? _existingPatient;

        // ── CanConfirm ────────────────────────────────────────
        public bool CanConfirm =>
            !string.IsNullOrWhiteSpace(FullName) &&
            !string.IsNullOrWhiteSpace(Phone) &&
            Phone.StartsWith("09") &&
            Phone.Length == 11 &&
            _selectedSlot != null &&
            !IsSunday &&
            !IsBusy;

        void NotifyCanConfirm() =>
            OnPropertyChanged(nameof(CanConfirm));

        partial void OnFullNameChanged(string value)
        {
            NotifyCanConfirm();
            UpdateSummary();
        }

        partial void OnPhoneChanged(string value)
        {
            NotifyCanConfirm();
            HasPhoneError = false;
        }

   

        partial void OnIsBusyChanged(bool value) =>
            NotifyCanConfirm();

        partial void OnAppointmentDateChanged(DateTime value)
        {
            IsSunday = value.DayOfWeek == DayOfWeek.Sunday;
            NotifyCanConfirm();

            if (!IsSunday)
            {
                // Reset slot selection when date changes
                _selectedSlot = null;
                SelectedSlotDisplay = string.Empty;
                NotifyCanConfirm();
                MainThread.BeginInvokeOnMainThread(async () =>
                    await LoadSlotsAsync(value));
            }
            UpdateSummary();
        }

        // ── Initialize ────────────────────────────────────────
        public async Task InitializeAsync()
        {
            AppointmentDate = DateTime.Today;
            IsSunday = DateTime.Today.DayOfWeek == DayOfWeek.Sunday;
            await LoadSlotsAsync(DateTime.Today);
        }

        // ── Patient lookup ────────────────────────────────────
        [RelayCommand]
        async Task LookupPatient()
        {
            HasPhoneError = false;
            HasError = false;

            if (string.IsNullOrWhiteSpace(Phone))
            {
                PhoneErrorMsg = "Please enter a phone number";
                HasPhoneError = true;
                return;
            }

            if (!Phone.StartsWith("09") || Phone.Length != 11)
            {
                PhoneErrorMsg = "Must start with 09 and be 11 digits";
                HasPhoneError = true;
                return;
            }

            IsBusy = true;
            try
            {
                var existing = await _supabase
                    .GetPatientByPhoneAsync(Phone);

                if (existing != null)
                {
                    _existingPatient = existing;
                    FullName = $"{existing.FirstName} {existing.LastName}".Trim();
                    Email = existing.Email ?? "";
                    IsExistingPatient = true;
                    IsNewPatient = false;
                }
                else
                {
                    _existingPatient = null;
                    FullName = string.Empty;
                    Email = string.Empty;
                    IsExistingPatient = false;
                    IsNewPatient = true;
                }

                NotifyCanConfirm();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Lookup failed: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        // ── Load time slots ───────────────────────────────────
        public async Task LoadSlotsAsync(DateTime date)
        {
            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                TimeSlots.Clear();
                HasNoSlots = true;
                return;
            }

            IsLoadingSlots = true;
            HasNoSlots = false;

            try
            {
                var booked = await _supabase
                    .GetBookedTimeSlotsForDateAsync(date);

                var hours = new[] { 10, 11, 12, 13, 14, 15 };

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    TimeSlots.Clear();
                    _selectedSlot = null;
                    SelectedSlotDisplay = string.Empty;
                    NotifyCanConfirm();

                    foreach (var h in hours)
                    {
                        var slotTime = new DateTime(
                            date.Year, date.Month, date.Day, h, 0, 0);

                        var isTaken = booked.Any(b =>
                            b.ToLocalTime().Hour == h);

                        TimeSlots.Add(new TimeSlotItem
                        {
                            Hour = h,
                            SlotDateTime = slotTime,
                            Display = slotTime.ToString("h:00 tt"),
                            IsTaken = isTaken,
                            IsSelected = false
                        });
                    }

                    HasNoSlots = TimeSlots.All(s => s.IsTaken);
                });
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load slots: {ex.Message}";
            }
            finally { IsLoadingSlots = false; }
        }

        [RelayCommand]
        void SelectSlot(TimeSlotItem slot)
        {
            if (slot == null || slot.IsTaken) return;

            foreach (var s in TimeSlots)
            {
                s.IsSelected = false;
                s.RefreshColors();
            }

            slot.IsSelected = true;
            slot.RefreshColors();
            _selectedSlot = slot;
            SelectedSlotDisplay = slot.Display;

            NotifyCanConfirm();
            UpdateSummary();
        }

        void UpdateSummary()
        {
            if (string.IsNullOrWhiteSpace(FullName)
                || _selectedSlot == null)
            {
                HasSummary = false;
                return;
            }

            HasSummary = true;
            SummaryText =
                $"Patient:   {FullName}\n" +
                $"Date:        {_selectedSlot.SlotDateTime:MMM dd, yyyy}\n" +
                $"Time:        {_selectedSlot.Display}\n" +
                $"Status:     Auto-approved ✓";
        }

        // ── Confirm walk-in booking ───────────────────────────
        [RelayCommand]
        async Task ConfirmBooking()
        {
            if (!CanConfirm || _selectedSlot == null) return;

            HasError = false;
            IsBusy = true;

            try
            {
                var localTime = _selectedSlot.SlotDateTime;
                var utcTime = localTime.ToUniversalTime();

                // 1. Create patient if new
                if (!IsExistingPatient)
                {
                    var parts = FullName.Trim().Split(' ', 2);
                    var p = new Patient
                    {
                        FirstName = parts.Length > 0 ? parts[0] : FullName,
                        LastName = parts.Length > 1 ? parts[1] : "",
                        MobileNo = Phone,
                        Email = Email,
                        ReferredBy = "Walk-in",
                        DateRegistered = DateTime.Now.ToString("yyyy-MM-dd")
                    };
                    await _db.AddPatient(p);

                    var sp = new SupabasePatient
                    {
                        FirstName = p.FirstName,
                        LastName = p.LastName,
                        Phone = Phone,
                        Email = Email,
                        ReferredBy = "Walk-in",
                        DateRegistered = DateTime.UtcNow
                    };
                    await _supabase.AddPatientAsync(sp);
                }

                // 2. Create appointment entry — auto-approved
                var bookingId = Guid.NewGuid().ToString();

                var localEntry = new AppointmentEntry
                {
                    SupabaseBookingId = bookingId,
                    PatientName = FullName,
                    Phone = Phone,
                    Email = Email,
                    Notes = Notes,
                    AppointmentDateTime = localTime
                                             .ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = "approved"
                };
                await _db.AddAppointmentEntry(localEntry);

                // 3. Save to Supabase appointment_entries
                var supEntry = new SupabaseAppointmentEntry
                {
                    SupabaseBookingId = bookingId,
                    PatientName = FullName,
                    Phone = Phone,
                    Email = Email,
                    Notes = Notes,
                    AppointmentDateTime = utcTime,
                    Status = "approved"
                };
                await _supabase.AddAppointmentEntryAsync(supEntry);

                // 4. Google Tasks (silent fail)
                try
                {
                    await _supabase.SyncToGoogleTasksAsync(
                        "", FullName, "Walk- In Appointment", localTime, Phone, Notes);
                }
                catch (Exception gEx)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[WalkIn] Google Tasks: {gEx.Message}");
                }

                await Shell.Current.DisplayAlert(
                    "✓ Booking Confirmed",
                    $"Walk-in appointment booked!\n\n" +
                    $"Patient:  {FullName}\n" +
                    $"Date:       {localTime:MMM dd, yyyy}\n" +
                    $"Time:       {localTime:h:00 tt}",
                    "Done");

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Booking failed: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(
                    $"[WalkIn] Error: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        async Task Cancel() =>
            await Shell.Current.GoToAsync("..");
    }
}