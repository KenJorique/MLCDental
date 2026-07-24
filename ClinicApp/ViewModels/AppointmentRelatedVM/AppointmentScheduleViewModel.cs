
using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ClinicApp.Views.AppointmentRelated;
using ClinicApp.Views;

namespace ClinicApp.ViewModels
{
    public partial class AppointmentScheduleViewModel : ObservableObject
    {
        readonly DatabaseService _db;
        readonly SupabaseDataService _supabaseData;

        // Add this field to track the Supabase appointment_entries UUID
        private string _selectedSupabaseEntryId = string.Empty;

        // Calendar drawable — GraphicsView renders this
        public CalendarDrawable CalendarDrawable { get; } = new();
        public event Action? CalendarNeedsRedraw;
        public ObservableCollection<AppointmentEntry> TodayAppointments { get; } = new();
        public ObservableCollection<AppointmentEntry> WeekAppointments { get; } = new();

        // Grouped by specific date (excludes today, which has its own section above).
        // Max 6 groups — the other days of the Sun–Sat week.
        public ObservableCollection<AppointmentDateGroup> GroupedWeekAppointments { get; } = new();
        [ObservableProperty] private bool hasNoWeekAppointments = true;

        // Calendar grid — 7 days x time slots
        public ObservableCollection<CalendarDayColumn> WeekColumns { get; } = new();
        [ObservableProperty] private bool canGoPrevious = true;
        [ObservableProperty] private bool isListView = true;
        [ObservableProperty] private bool isCalendarView;
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private bool isRefreshing;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private bool isInitialLoading = true;

        /// <summary>List content (including its "no appointments" text) is hidden only during the
        /// very first load — not on every refresh/week-nav, so the list doesn't disappear and
        /// get replaced by the big spinner on every quick tap.</summary>
        public bool ShowListContent => IsListView && !IsInitialLoading;

