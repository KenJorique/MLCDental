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

        private string _selectedSupabaseEntryId = string.Empty;

        public CalendarDrawable CalendarDrawable { get; } = new();
        public event Action? CalendarNeedsRedraw;
        public ObservableCollection<AppointmentEntry> TodayAppointments { get; } = new();
        public ObservableCollection<AppointmentEntry> WeekAppointments { get; } = new();

        // Grouped by specific date — one chronological list, Mon–Sat
        public ObservableCollection<AppointmentDateGroup> GroupedWeekAppointments { get; } = new();
        [ObservableProperty] private bool hasNoWeekAppointments = true;

        public ObservableCollection<CalendarDayColumn> WeekColumns { get; } = new();
        [ObservableProperty] private bool canGoPrevious = true;
        [ObservableProperty] private bool isListView = true;
        [ObservableProperty] private bool isCalendarView;
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private bool isRefreshing;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private bool isInitialLoading = true;

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

        // From File 2 — in-procedure queue badge
        [ObservableProperty] private int inProcedureQueueCount;
        [ObservableProperty] private bool hasInProcedureQueue;

        [ObservableProperty] private string todayLabel = "Today";
        [ObservableProperty] private string weekLabel = "This week";

        AppointmentDetailSheet? _detailSheet;

        // File 1: Complete/Mark button only shows for today's approved appointments
        public bool IsSelectedApproved =>
            SelectedAppointment?.Status == "approved" &&
            SelectedAppointment?.AppointmentDateTimeParsed.Date == DateTime.Today;

        // File 2: In-procedure/billing status check
        public bool IsSelectedInTransit =>
            SelectedAppointment?.Status == "in-procedure" ||
            SelectedAppointment?.Status == "billing";

        public bool IsSelectedPending =>
            SelectedAppointment?.Status == "pending" ||
            SelectedAppointment?.Status == "rescheduled";

        public bool CanCancel =>
            SelectedAppointment?.Status == "approved" ||
            SelectedAppointment?.Status == "pending" ||
            SelectedAppointment?.Status == "rescheduled";

        // Agreed: reschedule allowed for any approved appointment
        public bool CanChangeDate =>
            SelectedAppointment?.Status == "approved";

        partial void OnSelectedAppointmentChanged(AppointmentEntry? value)
        {
            OnPropertyChanged(nameof(IsSelectedApproved));
            OnPropertyChanged(nameof(IsSelectedInTransit));
            OnPropertyChanged(nameof(IsSelectedPending));
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(CanChangeDate));
        }

        [RelayCommand]
        async Task GoToPending()
        {
            await Shell.Current.GoToAsync(nameof(AppointmentPage));
        }

        // From File 2 — navigate to in-procedure queue page
        [RelayCommand]
        async Task GoToInProcedure()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[GoToInProcedure] navigating...");
                await Shell.Current.GoToAsync(nameof(InProcedurePage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GoToInProcedure] {ex}");
                await Shell.Current.DisplayAlert("Nav error", ex.Message, "OK");
            }
        }

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
            await LoadAppointments();
            CalendarNeedsRedraw?.Invoke();
        }

        [RelayCommand]
        async Task PreviousWeek()
        {
            var newDate = CurrentDate.AddDays(-7);
            if (newDate.Date < DateTime.Today.AddDays(-6)) return;
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

        private void UpdateCanGoPrevious()
        {
            CanGoPrevious = WeekStart.Date >= DateTime.Today.AddDays(-6);
        }

        private void UpdateListLabels()
        {
            var weekStartDate = WeekStart.Date;
            var today = DateTime.Today.Date;

            if (weekStartDate == today.AddDays(-(int)today.DayOfWeek).Date)
            {
                TodayLabel = DateTime.Today.ToString("dddd, MMMM d");
                WeekLabel = "This week";
            }
            else if (weekStartDate > today)
            {
                TodayLabel = weekStartDate.ToString("dddd");
                WeekLabel = "Week of " + weekStartDate.ToString("MMMM d");
            }
            else
            {
                TodayLabel = weekStartDate.ToString("dddd");
                WeekLabel = "Week of " + weekStartDate.ToString("MMMM d");
            }
        }

        // Single unified select — used by list, calendar tap, and today tap
        [RelayCommand]
        async Task SelectAppointment(AppointmentEntry entry)
        {
            if (entry == null) return;

            SelectedAppointment = entry;
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

        // Aliases for calendar and today tap handlers in code-behind
        [RelayCommand]
        async Task SelectWeekAppointment(AppointmentEntry entry) =>
            await SelectAppointmentCommand.ExecuteAsync(entry);

        [RelayCommand]
        async Task SelectTodayAppointment(AppointmentEntry entry) =>
            await SelectAppointmentCommand.ExecuteAsync(entry);

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

        // From File 2 — shared helper for updating appointment stage
        private async Task UpdateAppointmentStageAsync(string status)
        {
            if (SelectedAppointment == null) return;

            try
            {
                if (SelectedAppointment.Id > 0)
                    await _db.UpdateAppointmentStatus(SelectedAppointment.Id, status);

                if (!string.IsNullOrWhiteSpace(_selectedSupabaseEntryId))
                    await _supabaseData.UpdateAppointmentEntryStatusAsync(
                        _selectedSupabaseEntryId, status);

                if (!string.IsNullOrWhiteSpace(SelectedAppointment.SupabaseBookingId))
                    await _supabaseData.UpdateBookingStatusAsync(
                        SelectedAppointment.SupabaseBookingId, status);

                SelectedAppointment.Status = status;
                await LoadAppointments();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateAppointmentStage] {ex.Message}");
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // From File 2 — moves patient to in-procedure queue
        [RelayCommand]
        async Task SetInTransit()
        {
            if (SelectedAppointment == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Set In Transit",
                $"Mark {SelectedAppointment.PatientName} as currently in procedure?",
                "Yes", "Cancel");

            if (!confirm) return;

            ShowDetail = false;
            SelectedAppointment = null;
            await CloseSheetAsync();

            await UpdateAppointmentStageAsync("in-procedure");
        }

        // From File 2 — called from InProcedurePage to go to billing
        [RelayCommand]
        async Task ProceedToBilling()
        {
            if (SelectedAppointment == null) return;

            System.Diagnostics.Debug.WriteLine(
                $"[ProceedToBilling] PatientName='{SelectedAppointment.PatientName}' " +
                $"PatientSupabaseId='{SelectedAppointment.PatientSupabaseId}' " +
                $"Status='{SelectedAppointment.Status}'");

            bool confirm = await Shell.Current.DisplayAlert(
                "Start Billing",
                $"Procedure for {SelectedAppointment.PatientName} is done.\nStart billing now?",
                "Yes, proceed", "Cancel");

            if (!confirm) return;

            var appointment = SelectedAppointment;
            var supabaseEntryId = _selectedSupabaseEntryId;

            try
            {
                ShowDetail = false;
                SelectedAppointment = null;
                await CloseSheetAsync();

                await Shell.Current.GoToAsync(
                    $"{nameof(CreateBillPage)}" +
                    $"?patientId={Uri.EscapeDataString(appointment.PatientSupabaseId ?? string.Empty)}" +
                    $"&patientName={Uri.EscapeDataString(appointment.PatientName ?? string.Empty)}" +
                    $"&appointmentEntryId={Uri.EscapeDataString(supabaseEntryId ?? string.Empty)}" +
                    $"&supabaseEntryId={Uri.EscapeDataString(supabaseEntryId ?? string.Empty)}" +
                    $"&supabaseBookingId={Uri.EscapeDataString(appointment.SupabaseBookingId ?? string.Empty)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProceedToBilling] {ex.Message}");
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
                await _db.UpdateAppointmentStatus(SelectedAppointment.Id, "cancelled");

                if (!string.IsNullOrEmpty(_selectedSupabaseEntryId))
                    await _supabaseData.DeleteAppointmentEntryAsync(_selectedSupabaseEntryId);

                if (!string.IsNullOrEmpty(SelectedAppointment.SupabaseBookingId))
                    await _supabaseData.DeleteBookingAsync(SelectedAppointment.SupabaseBookingId);

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

            var currentDt = SelectedAppointment.AppointmentDateTimeParsed != DateTime.MinValue
                ? SelectedAppointment.AppointmentDateTimeParsed.ToString("MMM dd, yyyy h:mm tt")
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

        public async Task LoadAppointments()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var entries = await _supabaseData.GetAppointmentEntriesAsync();

                // From File 2 — track in-procedure/billing queue count for banner
                InProcedureQueueCount = entries.Count(e =>
                    string.Equals(e.Status, "in-procedure", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.Status, "billing", StringComparison.OrdinalIgnoreCase));
                HasInProcedureQueue = InProcedureQueueCount > 0;

                // Schedule shows APPROVED only
                var approvedEntries = entries
                    .Where(e => string.Equals(e.Status, "approved", StringComparison.OrdinalIgnoreCase))
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

                // Round to nearest hour
                foreach (var entry in allEntries)
                {
                    var dt = entry.AppointmentDateTimeParsed;
                    if (dt != DateTime.MinValue)
                    {
                        var rounded = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0);
                        entry.AppointmentDateTime = rounded.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                }

                var todayEntries = allEntries
                    .Where(e => e.AppointmentDateTimeParsed.Date == DateTime.Today)
                    .ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    TodayAppointments.Clear();
                    foreach (var a in todayEntries) TodayAppointments.Add(a);
                    TodayCount = TodayAppointments.Count;
                });

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    WeekAppointments.Clear();
                    foreach (var a in allEntries) WeekAppointments.Add(a);
                    WeekCount = WeekAppointments.Count;
                });

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    GroupedWeekAppointments.Clear();

                    bool isCurrentWeek = WeekStart.Date ==
                        DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek).Date;

                    for (int d = 0; d < 7; d++)
                    {
                        var day = WeekStart.AddDays(d).Date;
                        if (day.DayOfWeek == DayOfWeek.Sunday) continue;
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
                await _db.DeleteAppointmentEntry(SelectedAppointment);

                if (!string.IsNullOrEmpty(_selectedSupabaseEntryId))
                    await _supabaseData.DeleteAppointmentEntryAsync(_selectedSupabaseEntryId);

                if (!string.IsNullOrEmpty(SelectedAppointment.SupabaseBookingId))
                    await _supabaseData.DeleteBookingAsync(SelectedAppointment.SupabaseBookingId);

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
                    PhoneDialer.Default.Open(phoneNumber);
                else
                    await Shell.Current.DisplayAlert("Not Supported", "Phone dialing is not supported on this device.", "OK");
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
