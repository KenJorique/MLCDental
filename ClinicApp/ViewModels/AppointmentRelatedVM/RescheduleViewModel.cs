
using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    [QueryProperty(nameof(BookingId), "bookingId")]
    [QueryProperty(nameof(PatientName), "patientName")]
    [QueryProperty(nameof(CurrentDateTime), "currentDateTime")]
    public partial class RescheduleViewModel : ObservableObject
    {
        private static readonly TimeZoneInfo PhZone = GetPhilippineZone();

        private static TimeZoneInfo GetPhilippineZone()
        {
            foreach (var id in new[] { "Asia/Manila", "Philippine Standard Time", "UTC+8" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch { }
            }
            // Fallback: manually create UTC+8
            return TimeZoneInfo.CreateCustomTimeZone(
                "PST", TimeSpan.FromHours(8), "Philippine Standard Time", "PST");
        }

        readonly SupabaseDataService _supabaseData;

        [ObservableProperty] private string bookingId = string.Empty;
        [ObservableProperty] private string patientName = string.Empty;
        [ObservableProperty] private string currentDateTime = string.Empty;
        [ObservableProperty] private DateTime selectedDate = DateTime.Today.AddDays(1);
        [ObservableProperty] private bool isLoadingSlots;
        [ObservableProperty] private bool hasNoSlots = true;
        [ObservableProperty] private bool hasSelection;
        [ObservableProperty] private bool hasError;
        [ObservableProperty] private string errorMessage = string.Empty;
        [ObservableProperty] private string selectedSummary = string.Empty;

        public DateTime MinDate => DateTime.Today.AddDays(1);
        public DateTime MaxDate => DateTime.Today.AddDays(30);

        public ObservableCollection<TimeSlotItem> TimeSlots { get; } = new();

        private TimeSlotItem? _selectedSlot;

        public RescheduleViewModel(SupabaseDataService supabaseData)
        {
            _supabaseData = supabaseData;
            InitializeEmptySlots();
        }

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

        public async Task InitializeAsync()
        {
            // Skip Sundays for default date
            var date = DateTime.Today.AddDays(1);
            while (date.DayOfWeek == DayOfWeek.Sunday)
                date = date.AddDays(1);

            SelectedDate = date;
            await LoadSlotsForDateAsync(date);
        }

        public async Task LoadSlotsForDateAsync(DateTime date)
        {
            // Block Sundays
            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                HasError = true;
                ErrorMessage = "Clinic is closed on Sundays. Please pick another day.";
                TimeSlots.Clear();
                HasNoSlots = true;
                HasSelection = false;
                return;
            }

            HasError = false;
            ErrorMessage = string.Empty;
            IsLoadingSlots = true;
            HasNoSlots = false;
            _selectedSlot = null;
            HasSelection = false;

            try
            {
                // Check both bookings table (website) AND appointment_entries (app)
                var bookedSlots = await _supabaseData
                    .GetBookedTimeSlotsForDateAsync(date);

                var allEntries = await _supabaseData.GetAppointmentEntriesAsync();
                var startUtc = date.Date.ToUniversalTime();
                var endUtc = startUtc.AddDays(1);
                var entrySlots = allEntries
                    .Where(e => e.AppointmentDateTime >= startUtc
                             && e.AppointmentDateTime < endUtc
                             && e.Status != "rejected"
                             && e.Status != "cancelled")
                    .Select(e => e.AppointmentDateTime)
                    .ToList();

                var allBooked = bookedSlots.Concat(entrySlots).ToList();

                TimeSlots.Clear();

                var hours = new[] { 10, 11, 13, 14, 15, 16 };
                foreach (var h in hours)
                {
                    var slotTime = new DateTime(
                        date.Year, date.Month, date.Day, h, 0, 0);

                    // Check if this slot is already taken
                    var slotUtc = slotTime.ToUniversalTime();

                    var isTaken = bookedSlots.Any(b =>
                        b == slotUtc);

                    var item = new TimeSlotItem
                    {
                        Hour = h,
                        SlotDateTime = slotTime,
                        Display = slotTime.ToString("h:00 tt"),
                        IsTaken = isTaken,
                        IsSelected = false
                    };

                    item.RefreshColors();

                    TimeSlots.Add(item);
                }

                HasNoSlots = !TimeSlots.Any();
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load slots: {ex.Message}";
            }
            finally
            {
                IsLoadingSlots = false;
            }
        }

        [RelayCommand]
        void SelectSlot(TimeSlotItem slot)
        {
            if (slot == null || slot.IsTaken) return;

            // Deselect all
            foreach (var s in TimeSlots)
                s.IsSelected = false;

            // Select this one
            slot.IsSelected = true;
            _selectedSlot = slot;
            HasSelection = true;

            SelectedSummary =
                $"{slot.SlotDateTime:MMMM dd, yyyy} at {slot.Display}";
        }

        [RelayCommand]
        async Task ConfirmReschedule()
        {
            if (_selectedSlot == null || string.IsNullOrEmpty(BookingId))
                return;

            IsLoadingSlots = true;
            try
            {
                // Convert Philippine time to UTC for storage
                var localSlot = DateTime.SpecifyKind(
                    _selectedSlot.SlotDateTime, DateTimeKind.Unspecified);
                var utcTime = TimeZoneInfo.ConvertTimeToUtc(localSlot, PhZone);

                // TEMP DIAGNOSTIC — remove once the reschedule time-shift bug is found.
                System.Diagnostics.Debug.WriteLine(
                    $"[DIAG-WRITE] Picked local slot: {_selectedSlot.SlotDateTime:yyyy-MM-dd HH:mm:ss} " +
                    $"(Kind={_selectedSlot.SlotDateTime.Kind}) → sending utcTime=" +
                    $"{utcTime:yyyy-MM-dd HH:mm:ss} (Kind={utcTime.Kind})");

                // Handles appointments that came from a real online booking
                // (i.e. BookingId matches an actual row in the `bookings` table).
                await _supabaseData.RescheduleBookingAsync(BookingId, utcTime);

                var entries = await _supabaseData.GetAppointmentEntriesAsync();
                var entry = entries.FirstOrDefault(e => e.SupabaseBookingId == BookingId);

                // TEMP DIAGNOSTIC — remove once the reschedule time-shift bug is found.
                if (entry != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[DIAG-WRITE] Re-fetched entry AppointmentDateTime=" +
                        $"{entry.AppointmentDateTime:yyyy-MM-dd HH:mm:ss} (Kind={entry.AppointmentDateTime.Kind}) " +
                        $"vs sent utcTime={utcTime:yyyy-MM-dd HH:mm:ss} (Kind={utcTime.Kind}) — " +
                        $"{(entry.AppointmentDateTime == utcTime ? "MATCH" : "MISMATCH")}");
                }

                if (entry != null && entry.AppointmentDateTime != utcTime)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ConfirmReschedule] RescheduleBookingAsync did not update the entry " +
                        $"(likely a walk-in with no matching bookings row) — updating directly.");

                    await _supabaseData.DeleteAppointmentEntryAsync(entry.Id);

                    var replacement = new SupabaseAppointmentEntry
                    {
                        SupabaseBookingId = entry.SupabaseBookingId,
                        PatientName = entry.PatientName,
                        Phone = entry.Phone,
                        Email = entry.Email,
                        Notes = entry.Notes,
                        AppointmentDateTime = utcTime,
                        Status = entry.Status
                    };
                    await _supabaseData.AddAppointmentEntryAsync(replacement);
                }

                await Shell.Current.DisplayAlert(
                    "Rescheduled",
                    $"{PatientName}'s appointment has been rescheduled to\n{SelectedSummary}",
                    "OK");

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to reschedule: {ex.Message}";
            }
            finally
            {
                IsLoadingSlots = false;
            }
        }

        [RelayCommand]
        async Task Cancel()
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    public partial class TimeSlotItem : ObservableObject
    {
        public int Hour { get; set; }
        public DateTime SlotDateTime { get; set; }
        public string Display { get; set; } = string.Empty;

        private bool _isTaken;
        public bool IsTaken
        {
            get => _isTaken;
            set { _isTaken = value; RefreshColors(); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; RefreshColors(); }
        }

        // Observable color properties — set directly so CollectionView updates
        [ObservableProperty] Color backgroundColor = Colors.White;
        [ObservableProperty] Color borderColor = Color.FromArgb("#C8A84B");
        [ObservableProperty] Color textColor = Color.FromArgb("#1A1A2E");
        [ObservableProperty] Color statusColor = Color.FromArgb("#2E7D32");
        [ObservableProperty] string statusText = "Available";

        public void RefreshColors()
        {
            if (_isTaken)
            {
                BackgroundColor = Color.FromArgb("#F0F0F0");
                BorderColor = Color.FromArgb("#CCCCCC");
                TextColor = Color.FromArgb("#AAAAAA");
                StatusText = "Unavailable";
                StatusColor = Color.FromArgb("#AAAAAA");
            }
            else if (_isSelected)
            {
                BackgroundColor = Color.FromArgb("#2E7D32");
                BorderColor = Color.FromArgb("#2E7D32");
                TextColor = Colors.White;
                StatusText = "Selected";
                StatusColor = Color.FromArgb("#A5D6A7");
            }
            else
            {
                BackgroundColor = Colors.White;
                BorderColor = Color.FromArgb("#C8A84B");
                TextColor = Color.FromArgb("#1A1A2E");
                StatusText = "Available";
                StatusColor = Color.FromArgb("#2E7D32");
            }
        }
    }
}