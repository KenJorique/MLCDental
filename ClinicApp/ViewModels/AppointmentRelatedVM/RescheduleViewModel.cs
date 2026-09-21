using ClinicApp.Models;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
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

        // Resolves the Philippine time zone, with a manual UTC+8 fallback.
        // Windows doesn't recognize the IANA id "Asia/Manila", so without this
        // fallback chain, TimeZoneInfo.FindSystemTimeZoneById would throw on
        // Windows builds specifically.
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
        readonly DatabaseService _db;

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

        // ---------------------------------------------------------------
        // ConfirmationPopup helpers — replace Shell.Current.DisplayAlert
        // everywhere in this ViewModel with the app's dimmed-backdrop
        // rounded-card popup.
        // ---------------------------------------------------------------

        static Page CurrentPage =>
            Shell.Current?.CurrentPage
            ?? Application.Current?.Windows.FirstOrDefault()?.Page
            ?? throw new InvalidOperationException("No current page available to host the popup.");

        // Yes/No confirmation. Returns true only if the confirm button was tapped.
        static async Task<bool> ShowConfirmAsync(
            string title, string message, string confirmText = "Yes", Color? confirmColor = null)
        {
            var popup = new ConfirmationPopup(title, message, confirmText, confirmColor);
            var result = await CurrentPage.ShowPopupAsync(popup);
            return result is true;
        }

        // Plain OK-only notice (used in place of single-button DisplayAlert calls).
        static async Task ShowNoticeAsync(string title, string message, string okText = "OK")
        {
            var popup = new ConfirmationPopup(title, message, okText, null, showCancelButton: false);
            await CurrentPage.ShowPopupAsync(popup);
        }

        // Convenience wrapper for error alerts so call sites read the same as before.
        static Task ShowErrorAsync(string message) => ShowNoticeAsync("Error", message);

        // Injects the data services and seeds empty time slots.
        public RescheduleViewModel(SupabaseDataService supabaseData, DatabaseService db)
        {
            _supabaseData = supabaseData;
            _db = db;
            InitializeEmptySlots();
        }

        // Fills TimeSlots with the clinic's fixed hours, all initially open.
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

        // Picks the default date (skipping Sunday) and loads its slots.
        public async Task InitializeAsync()
        {
            // Skip Sundays for default date
            var date = DateTime.Today.AddDays(1);
            while (date.DayOfWeek == DayOfWeek.Sunday)
                date = date.AddDays(1);

            SelectedDate = date;
            await LoadSlotsForDateAsync(date);
        }

        // Loads available time slots for the given date, checking both bookings and appointment entries.
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
                // Check both bookings table (website) AND appointment_entries (app).
                var bookedSlots = await _supabaseData
                    .GetBookedTimeSlotsForDateAsync(date);

                var allEntries = await _supabaseData.GetAppointmentEntriesAsync();

                // Entry timestamps come back from Supabase already shifted by the
                // Manila offset with Kind mislabeled — normalize before comparing,
                // same as everywhere else this app reads AppointmentDateTime.
                var dayStartLocal = date.Date;
                var dayEndLocal = dayStartLocal.AddDays(1);

                var entrySlots = allEntries
                    .Where(e => e.Status != "rejected" && e.Status != "cancelled")
                    .Select(e => TimeZoneInfo.ConvertTimeFromUtc(
                        SupabaseDataService.NormalizeSupabaseUtc(e.AppointmentDateTime), PhZone))
                    .Where(local => local >= dayStartLocal && local < dayEndLocal)
                    .Select(local => TimeZoneInfo.ConvertTimeToUtc(
                        DateTime.SpecifyKind(local, DateTimeKind.Unspecified), PhZone))
                    .ToList();

                var allBooked = bookedSlots.Concat(entrySlots).ToList();

                TimeSlots.Clear();

                var hours = new[] { 10, 11, 13, 14, 15, 16 };
                foreach (var h in hours)
                {
                    var slotTime = new DateTime(
                        date.Year, date.Month, date.Day, h, 0, 0);

                    // Convert through the Philippine zone explicitly, not the device's
                    // local zone — matches how utcTime is computed in ConfirmReschedule
                    // below, so a slot that's actually taken never shows as open.
                    var slotUtc = TimeZoneInfo.ConvertTimeToUtc(
                        DateTime.SpecifyKind(slotTime, DateTimeKind.Unspecified), PhZone);

                    var isTaken = allBooked.Any(b => b == slotUtc);

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

        // Selects a slot, deselecting any other. Taken slots are simply ignored —
        // their greyed-out styling is the only feedback, no popup.
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

        // Applies the new time to the booking/entry and logs the activity.
        [RelayCommand]
        async Task ConfirmReschedule()
        {
            if (_selectedSlot == null || string.IsNullOrEmpty(BookingId))
                return;

            bool confirmed = await ShowConfirmAsync(
                "Confirm Reschedule",
                $"Reschedule {PatientName}'s appointment to {SelectedSummary}?",
                "Yes, reschedule");

            if (!confirmed) return;

            IsLoadingSlots = true;
            try
            {
                // Convert Philippine time to UTC for storage
                var localSlot = DateTime.SpecifyKind(
                    _selectedSlot.SlotDateTime, DateTimeKind.Unspecified);
                var utcTime = TimeZoneInfo.ConvertTimeToUtc(localSlot, PhZone);

                // Check which situation this is: an already-approved appointment being moved,
                // or a still-pending booking (no entry exists yet) being rescheduled.
                var entries = await _supabaseData.GetAppointmentEntriesAsync();
                var entry = entries.FirstOrDefault(e => e.SupabaseBookingId == BookingId);

                if (entry != null)
                {
                    // Already-approved appointment — just move its date/time.
                    await _supabaseData.RescheduleBookingAsync(BookingId, utcTime);

                    var refreshed = await _supabaseData.GetAppointmentEntriesAsync();
                    var refreshedEntry = refreshed.FirstOrDefault(e => e.SupabaseBookingId == BookingId);

                    // Normalize before comparing — the raw value read back from Supabase
                    // isn't directly comparable to utcTime without this (see note above).
                    var entryNormalizedUtc = refreshedEntry != null
                        ? SupabaseDataService.NormalizeSupabaseUtc(refreshedEntry.AppointmentDateTime)
                        : (DateTime?)null;

                    if (refreshedEntry != null && entryNormalizedUtc != utcTime)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[ConfirmReschedule] RescheduleBookingAsync did not update the entry " +
                            $"(likely a walk-in with no matching bookings row) — updating directly.");

                        await _supabaseData.DeleteAppointmentEntryAsync(refreshedEntry.Id);

                        var replacement = new SupabaseAppointmentEntry
                        {
                            SupabaseBookingId = refreshedEntry.SupabaseBookingId,
                            PatientName = refreshedEntry.PatientName,
                            Phone = refreshedEntry.Phone,
                            Email = refreshedEntry.Email,
                            Notes = refreshedEntry.Notes,
                            AppointmentDateTime = utcTime,
                            Status = refreshedEntry.Status
                        };
                        await _supabaseData.AddAppointmentEntryAsync(replacement);
                    }
                }
                else
                {
                    // No entry yet — this booking is still pending. Approve it directly at the newly picked time.
                    var booking = await _supabaseData.GetBookingByIdAsync(BookingId);
                    if (booking == null)
                    {
                        HasError = true;
                        ErrorMessage = "Booking not found — it may have already been removed.";
                        return;
                    }

                    var (success, approveError) = await _supabaseData.ApproveBookingAsync(_db, booking, localSlot);
                    if (!success)
                    {
                        HasError = true;
                        ErrorMessage = approveError ?? "Failed to approve and schedule this booking.";
                        return;
                    }
                }

                await ShowNoticeAsync(
                    "Rescheduled",
                    $"{PatientName}'s appointment has been rescheduled to {SelectedSummary}");

                await _supabaseData.LogActivityAsync("AppointmentRescheduled",
                    $"{PatientName}'s appointment was rescheduled to {SelectedSummary}");

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

        // Discards and goes back.
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

        // Recomputes this slot's colors/status text from its taken/selected state.
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
