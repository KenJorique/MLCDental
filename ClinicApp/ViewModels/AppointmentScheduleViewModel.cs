
using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    public partial class AppointmentScheduleViewModel : ObservableObject
    {
        readonly DatabaseService _db;
        readonly SupabaseDataService _supabaseData;

        // Add this field to track the Supabase appointment_entries UUID
        private string _selectedSupabaseEntryId = string.Empty;

        public ObservableCollection<AppointmentEntry> TodayAppointments { get; } = new();
        public ObservableCollection<AppointmentEntry> WeekAppointments { get; } = new();

        // Calendar grid — 7 days x time slots
        public ObservableCollection<CalendarDayColumn> WeekColumns { get; } = new();

        [ObservableProperty] private bool isListView = true;
        [ObservableProperty] private bool isCalendarView;
        [ObservableProperty] private bool isRefreshing;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private DateTime currentDate = DateTime.Today;
        [ObservableProperty] private string dateRangeLabel = string.Empty;
        [ObservableProperty] private AppointmentEntry? selectedAppointment;
        [ObservableProperty] private bool showDetail;
        [ObservableProperty] private int todayCount;
        [ObservableProperty] private int weekCount;

        // always start from Sunday of the CURRENT week
        public DateTime WeekStart
        {
            get
            {
                var diff = (7 + (CurrentDate.DayOfWeek - DayOfWeek.Sunday)) % 7;
                return CurrentDate.AddDays(-diff).Date;
            }
        }

        public AppointmentScheduleViewModel(DatabaseService db, SupabaseDataService supabaseData)
        {
            _db = db;
            UpdateDateLabel();
            _supabaseData = supabaseData;
        }

        private void UpdateDateLabel()
        {
            var ws = WeekStart;
            var we = ws.AddDays(6);
            DateRangeLabel = $"{ws:MMM d} – {we:d, yyyy}";
        }

        [RelayCommand]
        void ShowList()
        {
            IsListView = true;
            IsCalendarView = false;
        }

        [RelayCommand]
        void ShowCalendar()
        {
            IsListView = false;
            IsCalendarView = true;
        }

        [RelayCommand]
        async Task PreviousWeek()
        {
            CurrentDate = CurrentDate.AddDays(-7);
            UpdateDateLabel();
            await LoadAppointments();
        }

        [RelayCommand]
        async Task NextWeek()
        {
            CurrentDate = CurrentDate.AddDays(7);
            UpdateDateLabel();
            await LoadAppointments();
        }

        [RelayCommand]
        async Task GoToToday()
        {
            CurrentDate = DateTime.Today;
            UpdateDateLabel();
            await LoadAppointments();
        }

        [RelayCommand]
        async Task SelectAppointment(AppointmentEntry entry)
        {
            if (entry == null) return;

            // Pending bookings → go to Appointments page to approve/reschedule
            if (entry.Status == "pending" || entry.Status == "rescheduled")
            {
                await Shell.Current.DisplayAlert(
                    $"{entry.PatientName}",
                    $"Service: {entry.Service}\n" +
                    $"Date: {entry.DateDisplay} {entry.TimeDisplay} {entry.AmPm}\n" +
                    $"Status: {entry.StatusLabel}\n\n" +
                    "Go to the Appointments tab to approve or reschedule.",
                    "OK");
                return;
            }

            // Approved/completed/cancelled → show detail overlay
            SelectedAppointment = entry;
            ShowDetail = true;

            try
            {
                var all = await _supabaseData.GetAppointmentEntriesAsync();
                var match = all.FirstOrDefault(
                    a => a.SupabaseBookingId == entry.SupabaseBookingId);
                _selectedSupabaseEntryId = match?.Id ?? string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SelectAppointment] {ex.Message}");
                _selectedSupabaseEntryId = string.Empty;
            }
        }

        [RelayCommand]
        void CloseDetail()
        {
            ShowDetail = false;
            SelectedAppointment = null;
        }

        [RelayCommand]
        async Task MarkCompleted()
        {
            if (SelectedAppointment == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Mark as completed",
                $"Mark {SelectedAppointment.PatientName}'s appointment as completed?",
                "Yes", "Cancel");
            if (!confirm) return;

            try
            {
                // 1. Update local SQLite status
                await _db.UpdateAppointmentStatus(
                    SelectedAppointment.Id, "completed");

                // 2. Update Supabase appointment_entries status
                if (!string.IsNullOrEmpty(_selectedSupabaseEntryId))
                    await _supabaseData.UpdateAppointmentEntryStatusAsync(
                        _selectedSupabaseEntryId, "completed");

                // 3. Delete from Supabase bookings — completed = no longer pending
                if (!string.IsNullOrEmpty(SelectedAppointment.SupabaseBookingId))
                    await _supabaseData.DeleteBookingAsync(
                        SelectedAppointment.SupabaseBookingId);

                System.Diagnostics.Debug.WriteLine(
                    $"[MarkCompleted] Booking {SelectedAppointment.SupabaseBookingId} deleted from Supabase.");

                ShowDetail = false;
                SelectedAppointment = null;
                await LoadAppointments();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MarkCompleted] {ex.Message}");
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }

            // In MarkCompleted, after updating Supabase status:
            if (GoogleTasksService.Instance.IsSignedIn
                && !string.IsNullOrEmpty(SelectedAppointment.GoogleTaskId))
            {
                await GoogleTasksService.Instance
                    .CompleteTaskAsync(SelectedAppointment.GoogleTaskId);
            }

            // In MarkCompleted(), after updating Supabase status:
            var accessToken = Preferences.Get("google_access_token", "");
            if (!string.IsNullOrEmpty(accessToken)
                && !string.IsNullOrEmpty(SelectedAppointment?.GoogleTaskId))
            {
                // Call Edge Function to complete the task
                await _supabaseData.CompleteGoogleTaskAsync(
                    accessToken,
                    SelectedAppointment.GoogleTaskId);
            }
        }

        [RelayCommand]
        async Task CancelAppointment()
        {
            if (SelectedAppointment == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Cancel appointment",
                $"Cancel {SelectedAppointment.PatientName}'s appointment?\n" +
                "This will also remove the booking from the system.",
                "Yes, cancel", "Keep");
            if (!confirm) return;

            try
            {
                // 1. Update local SQLite status
                await _db.UpdateAppointmentStatus(
                    SelectedAppointment.Id, "cancelled");

                // 2. Delete from Supabase appointment_entries
                if (!string.IsNullOrEmpty(_selectedSupabaseEntryId))
                    await _supabaseData.DeleteAppointmentEntryAsync(
                        _selectedSupabaseEntryId);

                // 3. Delete from Supabase bookings
                if (!string.IsNullOrEmpty(SelectedAppointment.SupabaseBookingId))
                    await _supabaseData.DeleteBookingAsync(
                        SelectedAppointment.SupabaseBookingId);

                System.Diagnostics.Debug.WriteLine(
                    $"[CancelAppointment] Cleaned up booking {SelectedAppointment.SupabaseBookingId}");

                ShowDetail = false;
                SelectedAppointment = null;
                await LoadAppointments();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CancelAppointment] {ex.Message}");
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        [RelayCommand]
        async Task RescheduleAppointment()
        {
            if (SelectedAppointment == null) return;
            await _db.UpdateAppointmentStatus(SelectedAppointment.Id, "rescheduled");
            ShowDetail = false;
            await LoadAppointments();
        }

        [RelayCommand]
        async Task Refresh()
        {
            IsRefreshing = true;
            try { await LoadAppointments(); }
            finally { IsRefreshing = false; }
        }

        public async Task LoadAppointments()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var weekEnd = WeekStart.AddDays(7);

                // ── Pull from both sources in parallel ───────────────────
                var bookingsTask = _supabaseData.GetBookingsForWeekAsync(
                                            WeekStart, weekEnd);
                var entriesTask = _supabaseData.GetAppointmentEntriesAsync();

                await Task.WhenAll(bookingsTask, entriesTask);

                var bookings = bookingsTask.Result;
                var entries = entriesTask.Result;

                // ── Convert SupabaseBookings → AppointmentEntry ───────────
                // These are pending/rescheduled bookings from the web
                var bookingEntries = bookings.Select(b => new AppointmentEntry
                {
                    SupabaseBookingId = b.Id,
                    PatientName = b.FullName ?? "",
                    Phone = b.Phone ?? "",
                    Email = b.Email ?? "",
                    Service = b.Service ?? "",
                    Notes = b.Notes ?? "",
                    AppointmentDateTime = b.AppointmentDate
                                             .ToLocalTime()
                                             .ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = b.Status  // pending / rescheduled
                }).ToList();

                // ── Convert approved AppointmentEntries ───────────────────
                var approvedEntries = entries
                    .Where(e =>
                    {
                        if (!DateTime.TryParse(
                                e.AppointmentDateTime.ToString(), out var dt))
                            return false;
                        return dt >= WeekStart && dt < weekEnd;
                    })
                    .Select(e => new AppointmentEntry
                    {
                        SupabaseBookingId = e.SupabaseBookingId,
                        PatientName = e.PatientName,
                        Phone = e.Phone ?? "",
                        Email = e.Email ?? "",
                        Service = e.Service ?? "",
                        Notes = e.Notes ?? "",
                        AppointmentDateTime = e.AppointmentDateTime
                                                 .ToLocalTime()
                                                 .ToString("yyyy-MM-dd HH:mm:ss"),
                        Status = e.Status
                    }).ToList();

                // ── Merge — avoid duplicates by SupabaseBookingId ─────────
                // Approved entries take priority over pending booking entries
                var approvedIds = approvedEntries
                    .Select(e => e.SupabaseBookingId)
                    .ToHashSet();

                var pendingOnly = bookingEntries
                    .Where(b => !approvedIds.Contains(b.SupabaseBookingId))
                    .ToList();

                var allEntries = approvedEntries
                    .Concat(pendingOnly)
                    .OrderBy(e => e.AppointmentDateTimeParsed)
                    .ToList();

                // ── Populate Today ────────────────────────────────────────
                var todayEntries = allEntries
                    .Where(e => e.AppointmentDateTimeParsed.Date == DateTime.Today)
                    .ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    TodayAppointments.Clear();
                    foreach (var a in todayEntries)
                        TodayAppointments.Add(a);
                    TodayCount = TodayAppointments.Count;
                });

                // ── Populate Week ─────────────────────────────────────────
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    WeekAppointments.Clear();
                    foreach (var a in allEntries)
                        WeekAppointments.Add(a);
                    WeekCount = WeekAppointments.Count;
                });

                // ── Build calendar ────────────────────────────────────────
                BuildCalendarColumns(allEntries);

                System.Diagnostics.Debug.WriteLine(
                    $"[LoadAppointments] Today={TodayCount} Week={WeekCount}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[LoadAppointments] ERROR: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        private void BuildCalendarColumns(List<AppointmentEntry> entries)
        {
            var hours = new[] { 10, 11, 12, 13, 14, 15, 16 };
            var newColumns = new ObservableCollection<CalendarDayColumn>();

            for (int d = 0; d < 7; d++)
            {
                var day = WeekStart.AddDays(d).Date;
                var dayEntries = entries
                    .Where(a => a.AppointmentDateTimeParsed.Date == day)
                    .ToList();

                var slots = new ObservableCollection<CalendarSlot>(
                    hours.Select(h => new CalendarSlot
                    {
                        Hour = h,
                        Entry = dayEntries.FirstOrDefault(
                            a => a.AppointmentDateTimeParsed.Hour == h)
                    }));

                newColumns.Add(new CalendarDayColumn
                {
                    Date = day,
                    DayLabel = day.ToString("ddd").ToUpper(),
                    DayNum = day.Day.ToString(),
                    IsToday = day == DateTime.Today,
                    Slots = slots
                });
            }

            // Reassign entirely — forces CollectionView to fully re-render
            WeekColumns.Clear();
            foreach (var col in newColumns)
                WeekColumns.Add(col);

            OnPropertyChanged(nameof(WeekColumns));
        }

        [RelayCommand]
        async Task DeleteAppointment()
        {
            if (SelectedAppointment == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Delete appointment",
                $"Permanently delete {SelectedAppointment.PatientName}'s appointment?\n" +
                "This cannot be undone.",
                "Delete", "Cancel");
            if (!confirm) return;

            try
            {
                // 1. Delete from local SQLite
                await _db.DeleteAppointmentEntry(SelectedAppointment);

                // 2. Delete from Supabase appointment_entries
                if (!string.IsNullOrEmpty(_selectedSupabaseEntryId))
                    await _supabaseData.DeleteAppointmentEntryAsync(
                        _selectedSupabaseEntryId);

                // 3. Delete from Supabase bookings
                if (!string.IsNullOrEmpty(SelectedAppointment.SupabaseBookingId))
                    await _supabaseData.DeleteBookingAsync(
                        SelectedAppointment.SupabaseBookingId);

                System.Diagnostics.Debug.WriteLine(
                    $"[DeleteAppointment] Fully deleted booking {SelectedAppointment.SupabaseBookingId}");

                ShowDetail = false;
                SelectedAppointment = null;
                await LoadAppointments();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeleteAppointment] {ex.Message}");
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }

    public class CalendarDayColumn
    {
        public DateTime Date { get; set; }
        public string DayLabel { get; set; } = "";
        public string DayNum { get; set; } = "";
        public bool IsToday { get; set; }
        public ObservableCollection<CalendarSlot> Slots { get; set; } = new();

        // For day header circle color
        public Color CircleBg => IsToday ? Color.FromArgb("#4A4A8A") : Colors.Transparent;
        public Color NumColor => IsToday ? Colors.White : Color.FromArgb("#333333");
    }

    public class CalendarSlot
    {
        public int Hour { get; set; }
        public AppointmentEntry? Entry { get; set; }
        public bool HasEntry => Entry != null;
        public string HourLabel => $"{(Hour > 12 ? Hour - 12 : Hour)} {(Hour >= 12 ? "PM" : "AM")}";
    }
}