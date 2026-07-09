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
            Console.WriteLine("[Approve] ===== APPROVE STARTED =====");

            if (booking == null)
            {
                Console.WriteLine("[Approve] booking is NULL — returning");
                return;
            }

            Console.WriteLine($"[Approve] Patient: {booking.FullName}");

            bool confirm = await Shell.Current.DisplayAlert(
                "Approve Booking",
                $"Approve booking for {booking.FullName}?\nService: {booking.Service}",
                "Approve", "Cancel");

            Console.WriteLine($"[Approve] Confirmed: {confirm}");
            if (!confirm) return;

            IsLoading = true;
            try
            {
                Console.WriteLine("[Approve] Step 1: Adding patient to SQLite...");
                var parts = (booking.FullName ?? "").Trim().Split(' ', 2);
                var patient = new Patient
                {
                    FirstName = parts.Length > 0 ? parts[0] : "",
                    LastName = parts.Length > 1 ? parts[1] : "",
                    MobileNo = booking.Phone ?? "",
                    Email = booking.Email ?? "",
                    DateOfBirth = booking.DateOfBirth.HasValue
                                                ? booking.DateOfBirth.Value.ToString("yyyy-MM-dd") : "",
                    ReasonForConsultation = booking.Service ?? "",
                    ReferredBy = "Online Booking",
                    DateRegistered = DateTime.Now.ToString("yyyy-MM-dd")
                };
                await _db.AddPatient(patient);
                Console.WriteLine($"[Approve] Step 1 done. PatientID={patient.PatientID}");

                Console.WriteLine("[Approve] Step 2: Adding patient to Supabase...");
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
                Console.WriteLine("[Approve] Step 2 done.");

                Console.WriteLine("[Approve] Step 3: Adding local appointment entry...");
                var localEntry = new AppointmentEntry
                {
                    SupabaseBookingId = booking.Id,
                    PatientName = booking.FullName ?? "",
                    Phone = booking.Phone ?? "",
                    Email = booking.Email ?? "",
                    Service = booking.Service ?? "",
                    Notes = booking.Notes ?? "",
                    AppointmentDateTime = booking.AppointmentDate.Kind == DateTimeKind.Utc
                                              ? booking.AppointmentDate.ToLocalTime()
                                                    .ToString("yyyy-MM-dd HH:mm:ss")
                                              : booking.AppointmentDate
                                                    .ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = "approved"
                };
                await _db.AddAppointmentEntry(localEntry);
                Console.WriteLine("[Approve] Step 3 done.");

                Console.WriteLine("[Approve] Step 4: Adding Supabase appointment entry...");
                var supEntry = new SupabaseAppointmentEntry
                {
                    SupabaseBookingId = booking.Id,
                    PatientName = booking.FullName ?? "",
                    Phone = booking.Phone ?? "",
                    Email = booking.Email ?? "",
                    Service = booking.Service ?? "",
                    Notes = booking.Notes ?? "",
                    AppointmentDateTime = booking.AppointmentDate.ToUniversalTime(),
                    Status = "approved"
                };
                await _supabaseData.AddAppointmentEntryAsync(supEntry);
                Console.WriteLine("[Approve] Step 4 done.");

                Console.WriteLine("[Approve] Step 5: Updating booking status...");
                await _supabaseData.UpdateBookingStatusAsync(booking.Id, "approved");
                Console.WriteLine("[Approve] Step 5 done.");

                // ── Step 6: Google Tasks ──────────────────────────────────
                try
                {
                    var taskId = await _supabaseData.SyncToGoogleTasksAsync(
                        "",  // pass empty — auto-fetches fresh token
                        booking.FullName ?? "",
                        booking.Service ?? "",
                        booking.AppointmentDate,
                        booking.Phone ?? "",
                        booking.Notes ?? "");

                    if (!string.IsNullOrEmpty(taskId))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[Approve] Google Task created: {taskId}");

                        // Save taskId to local entry for later complete/delete
                        var entries = await _db.GetAppointmentsForWeek(
                            WeekStart(booking.AppointmentDate));
                        var entry = entries.FirstOrDefault(
                            e => e.SupabaseBookingId == booking.Id);
                        if (entry != null)
                        {
                            entry.GoogleTaskId = taskId;
                            await _db.UpdateAppointmentEntry(entry);
                        }
                    }
                }
                catch (Exception googleEx)
                {
                    // Never block approve if Google Tasks fails
                    System.Diagnostics.Debug.WriteLine(
                        $"[Approve] Google Tasks error: {googleEx.Message}");
                }

                Console.WriteLine("[Approve] Step 6 done.");

                await Shell.Current.DisplayAlert("Approved",
                    $"{booking.FullName} added to patient list and schedule.", "OK");

                Console.WriteLine("[Approve] ===== APPROVE COMPLETE =====");
                await FetchAndPopulate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Approve] MAIN EXCEPTION: {ex.Message}");
                Console.WriteLine($"[Approve] STACK: {ex.StackTrace}");
                await Shell.Current.DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
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
    }
}