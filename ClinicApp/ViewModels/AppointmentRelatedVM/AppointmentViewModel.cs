using ClinicApp.Models.AppointmentModels;
using ClinicApp.Models.PatientModels;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using ClinicApp.Views.AppointmentRelated;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    public partial class AppointmentViewModel : ObservableObject
    {
        readonly DatabaseService _db;
        readonly SupabaseDataService _supabaseData;

        static readonly TimeZoneInfo ManilaTz =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila") ?? TimeZoneInfo.Utc;

        public ObservableCollection<BookingCardViewModel> PendingBookings { get; set; } = new();

        // Separate busy flags — IsRefreshing for pull-to-refresh, IsLoading for internal ops
        [ObservableProperty] private bool isRefreshing;
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private int pendingCount;
        [ObservableProperty] private BookingCardViewModel? selectedCard;

        // Capital H — matches XAML binding exactly
        public bool HasPending => PendingCount > 0;

        // Injects local + remote data services.
        public AppointmentViewModel(DatabaseService db, SupabaseDataService supabaseData)
        {
            _db = db;
            _supabaseData = supabaseData;
        }

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
                var pending = await _supabaseData.GetBookingsByStatusAsync("pending");

                PendingBookings.Clear();
                foreach (var b in pending)
                    PendingBookings.Add(new BookingCardViewModel(b));

                PendingCount = PendingBookings.Count;

                OnPropertyChanged(nameof(HasPending));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FetchAndPopulate] {ex.Message}");
            }

        }

        PendingDetailSheet? _pendingSheet;

        // Opens the bottom detail sheet when a card is tapped, matching AppointmentDetailSheet's layout.
        [RelayCommand]
        async Task ShowBookingDetail(BookingCardViewModel card)
        {
            if (card is null) return;
            SelectedCard = card;

            _pendingSheet = new PendingDetailSheet { BindingContext = this };
            await _pendingSheet.ShowAsync();
        }

        // Dismisses the pending-detail bottom sheet, if one is open.
        async Task CloseSheetAsync()
        {
            if (_pendingSheet == null) return;
            var sheet = _pendingSheet;
            _pendingSheet = null;
            try { await sheet.DismissAsync(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CloseSheetAsync] {ex.Message}");
            }
        }

        // Opens the device dialer with the given phone number.
        [RelayCommand]
        async Task CallPatient(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                await ShowNoticeAsync("Error", "No phone number available for this patient.");
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
                    await ShowNoticeAsync("Not Supported", "Phone dialing is not supported on this device.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CallPatient] Error: {ex.Message}");
                await ShowErrorAsync("Unable to open phone dialer.");
            }
        }

        // Approves a pending booking: resolves/creates the patient record (Supabase + local, phone kept in sync), guards against a double-booked slot, creates the appointment entry, and syncs to Google Tasks.
        [RelayCommand]
        async Task Approve(BookingCardViewModel card)
        {
            if (card == null) return;
            var booking = card.Booking;

            bool confirm = await ShowConfirmAsync(
                "Approve Booking",
                $"Approve booking for {booking.FullName}",
                "Approve");

            if (!confirm) return;

            IsLoading = true;
            try
            {
                // Resolve the patient: Supabase phone match, then Supabase name match, then a local-DB fallback (in case this patient was never synced to Supabase).
                SupabasePatient? patient = null;
                Patient? localOnlyMatch = null;

                if (!string.IsNullOrEmpty(booking.Phone))
                    patient = await _supabaseData.GetPatientByPhoneAsync(booking.Phone);

                if (patient == null && !string.IsNullOrEmpty(booking.FullName))
                    patient = await _supabaseData.GetPatientByNameAsync(booking.FullName);

                if (patient == null)
                {
                    var localPatients = await _db.GetPatients();
                    localOnlyMatch = localPatients.FirstOrDefault(p =>
                        NormalizeName(p.FullName) == NormalizeName(booking.FullName ?? "") ||
                        (!string.IsNullOrEmpty(booking.Phone) && PhoneEndsMatch(p.MobileNo, booking.Phone)));
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[Approve] Match — Supabase: {(patient != null ? patient.Id : "NONE")}, " +
                    $"local-only: {(localOnlyMatch != null ? localOnlyMatch.PatientID.ToString() : "NONE")}");

                if (patient == null && localOnlyMatch == null)
                {
                    // No match anywhere — genuinely a new patient.
                    var parts = (booking.FullName ?? "").Trim().Split(' ', 2);
                    var localPatient = new Patient
                    {
                        FirstName = parts.Length > 0 ? parts[0] : "",
                        LastName = parts.Length > 1 ? parts[1] : "",
                        MobileNo = booking.Phone ?? "",
                        Email = booking.Email ?? "",
                        ReferredBy = "Online Booking",
                        DateRegistered = DateTime.Now.ToString("yyyy-MM-dd")
                    };

                    var supPatient = new SupabasePatient
                    {
                        FirstName = localPatient.FirstName,
                        LastName = localPatient.LastName,
                        Phone = localPatient.MobileNo,
                        Email = localPatient.Email,
                        ReferredBy = "Online Booking",
                        DateRegistered = DateTime.UtcNow
                    };
                    patient = await _supabaseData.AddPatientAsync(supPatient);

                    if (patient != null)
                        localPatient.SupabaseId = patient.Id;

                    await _db.AddPatient(localPatient);

                    System.Diagnostics.Debug.WriteLine(
                        $"[Approve] New patient created: {localPatient.FirstName}");
                }
                else if (patient != null)
                {
                    // Matched on Supabase — overwrite the phone there and locally if this booking used a different number.
                    if (!string.IsNullOrEmpty(booking.Phone) && patient.Phone != booking.Phone)
                    {
                        patient.Phone = booking.Phone;
                        await _supabaseData.UpdatePatientAsync(patient);
                        await _db.SyncPatientFromSupabase(patient);

                        System.Diagnostics.Debug.WriteLine(
                            $"[Approve] Phone updated for existing patient: {patient.Id}");
                    }
                }
                else if (localOnlyMatch != null)
                {
                    // Found locally but never made it to Supabase — update the phone locally, then push this patient to Supabase now.
                    if (!string.IsNullOrEmpty(booking.Phone))
                        localOnlyMatch.MobileNo = booking.Phone;

                    var supPatient = new SupabasePatient
                    {
                        FirstName = localOnlyMatch.FirstName,
                        LastName = localOnlyMatch.LastName,
                        Phone = localOnlyMatch.MobileNo,
                        Email = localOnlyMatch.Email,
                        ReferredBy = localOnlyMatch.ReferredBy,
                        DateRegistered = DateTime.UtcNow
                    };
                    var inserted = await _supabaseData.AddPatientAsync(supPatient);

                    if (inserted != null)
                    {
                        localOnlyMatch.SupabaseId = inserted.Id;
                        patient = inserted;
                    }

                    await _db.UpdatePatient(localOnlyMatch);

                    System.Diagnostics.Debug.WriteLine(
                        $"[Approve] Local-only patient synced to Supabase: {localOnlyMatch.PatientID}");
                }

                // Booking's appointment date treated as PH local time.
                var localDate = booking.AppointmentDate.Kind == DateTimeKind.Utc
                    ? booking.AppointmentDate.ToLocalTime()
                    : DateTime.SpecifyKind(booking.AppointmentDate, DateTimeKind.Local);

                // UTC equivalent, explicitly against Asia/Manila (not the device's own timezone) — matches how every other slot check in the app resolves PH time.
                var utcDate = TimeZoneInfo.ConvertTimeToUtc(localDate, ManilaTz);

                // Guard against double-booking: another booking for this exact slot may have already been approved while this one sat pending.
                var slotStillFree = await _supabaseData.IsSlotAvailableAsync(utcDate);
                if (!slotStillFree)
                {
                    await ShowNoticeAsync(
                        "Slot Already Taken",
                        $"{booking.FullName}'s requested time ({localDate:MMM dd, yyyy h:mm tt}) " +
                        "has already been booked by another approved appointment. " +
                        "Please reschedule this booking to a different time before approving.");
                    return;
                }

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
                    PatientId = patient?.Id ?? "", // links the entry back to the patient record
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

                await ShowNoticeAsync("Approved",
                    booking.IsExistingPatient
                        ? $"{booking.FullName}'s appointment approved. (Existing patient)"
                        : $"{booking.FullName} added to patient list and approved.");

                await CloseSheetAsync();
                await FetchAndPopulate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Approve] {ex.Message}");
                await ShowErrorAsync(ex.Message);
            }
            finally { IsLoading = false; }
        }

        // Helper to get week start for date lookup
        private DateTime WeekStart(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Sunday)) % 7;
            return date.AddDays(-diff).Date;
        }

        // Closes the sheet and navigates to ReschedulePage for this booking.
        [RelayCommand]
        async Task Reschedule(BookingCardViewModel card)
        {
            if (card == null)
            {
                System.Diagnostics.Debug.WriteLine("[Reschedule] card is null");
                return;
            }
            var booking = card.Booking;

            await CloseSheetAsync();

            var currentDt = booking.AppointmentDate != DateTime.MinValue
                ? booking.AppointmentDate.ToString("MMM dd, yyyy h:mm tt")
                : "Unknown";

            await Shell.Current.GoToAsync(
                $"{nameof(ReschedulePage)}" +
                $"?bookingId={Uri.EscapeDataString(booking.Id)}" +
                $"&patientName={Uri.EscapeDataString(booking.FullName ?? "")}" +
                $"&currentDateTime={Uri.EscapeDataString(currentDt)}");
        }

        // Reverts a booking's status back to pending.
        [RelayCommand]
        async Task MoveToPending(BookingCardViewModel card)
        {
            if (card == null)
            {
                System.Diagnostics.Debug.WriteLine("[MoveToPending] card is null");
                return;
            }
            var booking = card.Booking;

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
                await ShowErrorAsync($"Failed: {ex.Message}");
            }
            finally { IsLoading = false; }
        }

        // Cancel a pending booking
        [RelayCommand]
        async Task CancelBooking(BookingCardViewModel card)
        {
            if (card == null) return;
            var booking = card.Booking;

            bool confirm = await ShowConfirmAsync(
                "Cancel Booking",
                $"Cancel {booking.FullName}'s booking?\nThis cannot be undone.",
                "Yes, cancel");

            if (!confirm) return;

            IsLoading = true;
            try
            {
                await _supabaseData.UpdateBookingStatusAsync(booking.Id, "cancelled");
                await CloseSheetAsync();
                await FetchAndPopulate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CancelBooking] {ex.Message}");
                await ShowErrorAsync(ex.Message);
            }
            finally { IsLoading = false; }
        }

        // Completes an appointment: closes its Google Task, then deletes it everywhere (Supabase + local).
        [RelayCommand]
        async Task MarkComplete(BookingCardViewModel card)
        {
            if (card == null) return;
            var booking = card.Booking;

            bool confirm = await ShowConfirmAsync(
                "Mark as Complete",
                $"Mark {booking.FullName}'s appointment as completed?\n" +
                "It will be removed from the appointment list.",
                "Yes");

            if (!confirm) return;

            IsLoading = true;
            try
            {
                // Get the entry before deleting so its Google Task can be completed first.
                var entries = await _supabaseData.GetAppointmentEntriesAsync();
                var entry = entries.FirstOrDefault(
                    e => e.SupabaseBookingId == booking.Id);

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

                // Remove the appointment + booking from Supabase, then the local mirror.
                if (entry != null && !string.IsNullOrEmpty(entry.Id))
                    await _supabaseData.DeleteAppointmentEntryAsync(entry.Id);

                await _supabaseData.DeleteBookingAsync(booking.Id);

                await _db.ExecuteAsync(
                    "DELETE FROM AppointmentEntry WHERE SupabaseBookingId = ?",
                    booking.Id);

                System.Diagnostics.Debug.WriteLine(
                    $"[MarkComplete] {booking.FullName} removed from all lists");

                await FetchAndPopulate();

                await ShowNoticeAsync("Completed",
                    $"{booking.FullName}'s appointment has been completed " +
                    "and removed from the list.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MarkComplete] {ex.Message}");
                await ShowErrorAsync(ex.Message);
            }
            finally { IsLoading = false; }
        }

        // Collapses whitespace and lowercases a name, for tolerant comparisons against the local patient list.
        private static string NormalizeName(string name) =>
            string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim().ToLowerInvariant();

        // Compares two phone numbers by their last 7 digits, to tolerate formatting differences.
        private static bool PhoneEndsMatch(string a, string b)
        {
            var digitsA = new string((a ?? "").Where(char.IsDigit).ToArray());
            var digitsB = new string((b ?? "").Where(char.IsDigit).ToArray());
            if (digitsA.Length == 0 || digitsB.Length == 0) return false;

            var tailA = digitsA.Length >= 7 ? digitsA[^7..] : digitsA;
            var tailB = digitsB.Length >= 7 ? digitsB[^7..] : digitsB;
            return tailA == tailB;
        }
    }

    // Thin passthrough wrapper around a SupabaseBooking for the card list — always shown fully expanded.
    public partial class BookingCardViewModel : ObservableObject
    {
        public SupabaseBooking Booking { get; }

        // Wraps the given booking for display.
        public BookingCardViewModel(SupabaseBooking booking)
        {
            Booking = booking;
        }

        // Convenience passthroughs so the card template can bind directly
        public string FullName => Booking.FullName ?? "";
        public string Phone => Booking.Phone ?? "";
        public string Email => Booking.Email ?? "";
        public string Notes => Booking.Notes ?? "";
        public DateTime AppointmentDate => Booking.AppointmentDate;
        public bool IsExistingPatient => Booking.IsExistingPatient;
    }
}
