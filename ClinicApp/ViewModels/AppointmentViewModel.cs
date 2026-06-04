using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

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
                $"Approve booking for {booking.FullName}?\nService: {booking.Service}",
                "Approve", "Cancel");

            if (!confirm) return;

            IsLoading = true;
            try
            {
                // 1. Save to local SQLite — this is what PatientList reads from
                var parts = (booking.FullName ?? "").Trim().Split(' ', 2);
                var patient = new Patient
                {
                    FirstName = parts.Length > 0 ? parts[0] : "",
                    LastName = parts.Length > 1 ? parts[1] : "",
                    MobileNo = booking.Phone ?? "",
                    Email = booking.Email ?? "",
                    DateOfBirth = booking.DateOfBirth.HasValue
                                                ? booking.DateOfBirth.Value.ToString("yyyy-MM-dd")
                                                : "",
                    ReasonForConsultation = booking.Service ?? "",
                    ReferredBy = "Online Booking",
                    DateRegistered = DateTime.Now.ToString("yyyy-MM-dd")
                };
                await _db.AddPatient(patient);
                System.Diagnostics.Debug.WriteLine($"[Approve] SQLite PatientID={patient.PatientID}");

                // 2. Save to Supabase patients table for cross-device sync
                var supPatient = new SupabasePatient
                {
                    FirstName = patient.FirstName,
                    LastName = patient.LastName,
                    Phone = patient.MobileNo,
                    Email = patient.Email,
                    DateOfBirth = booking.DateOfBirth,
                    ReasonForConsultation = patient.ReasonForConsultation,
                    ReferredBy = "Online Booking",
                    DateRegistered = DateTime.Now
                };
                await _supabaseData.AddPatientAsync(supPatient);

                // 3. Update booking status to approved
                await _supabaseData.UpdateBookingStatusAsync(booking.Id, "approved");

                await Shell.Current.DisplayAlert("Approved",
                    $"{booking.FullName} has been added to the patient list.", "OK");

                // 4. Reload appointments
                await FetchAndPopulate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Approve] FAILED: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        async Task Reschedule(SupabaseBooking booking)
        {
            if (booking == null)
            {
                System.Diagnostics.Debug.WriteLine("[Reschedule] booking is null");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[Reschedule] Starting for {booking.FullName}, Id={booking.Id}");

            bool confirm = await Shell.Current.DisplayAlert(
                "Reschedule Booking",
                $"Mark {booking.FullName}'s booking for rescheduling?\n" +
                $"The dentist will contact the patient to set a new date.",
                "Reschedule", "Cancel");

            if (!confirm) return;

            IsLoading = true;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[Reschedule] Updating status to rescheduled...");
                await _supabaseData.UpdateBookingStatusAsync(booking.Id, "rescheduled");
                System.Diagnostics.Debug.WriteLine($"[Reschedule] Status updated successfully.");

                await FetchAndPopulate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Reschedule] FAILED: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[Reschedule] Stack: {ex.StackTrace}");
                await Shell.Current.DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
            }
            finally { IsLoading = false; }
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