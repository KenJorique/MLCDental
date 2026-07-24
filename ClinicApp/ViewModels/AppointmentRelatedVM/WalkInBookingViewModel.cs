using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    // ── Helper: result shown in the live dropdown ──────────────
    public class PatientSearchResult
    {
        public int PatientID { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string ContactNo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    public partial class WalkInBookingViewModel : ObservableObject
    {
        readonly DatabaseService _db;
        readonly SupabaseDataService _supabase;

        public WalkInBookingViewModel(DatabaseService db, SupabaseDataService supabase)
        {
            _db = db;
            _supabase = supabase;
            InitializeEmptySlots();
        }

        // Pre-populate 6 empty slots so TimeSlots[0-5] bindings never crash
        void InitializeEmptySlots()
        {
            var hours = new[] { 10, 11, 13, 14, 15, 16 };
            foreach (var h in hours)
            {
                var slotTime = new DateTime(
                    DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, h, 0, 0);
                TimeSlots.Add(new TimeSlotItem
                {
                    Hour = h,
                    SlotDateTime = slotTime,
                    Display = slotTime.ToString("h:00 tt"),
                    IsTaken = false,
                    IsSelected = false
                });
            }
        }

        // ── Patient ───────────────────────────────────────────
        bool _suppressSearch = false; // prevents re-search when auto-filling
        [ObservableProperty] string searchName = string.Empty;
        [ObservableProperty] string fullName = string.Empty;
        [ObservableProperty] string phone = string.Empty;
        [ObservableProperty] string email = string.Empty;
        [ObservableProperty] bool isExistingPatient;
        [ObservableProperty] bool isNewPatient;
        [ObservableProperty] bool hasPhoneError;
        [ObservableProperty] string phoneErrorMsg = string.Empty;
        [ObservableProperty] bool hasSearchResults;

        public ObservableCollection<PatientSearchResult> SearchResults { get; } = new();

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
            !IsBusy;

        void NotifyCanConfirm() => OnPropertyChanged(nameof(CanConfirm));

        // ── Live name search ──────────────────────────────────
        partial void OnSearchNameChanged(string value)
        {
            // Skip search if we are auto-filling from a selection
            if (_suppressSearch) return;

            // Clear patient state when user edits the name field
            if (IsExistingPatient)
            {
                IsExistingPatient = false;
                IsNewPatient = false;
                Phone = string.Empty;
                Email = string.Empty;
                FullName = string.Empty;
                _existingPatient = null;
            }

            // BUGFIX: FullName was only ever set inside SelectPatient() (tapping a
            // dropdown result). For a brand-new patient with no matching record,
            // nothing was ever available to tap, so FullName stayed empty forever
            // and CanConfirm could never become true. Keep it in sync with what's
            // typed here; SelectPatient() still overwrites it correctly afterward
            // if the user does pick an existing patient.
            FullName = value;
            IsNewPatient = !string.IsNullOrWhiteSpace(value);

            if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
            {
                SearchResults.Clear();
                HasSearchResults = false;
                return;
            }

            MainThread.BeginInvokeOnMainThread(async () =>
                await SearchPatientsAsync(value));
        }

        private async Task SearchPatientsAsync(string query)
        {
            try
            {
                var allPatients = await _db.GetPatients();
                var q = query.ToLowerInvariant();

                var matches = allPatients
                    .Where(p =>
                        (p.FirstName + " " + p.LastName).ToLowerInvariant().Contains(q) ||
                        (p.LastName + " " + p.FirstName).ToLowerInvariant().Contains(q))
                    .Take(5)
                    .Select(p => new PatientSearchResult
                    {
                        PatientID = p.PatientID,
                        FullName = $"{p.FirstName} {p.LastName}".Trim(),
                        DisplayName = $"{p.LastName}, {p.FirstName}".Trim(),
                        ContactNo = p.MobileNo ?? string.Empty,
                        Email = p.Email ?? string.Empty,
                    })
                    .ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SearchResults.Clear();
                    foreach (var m in matches)
                        SearchResults.Add(m);
                    HasSearchResults = SearchResults.Count > 0;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Search] {ex.Message}");
            }
        }

        // ── Tap a search result → auto-fill ──────────────────
        [RelayCommand]
        void SelectPatient(PatientSearchResult result)
        {
            if (result is null) return;

            // Suppress OnSearchNameChanged so setting SearchName
            // does not trigger another search and re-show the dropdown
            _suppressSearch = true;
            SearchName = result.FullName;
            FullName = result.FullName;
            Phone = result.ContactNo;
            Email = result.Email;
            IsExistingPatient = true;
            IsNewPatient = false;
            SearchResults.Clear();
            HasSearchResults = false;
            _suppressSearch = false;

            NotifyCanConfirm();
            UpdateSummary();
        }

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

        partial void OnIsBusyChanged(bool value) => NotifyCanConfirm();

        partial void OnAppointmentDateChanged(DateTime value)
        {
            IsSunday = value.DayOfWeek == DayOfWeek.Sunday;
            NotifyCanConfirm();

            if (!IsSunday)
            {
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
                var booked = await _supabase.GetBookedTimeSlotsForDateAsync(date);
                var hours = new[] { 10, 11, 13, 14, 15, 16 };

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    TimeSlots.Clear();
                    _selectedSlot = null;
                    SelectedSlotDisplay = string.Empty;
                    NotifyCanConfirm();

                    foreach (var h in hours)
                    {
                        var slotTime = new DateTime(date.Year, date.Month, date.Day, h, 0, 0);
                        var isTaken = booked.Any(b => b.ToLocalTime().Hour == h);

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
            if (string.IsNullOrWhiteSpace(FullName) || _selectedSlot == null)
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

        // ── Confirm booking ───────────────────────────────────
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

                if (!IsExistingPatient)
                {
                    var parts = FullName.Trim().Split(' ', 2);
                    var p = new Patient
                    {
                        FirstName = parts.Length > 0 ? parts[0] : FullName,
                        LastName = parts.Length > 1 ? parts[1] : "",
                        MobileNo = Phone,
                        Email = Email,
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

                var bookingId = Guid.NewGuid().ToString();

                var localEntry = new AppointmentEntry
                {
                    SupabaseBookingId = bookingId,
                    PatientName = FullName,
                    Phone = Phone,
                    Email = Email,
                    Notes = Notes,
                    AppointmentDateTime = localTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = "approved"
                };
                await _db.AddAppointmentEntry(localEntry);

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

                try
                {
                    await _supabase.SyncToGoogleTasksAsync(
                        "", FullName, "Walk-In Appointment", localTime, Phone, Notes);
                }
                catch (Exception gEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[WalkIn] Google Tasks: {gEx.Message}");
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
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        async Task Cancel() => await Shell.Current.GoToAsync("..");
    }
}
