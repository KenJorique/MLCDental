using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ClinicApp.Views.AppointmentRelated;

namespace ClinicApp.ViewModels
{
    public partial class AppointmentViewModel : ObservableObject
    {
        readonly DatabaseService _db;
        readonly SupabaseDataService _supabaseData;

        public ObservableCollection<SupabaseBooking> PendingBookings { get; set; } = new();
        public ObservableCollection<SupabaseBooking> ApprovedBookings { get; set; } = new();
        public ObservableCollection<SupabaseBooking> RescheduledBookings { get; set; } = new();

        // Separate busy flags — IsRefreshing for pull-to-refresh, IsLoading for internal ops
        [ObservableProperty] private bool isRefreshing;
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private int pendingCount;
        [ObservableProperty] private int approvedCount;
        [ObservableProperty] private int rescheduledCount;

        // Capital H — matches XAML binding exactly
        public bool HasPending => PendingCount > 0;
        public bool HasApproved => ApprovedCount > 0;
        public bool HasRescheduled => RescheduledCount > 0;

        public AppointmentViewModel(DatabaseService db, SupabaseDataService supabaseData)
        {
            _db = db;
            _supabaseData = supabaseData;
        }

        // Called from OnAppearing — not triggered by RefreshView
        public async Task LoadAppointments()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                await FetchAndPopulate();
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Called by RefreshView pull-to-refresh
        [RelayCommand]
        async Task Refresh()
        {
            IsRefreshing = true;
            try
            {
                await FetchAndPopulate();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        // Core fetch logic shared by both
        private async Task FetchAndPopulate()
        {

            try
            {
                var pendingTask = _supabaseData.GetBookingsByStatusAsync("pending");
                var approvedTask = _supabaseData.GetBookingsByStatusAsync("approved");
                var rescheduledTask = _supabaseData.GetBookingsByStatusAsync("rescheduled");

                await Task.WhenAll(pendingTask, approvedTask, rescheduledTask);

                PendingBookings.Clear();
                foreach (var b in pendingTask.Result)
                    PendingBookings.Add(b);

                ApprovedBookings.Clear();
                foreach (var b in approvedTask.Result)
                    ApprovedBookings.Add(b);

                RescheduledBookings.Clear();
                foreach (var b in rescheduledTask.Result)
                    RescheduledBookings.Add(b);

                PendingCount = PendingBookings.Count;
                ApprovedCount = ApprovedBookings.Count;
                RescheduledCount = RescheduledBookings.Count;

                OnPropertyChanged(nameof(HasPending));
                OnPropertyChanged(nameof(HasApproved));
                OnPropertyChanged(nameof(HasRescheduled));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FetchAndPopulate] {ex.Message}");
            }

        }
        [RelayCommand]
        async Task Approve(SupabaseBooking booking)
        {
            if (booking == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Approve Booking",
                $"Approve booking for {booking.FullName}",
                "Approve", "Cancel");

            if (!confirm) return;

            IsLoading = true;
            try
            {
                // Only create new patient if not existing
                // Replace the existing patient check section with this:

                // Always check by phone first — prevents duplicates regardless of flag
                bool patientExists = false;

                if (!string.IsNullOrEmpty(booking.Phone))
                {
                    var existingPatients = await _supabaseData
                        .GetPatientByPhoneAsync(booking.Phone);
                    patientExists = existingPatients != null;

                    System.Diagnostics.Debug.WriteLine(
                        $"[Approve] Patient exists check: {patientExists} " +
                        $"for phone {booking.Phone}");
                }

                if (!patientExists)
                {
                    // Create new patient — only if truly doesn't exist
                    var parts = (booking.FullName ?? "").Trim().Split(' ', 2);
                    var patient = new Patient
                    {
                        FirstName = parts.Length > 0 ? parts[0] : "",
                        LastName = parts.Length > 1 ? parts[1] : "",
                        MobileNo = booking.Phone ?? "",
                        Email = booking.Email ?? "",
                        ReferredBy = "Online Booking",
                        DateRegistered = DateTime.Now.ToString("yyyy-MM-dd")
                    };
                    await _db.AddPatient(patient);

                    var supPatient = new SupabasePatient
                    {
                        FirstName = patient.FirstName,
                        LastName = patient.LastName,
                        Phone = patient.MobileNo,
                        Email = patient.Email,
                        ReasonForConsultation = patient.ReasonForConsultation,
                        ReferredBy = "Online Booking",
                        DateRegistered = DateTime.UtcNow
                    };
                    await _supabaseData.AddPatientAsync(supPatient);

                    System.Diagnostics.Debug.WriteLine(
                        $"[Approve] New patient created: {patient.FirstName}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Approve] Patient already exists — skipping creation");
                }

                // Rest of approve flow stays the same...
                // 1. Treat the booking's appointment date as Local time (Philippine Time)
                var localDate = booking.AppointmentDate.Kind == DateTimeKind.Utc
                    ? booking.AppointmentDate.ToLocalTime()
                    : DateTime.SpecifyKind(booking.AppointmentDate, DateTimeKind.Local);

                // 2. Derive the true UTC equivalent for Supabase storage (subtracts 8 hours)
                var utcDate = localDate.ToUniversalTime();

                var localEntry = new AppointmentEntry
                {
                    SupabaseBookingId = booking.Id,
                    PatientName = booking.FullName ?? "",
                    Phone = booking.Phone ?? "",
                    Email = booking.Email ?? "",
                    Notes = booking.Notes ?? "",
                    AppointmentDateTime = localDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = "approved"
                };
                await _db.AddAppointmentEntry(localEntry);

                var supEntry = new SupabaseAppointmentEntry
                {
                    SupabaseBookingId = booking.Id,
                    PatientName = booking.FullName ?? "",
                    Phone = booking.Phone ?? "",
                    Email = booking.Email ?? "",
                    Notes = booking.Notes ?? "",
                    AppointmentDateTime = utcDate,
                    Status = "approved"
                };
                await _supabaseData.AddAppointmentEntryAsync(supEntry);

                await _supabaseData.UpdateBookingStatusAsync(booking.Id, "approved");

                // Google Tasks
                try
                {
                    var taskId = await _supabaseData.SyncToGoogleTasksAsync(
                        "",
                        booking.FullName ?? "",
                        " ",
                        booking.AppointmentDate,
                        booking.Phone ?? "",
                        booking.Notes ?? "");

                    System.Diagnostics.Debug.WriteLine(
                        $"[Approve] Task: {taskId ?? "null"}");
                }
                catch (Exception gEx)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Approve] Google: {gEx.Message}");
                }

                await Shell.Current.DisplayAlert("Approved",
                    booking.IsExistingPatient
                        ? $"{booking.FullName}'s appointment approved. (Existing patient)"
                        : $"{booking.FullName} added to patient list and approved.",
                    "OK");

                await FetchAndPopulate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Approve] {ex.Message}");
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
            finally { IsLoading = false; }
        }