        partial void OnIsInitialLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowListContent));
        partial void OnIsListViewChanged(bool value) => OnPropertyChanged(nameof(ShowListContent));
        [ObservableProperty] private DateTime currentDate = DateTime.Today;
        [ObservableProperty] private string dateRangeLabel = string.Empty;
        [ObservableProperty] private AppointmentEntry? selectedAppointment;
        [ObservableProperty] private bool showDetail;
        [ObservableProperty] private int todayCount;
        [ObservableProperty] private int weekCount;
        [ObservableProperty] private int pendingBookingsCount;
        [ObservableProperty] private bool hasPendingBookings;
        [ObservableProperty] private string todayLabel = "Today";
        [ObservableProperty]  private string weekLabel = "This week";
        [ObservableProperty] private bool selectedFromWeekSection;
        [ObservableProperty] private string weekLabel = "This week";

        AppointmentDetailSheet? _detailSheet;
        // Add these properties
        partial void OnSelectedFromWeekSectionChanged(bool value)
        {
            OnPropertyChanged(nameof(CanChangeDate));
        }
        public bool IsSelectedApproved =>
            SelectedAppointment?.Status == "approved" &&
            SelectedAppointment?.AppointmentDateTimeParsed.Date == DateTime.Today; 

        public bool IsSelectedPending =>
            SelectedAppointment?.Status == "pending" ||
            SelectedAppointment?.Status == "rescheduled";

        public bool CanCancel =>
            SelectedAppointment?.Status == "approved" ||
            SelectedAppointment?.Status == "pending" ||
            SelectedAppointment?.Status == "rescheduled";
        public bool CanChangeDate =>
            SelectedAppointment?.Status == "approved" && SelectedFromWeekSection;
        partial void OnSelectedAppointmentChanged(AppointmentEntry? value)
        {
            OnPropertyChanged(nameof(IsSelectedApproved));
            OnPropertyChanged(nameof(IsSelectedPending));
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(CanChangeDate));
        }

        [RelayCommand]
        async Task GoToPending()
        {
            await Shell.Current.GoToAsync(nameof(AppointmentPage));
        }
        // always start from Sunday of the CURRENT week
        public DateTime WeekStart
        {
            get
            {
                var diff = (7 + (CurrentDate.DayOfWeek - DayOfWeek.Sunday)) % 7;
                return CurrentDate.AddDays(-diff).Date;
            }
        }

        [RelayCommand]
        async Task GoToWalkIn()
        {
            await Shell.Current.GoToAsync(nameof(WalkInBookingPage));
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
            UpdateListLabels();
        }


        [RelayCommand]
        void ShowList()
        {
            IsListView = true;
            IsCalendarView = false;
        }

        [RelayCommand]
        async void ShowCalendar()
        {
            IsListView = false;
            IsCalendarView = true;

            // Force load and redraw
            await LoadAppointments();
            CalendarNeedsRedraw?.Invoke();
        }

        [RelayCommand]
        async Task PreviousWeek()
        {
            var newDate = CurrentDate.AddDays(-7);

            // Prevent going before current week
            if (newDate.Date < DateTime.Today.AddDays(-6)) // Allow current week only
            {
                return;
            }

            CurrentDate = newDate;
            UpdateDateLabel();
            await LoadAppointments();
            CalendarNeedsRedraw?.Invoke();
        }

        [RelayCommand]
        async Task NextWeek()
        {
            CurrentDate = CurrentDate.AddDays(7);
            UpdateDateLabel();
            UpdateCanGoPrevious();
            await LoadAppointments();
            CalendarNeedsRedraw?.Invoke();
        }

        [RelayCommand]
        async Task GoToToday()
        {
            CurrentDate = DateTime.Today;
            UpdateDateLabel();
            UpdateCanGoPrevious();
            await LoadAppointments();
            CalendarNeedsRedraw?.Invoke();
        }

        // Add this method
        private void UpdateCanGoPrevious()
        {
            CanGoPrevious = WeekStart.Date >= DateTime.Today.AddDays(-6); // Current week or future
        }

        private void UpdateListLabels()
        {
            var weekStartDate = WeekStart.Date;
            var today = DateTime.Today.Date;

            if (weekStartDate == today.AddDays(-(int)today.DayOfWeek).Date) // Current week
            {
                TodayLabel = DateTime.Today.ToString("dddd, MMMM d");
                WeekLabel = "This week";
            }
            else if (weekStartDate > today) // Future week
            {
                TodayLabel = weekStartDate.ToString("dddd");
                WeekLabel = "Week of " + weekStartDate.ToString("MMMM d");
            }
            else // Past week
            {
                TodayLabel = weekStartDate.ToString("dddd");
                WeekLabel = "Week of " + weekStartDate.ToString("MMMM d");
            }
        }

        [RelayCommand]
        async Task SelectTodayAppointment(AppointmentEntry entry) =>
    await SelectAppointmentCore(entry, fromWeek: false);

        [RelayCommand]
        async Task SelectWeekAppointment(AppointmentEntry entry) =>
            await SelectAppointmentCore(entry, fromWeek: true);

        private async Task SelectAppointmentCore(AppointmentEntry entry, bool fromWeek)
        {
            if (entry == null) return;

            SelectedAppointment = entry;
            SelectedFromWeekSection = fromWeek;
            ShowDetail = true;

            _detailSheet = new AppointmentDetailSheet { BindingContext = this };
            _ = _detailSheet.ShowAsync();

            try
            {
                var all = await _supabaseData.GetAppointmentEntriesAsync();
                var match = all.FirstOrDefault(
                    a => a.SupabaseBookingId == entry.SupabaseBookingId);
                _selectedSupabaseEntryId = match?.Id ?? string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SelectAppointment] {ex.Message}");
            }
        }

        [RelayCommand]
        async Task CloseDetail()
        {
            ShowDetail = false;
            SelectedAppointment = null;
            await CloseSheetAsync();
        }

        async Task CloseSheetAsync()
        {
            if (_detailSheet == null) return;
            var sheet = _detailSheet;
            _detailSheet = null;
            try { await sheet.DismissAsync(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CloseSheetAsync] {ex.Message}");
            }
        }

        [RelayCommand]
        async Task MarkCompleted()
        {
            if (SelectedAppointment == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Mark as completed",
                $"Mark {SelectedAppointment.PatientName}'s appointment as completed?\n" +
                "You will be redirected to create a bill.",
                "Yes, proceed", "Cancel");
            if (!confirm) return;

            var appointment = SelectedAppointment;
            var supabaseEntryId = _selectedSupabaseEntryId;

            try
            {
                // Google Tasks complete
                try
                {
                    var token = await _supabaseData.GetFreshAccessTokenAsync();
                    if (!string.IsNullOrEmpty(token) &&
                        !string.IsNullOrEmpty(appointment.GoogleTaskId))
                        await _supabaseData.CompleteGoogleTaskAsync(
                            token, appointment.GoogleTaskId);
                }
                catch { /* silent */ }

                // Remove from both lists immediately — the booking is "done"
                // from the user's perspective the moment they confirm, even
                // though actual deletion happens later inside CreateBillViewModel.
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var todayMatch = TodayAppointments.FirstOrDefault(
                        a => a.SupabaseBookingId == appointment.SupabaseBookingId);
                    if (todayMatch != null)
                    {
                        TodayAppointments.Remove(todayMatch);
                        TodayCount = TodayAppointments.Count;
                    }

                    var weekMatch = WeekAppointments.FirstOrDefault(
                        a => a.SupabaseBookingId == appointment.SupabaseBookingId);
                    if (weekMatch != null)
                    {
                        WeekAppointments.Remove(weekMatch);
                        WeekCount = WeekAppointments.Count;
                    }
                });

                // Rebuild calendar so the slot clears too
                BuildCalendarColumns(WeekAppointments.ToList());

                ShowDetail = false;
                SelectedAppointment = null;
                await CloseSheetAsync();

                // Navigate to billing — pass appointment entry id
                // Deletion happens INSIDE CreateBillViewModel.CreateBill()
                // so going back doesn't lose the appointment
                await Shell.Current.GoToAsync(
     $"{nameof(CreateBillPage)}" +
     $"?patientId={Uri.EscapeDataString(appointment.PatientSupabaseId ?? string.Empty)}" +
     $"&patientName={Uri.EscapeDataString(appointment.PatientName ?? string.Empty)}" +
     $"&appointmentEntryId={Uri.EscapeDataString(supabaseEntryId ?? string.Empty)}" +
     $"&supabaseEntryId={Uri.EscapeDataString(supabaseEntryId ?? string.Empty)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MarkCompleted] {ex.Message}");
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
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
                await CloseSheetAsync();
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

            ShowDetail = false;
            await CloseSheetAsync();

            var currentDt = SelectedAppointment.AppointmentDateTimeParsed
                != DateTime.MinValue
                ? SelectedAppointment.AppointmentDateTimeParsed
                      .ToString("MMM dd, yyyy h:mm tt")
                : "Unknown";

            await Shell.Current.GoToAsync(
                $"{nameof(ReschedulePage)}" +
                $"?bookingId={Uri.EscapeDataString(SelectedAppointment.SupabaseBookingId)}" +
                $"&patientName={Uri.EscapeDataString(SelectedAppointment.PatientName)}" +
                $"&currentDateTime={Uri.EscapeDataString(currentDt)}");
        }


        [RelayCommand]
        async Task Refresh()
        {
            IsRefreshing = true;
            try { await LoadAppointments(); }
            finally { IsRefreshing = false; }
        }

        [RelayCommand]
        void RefreshCalendar()
        {
            CalendarNeedsRedraw?.Invoke();
        }

        private async Task FetchAndPopulate()
        {
            try
            {
                var weekEnd = WeekStart.AddDays(7);

                var bookingsTask = _supabaseData.GetBookingsForWeekAsync(
                                       WeekStart, weekEnd);
                var entriesTask = _supabaseData.GetAppointmentEntriesAsync();

                await Task.WhenAll(bookingsTask, entriesTask);

                var bookings = bookingsTask.Result;
                var entries = entriesTask.Result;

                // ── Convert bookings → AppointmentEntry ──────────────
                var bookingEntries = bookings.Select(b =>
                {
                    // Force UTC kind then convert ONCE to local
                    var utc = DateTime.SpecifyKind(
                                    b.AppointmentDate, DateTimeKind.Utc);
                    var local = utc.ToLocalTime();

                    System.Diagnostics.Debug.WriteLine(
                        $"[Time] {b.FullName}: UTC={utc:HH:mm} Local={local:HH:mm}");

                    return new AppointmentEntry
                    {
                        SupabaseBookingId = b.Id,
                        PatientName = b.FullName ?? "",
                        Phone = b.Phone ?? "",
                        Email = b.Email ?? "",
                        Notes = b.Notes ?? "",
                        // Store as LOCAL time string with no timezone info
                        // AppointmentDateTimeParsed treats unspecified as local
                        AppointmentDateTime = local.ToString("yyyy-MM-dd HH:mm:ss"),
                        Status = b.Status
                    };
                }).ToList();

                // ── Convert approved entries ──────────────────────────
                var approvedEntries = entries
                    .Select(e =>
                    {
                        // e.AppointmentDateTime is UTC from Supabase
                        var utc = DateTime.SpecifyKind(
                                        e.AppointmentDateTime, DateTimeKind.Utc);
                        var local = utc.ToLocalTime();

                        return new AppointmentEntry
                        {
                            SupabaseBookingId = e.SupabaseBookingId,
                            PatientName = e.PatientName,
                            PatientSupabaseId = e.PatientId,
                            Phone = e.Phone ?? "",
                            Email = e.Email ?? "",
                            Notes = e.Notes ?? "",
                            AppointmentDateTime = local.ToString("yyyy-MM-dd HH:mm:ss"),
                            Status = e.Status,
                            GoogleTaskId = e.GoogleTaskId ?? ""
                        };
                    })
                    .Where(e =>
                    {
                        // Filter by week AFTER conversion to local
                        return e.AppointmentDateTimeParsed.Date >= WeekStart.Date &&
                               e.AppointmentDateTimeParsed.Date < WeekStart.AddDays(7).Date;
                    })
                    .ToList();

                // ── Merge — approved takes priority over pending ──────
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

                // ── Today ─────────────────────────────────────────────
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

                // ── Week ──────────────────────────────────────────────
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    WeekAppointments.Clear();
                    foreach (var a in allEntries)
                        WeekAppointments.Add(a);
                    WeekCount = WeekAppointments.Count;
                });

                // ── Pending count for banner ──────────────────────────
                var pending = await _supabaseData.GetBookingsByStatusAsync("pending");
                PendingBookingsCount = pending.Count;
                HasPendingBookings = PendingBookingsCount > 0;
                OnPropertyChanged(nameof(HasPendingBookings));
                OnPropertyChanged(nameof(PendingBookingsCount));

                // ── Calendar ──────────────────────────────────────────
                BuildCalendarColumns(allEntries);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FetchAndPopulate] ERROR: {ex.Message}");
            }
        }
        public async Task LoadAppointments()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var entries = await _supabaseData.GetAppointmentEntriesAsync();

                // Schedule page shows APPROVED appointments only.
                // Pending / rescheduled bookings live exclusively in the
                // review list (AppointmentPage) — they never appear here.
                var approvedEntries = entries
                    .Where(e => e.Status == "approved")
                    .Where(e =>
                    {
                        var dt = e.AppointmentDateTime.Kind == DateTimeKind.Utc
                            ? e.AppointmentDateTime.ToLocalTime()
                            : e.AppointmentDateTime;
                        return dt.Date >= WeekStart.Date &&
                               dt.Date < WeekStart.AddDays(7).Date;
                    })
                    .Select(e =>
                    {
                        var localDt = e.AppointmentDateTime.Kind == DateTimeKind.Utc
                            ? e.AppointmentDateTime.ToLocalTime()
                            : e.AppointmentDateTime;

                        return new AppointmentEntry
                        {
                            SupabaseBookingId = e.SupabaseBookingId,
                            PatientName = e.PatientName,
                            PatientSupabaseId = e.PatientId,
                            Phone = e.Phone ?? "",
                            Email = e.Email ?? "",
                            Notes = e.Notes ?? "",
                            AppointmentDateTime = localDt.ToString("yyyy-MM-dd HH:mm:ss"),
                            Status = e.Status,
                            GoogleTaskId = e.GoogleTaskId ?? ""
                        };
                    }).ToList();

                var allEntries = approvedEntries
                    .OrderBy(e => e.AppointmentDateTimeParsed)
                    .ToList();

                // ── Force round to nearest hour (no :30) ─────────────────
                foreach (var entry in allEntries)
                {
                    var dt = entry.AppointmentDateTimeParsed;
                    if (dt != DateTime.MinValue)
                    {
                        var rounded = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0);
                        entry.AppointmentDateTime = rounded.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                }

                // ── Populate Today (kept for backward-compat; no longer drives its own UI section) ──
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

                // ── Populate Week (kept as-is; no longer bound in the list UI,
                //    left in case anything else reads WeekAppointments/WeekCount) ──
                // ── Populate Week (full week list, same as before)
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    WeekAppointments.Clear();
                    foreach (var a in allEntries)
                        WeekAppointments.Add(a);
                    WeekCount = WeekAppointments.Count;
                });

                // ── Populate GroupedWeekAppointments — ONE chronological list, Mon–Sat.
                //    Today gets a "Today, <date>" header instead of the weekday name, and always
                //    shows (even with 0 appointments) — but ONLY when the week being viewed is the
                //    current week, since "day == DateTime.Today" can only be true for a day that's
                //    actually inside WeekStart..WeekStart+6. Other days are skipped when empty. ──
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    GroupedWeekAppointments.Clear();

                    bool isCurrentWeek = WeekStart.Date ==
                        DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek).Date;

                    for (int d = 0; d < 7; d++)
                    {
                        var day = WeekStart.AddDays(d).Date;
                        if (day.DayOfWeek == DayOfWeek.Sunday) continue; // Clinic closed Sundays

                        // Only when viewing the CURRENT week: hide days already passed, so
                        // Today is always first. Intentionally-browsed past weeks still show fully.
                        if (isCurrentWeek && day < DateTime.Today.Date) continue;

                        bool isToday = day == DateTime.Today;

                        var dayEntries = allEntries
                            .Where(e => e.AppointmentDateTimeParsed.Date == day)
                            .ToList();
                        if (dayEntries.Count == 0 && !isToday) continue;

                        GroupedWeekAppointments.Add(new AppointmentDateGroup
                        {
                            Header = isToday ? $"Today, {day:MMMM d}" : day.ToString("dddd, MMMM d"),
                            Items = dayEntries,
                            IsToday = isToday
                        });
                    }
                    HasNoWeekAppointments = GroupedWeekAppointments.Count == 0;
                });

                // ── Build calendar
                BuildCalendarColumns(allEntries);
                UpdateListLabels();

                System.Diagnostics.Debug.WriteLine(
                    $"[LoadAppointments] Today={TodayCount} Week={WeekCount}");

                var pending = await _supabaseData.GetBookingsByStatusAsync("pending");
                PendingBookingsCount = pending.Count;
                HasPendingBookings = PendingBookingsCount > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadAppointments] ERROR: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                IsInitialLoading = false;
            }
        }

        private void BuildCalendarColumns(List<AppointmentEntry> entries)
        {
            var hours = new[] { 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            var newColumns = new List<CalendarDayColumn>();

            for (int d = 0; d < 7; d++)
            {
                var day = WeekStart.AddDays(d).Date;
                var dayEntries = entries.Where(a => a.AppointmentDateTimeParsed.Date == day).ToList();

                var slots = new ObservableCollection<CalendarSlot>();

                foreach (var h in hours)
                {
                    var matching = dayEntries.FirstOrDefault(a => a.AppointmentDateTimeParsed.Hour == h);
                    slots.Add(new CalendarSlot { Hour = h, Entry = matching });
                }

                newColumns.Add(new CalendarDayColumn
                {
                    Date = day,
                    DayLabel = day.ToString("ddd").ToUpper(),
                    DayNum = day.Day.ToString(),
                    IsToday = day == DateTime.Today,
                    Slots = slots
                });
            }

            WeekColumns.Clear();
            foreach (var col in newColumns) WeekColumns.Add(col);

            CalendarDrawable.Columns = newColumns;
            OnPropertyChanged(nameof(CalendarDrawable));
            CalendarNeedsRedraw?.Invoke();
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
                await CloseSheetAsync();
                await LoadAppointments();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeleteAppointment] {ex.Message}");
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        [RelayCommand]
        async Task CallPatient(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                await Shell.Current.DisplayAlert("Error", "No phone number available for this patient.", "OK");
                return;
            }

            try
            {
                if (PhoneDialer.Default.IsSupported)
                {
                    PhoneDialer.Default.Open(phoneNumber);
                }
                else
                {
                    await Shell.Current.DisplayAlert("Not Supported", "Phone dialing is not supported on this device.", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CallPatient] Error: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "Unable to open phone dialer.", "OK");
            }
        }

        [RelayCommand]
        async Task EmailPatient(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                await Shell.Current.DisplayAlert("Error", "No email address available for this patient.", "OK");
                return;
            }

            try
            {
                var message = new EmailMessage { To = new List<string> { email } };
                await Email.Default.ComposeAsync(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailPatient] Error: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "Unable to open email app.", "OK");
            }
        }

    }

    public class AppointmentDateGroup
    {
        public string Header { get; set; } = "";
        public List<AppointmentEntry> Items { get; set; } = new();
        public bool IsToday { get; set; }
        public bool IsEmptyToday => IsToday && Items.Count == 0;
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