        // Helper to get week start for date lookup
        private DateTime WeekStart(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Sunday)) % 7;
            return date.AddDays(-diff).Date;
        }

        [RelayCommand]
        async Task Reschedule(SupabaseBooking booking)
        {
            if (booking == null)
            {
                System.Diagnostics.Debug.WriteLine("[Reschedule] booking is null");
                return;
            }

            // 1. (Optional) Remove the status update alert if you want it to navigate instantly,
            // or keep it if you want them to confirm they are changing it right now.
            var currentDt = booking.AppointmentDate != DateTime.MinValue
                ? booking.AppointmentDate.ToString("MMM dd, yyyy h:mm tt")
                : "Unknown";

            // 2. Navigate straight to the ReschedulePage, passing the required query parameters
            await Shell.Current.GoToAsync(
                $"{nameof(ReschedulePage)}" +
                $"?bookingId={Uri.EscapeDataString(booking.Id)}" +
                $"&patientName={Uri.EscapeDataString(booking.FullName ?? "")}" +
                $"&currentDateTime={Uri.EscapeDataString(currentDt)}");
        }

        [RelayCommand]
        async Task MoveToPending(SupabaseBooking booking)
        {
            if (booking == null)
            {
                System.Diagnostics.Debug.WriteLine("[MoveToPending] booking is null");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[MoveToPending] Starting for {booking.FullName}, Id={booking.Id}");

            IsLoading = true;
            try
            {
                await _supabaseData.UpdateBookingStatusAsync(booking.Id, "pending");
                System.Diagnostics.Debug.WriteLine($"[MoveToPending] Done.");
                await FetchAndPopulate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MoveToPending] FAILED: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
            }
            finally { IsLoading = false; }
        }

        // Cancel a pending booking
        [RelayCommand]
        async Task CancelBooking(SupabaseBooking booking)
        {
            if (booking == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Cancel Booking",
                $"Cancel {booking.FullName}'s booking?\nThis cannot be undone.",
                "Yes, cancel", "Keep");

            if (!confirm) return;

            IsLoading = true;
            try
            {
                await _supabaseData.UpdateBookingStatusAsync(booking.Id, "cancelled");
                await FetchAndPopulate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CancelBooking] {ex.Message}");
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        async Task MarkComplete(SupabaseBooking booking)
        {
            if (booking == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Mark as Complete",
                $"Mark {booking.FullName}'s appointment as completed?\n" +
                "It will be removed from the appointment list.",
                "Yes", "Cancel");

            if (!confirm) return;

            IsLoading = true;
            try
            {
                // 1. Get the appointment entry before deleting
                var entries = await _supabaseData.GetAppointmentEntriesAsync();
                var entry = entries.FirstOrDefault(
                    e => e.SupabaseBookingId == booking.Id);

                // 2. Complete Google Task if exists
                try
                {
                    var accessToken = await _supabaseData.GetFreshAccessTokenAsync();
                    if (!string.IsNullOrEmpty(accessToken)
                        && entry != null
                        && !string.IsNullOrEmpty(entry.GoogleTaskId))
                    {
                        await _supabaseData.CompleteGoogleTaskAsync(
                            accessToken, entry.GoogleTaskId);
                    }
                }
                catch (Exception googleEx)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[MarkComplete] Google Tasks: {googleEx.Message}");
                }

                // 3. Delete from Supabase appointment_entries immediately
                if (entry != null && !string.IsNullOrEmpty(entry.Id))
                    await _supabaseData.DeleteAppointmentEntryAsync(entry.Id);

                // 4. Delete from Supabase bookings immediately
                await _supabaseData.DeleteBookingAsync(booking.Id);

                // 5. Delete from local SQLite immediately
                await _db.ExecuteAsync(
                    "DELETE FROM AppointmentEntry WHERE SupabaseBookingId = ?",
                    booking.Id);

                System.Diagnostics.Debug.WriteLine(
                    $"[MarkComplete] {booking.FullName} removed from all lists");

                // 6. Refresh the list — booking gone immediately
                await FetchAndPopulate();

                await Shell.Current.DisplayAlert("Completed",
                    $"{booking.FullName}'s appointment has been completed " +
                    "and removed from the list.", "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MarkComplete] {ex.Message}");
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
            finally { IsLoading = false; }
        }
    }
}