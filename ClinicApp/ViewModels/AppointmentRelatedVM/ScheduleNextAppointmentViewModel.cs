
using ClinicApp.Models.AppointmentModels;
using ClinicApp.Models.PatientModels;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    [QueryProperty(nameof(SequenceId), "sequenceId")]
    [QueryProperty(nameof(PatientId), "patientId")]
    [QueryProperty(nameof(PatientName), "patientName")]
    [QueryProperty(nameof(Phone), "phone")]
    [QueryProperty(nameof(Email), "email")]
    [QueryProperty(nameof(ServiceId), "serviceId")]
    [QueryProperty(nameof(ServiceName), "serviceName")]
    [QueryProperty(nameof(SessionNumber), "sessionNumber")]
    [QueryProperty(nameof(TotalSessions), "totalSessions")]
    [QueryProperty(nameof(RecommendedDateRaw), "recommendedDate")]
    public partial class ScheduleNextAppointmentViewModel : ObservableObject
    {
        readonly DatabaseService _db;
        readonly SupabaseDataService _supabase;

        [ObservableProperty] string sequenceId = string.Empty;
        [ObservableProperty] string patientId = string.Empty;
        [ObservableProperty] string patientName = string.Empty;
        [ObservableProperty] string phone = string.Empty;
        [ObservableProperty] string email = string.Empty;
        [ObservableProperty] string serviceId = string.Empty;
        [ObservableProperty] string serviceName = string.Empty;
        [ObservableProperty] int sessionNumber;
        [ObservableProperty] int totalSessions;
        [ObservableProperty] string recommendedDateRaw = string.Empty;

        [ObservableProperty] DateTime appointmentDate = DateTime.Today.AddDays(1);
        [ObservableProperty] bool isLoadingSlots;
        [ObservableProperty] bool hasNoSlots;
        [ObservableProperty] bool isBusy;
        [ObservableProperty] bool hasError;
        [ObservableProperty] string errorMessage = string.Empty;
        [ObservableProperty] bool hasSelection;
        [ObservableProperty] string selectedSummary = string.Empty;

        public string SessionLabel => $"Session {SessionNumber} of {TotalSessions}";
        public DateTime MinDate => DateTime.Today;
        public DateTime MaxDate => DateTime.Today.AddDays(90);

        public ObservableCollection<TimeSlotItem> TimeSlots { get; } = new();
        TimeSlotItem? _selectedSlot;

        public ScheduleNextAppointmentViewModel(DatabaseService db, SupabaseDataService supabase)
        {
            _db = db;
            _supabase = supabase;
        }

        public async Task InitializeAsync()
        {
            if (DateTime.TryParse(RecommendedDateRaw, out var recommended)
                && recommended.Date >= DateTime.Today)
            {
                AppointmentDate = recommended.Date;
            }

            await LoadSlotsForDateAsync(AppointmentDate);
        }

        public async Task LoadSlotsForDateAsync(DateTime date)
        {
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
                var bookedSlots = await _supabase.GetBookedTimeSlotsForDateAsync(date);
                TimeSlots.Clear();

                var hours = new[] { 8, 9, 10, 11, 13, 14, 15, 16 };
                foreach (var h in hours)
                {
                    var slotTime = new DateTime(date.Year, date.Month, date.Day, h, 0, 0);
                    var slotUtc = slotTime.ToUniversalTime();
                    var isTaken = bookedSlots.Any(b => b == slotUtc);

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

            foreach (var s in TimeSlots) { s.IsSelected = false; s.RefreshColors(); }

            slot.IsSelected = true;
            slot.RefreshColors();
            _selectedSlot = slot;
            HasSelection = true;
            SelectedSummary = $"{slot.SlotDateTime:MMMM dd, yyyy} at {slot.Display}";
        }

        [RelayCommand]
        async Task Confirm()
        {
            if (_selectedSlot == null) return;

            IsBusy = true;
            HasError = false;

            try
            {
                var localTime = _selectedSlot.SlotDateTime;
                var utcTime = localTime.ToUniversalTime();

                var available = await _supabase.IsSlotAvailableAsync(utcTime);
                if (!available)
                {
                    await Shell.Current.DisplayAlert(
                        "Slot Taken",
                        "This time slot has already been booked. Please choose another time.",
                        "OK");
                    await LoadSlotsForDateAsync(AppointmentDate);
                    return;
                }

                var correlationId = Guid.NewGuid().ToString();
                var noteText = $"Follow-up: {ServiceName} — Session {SessionNumber} of {TotalSessions}";

                var localEntry = new AppointmentEntry
                {
                    SupabaseBookingId = correlationId,
                    PatientName = PatientName,
                    PatientSupabaseId = PatientId,
                    Phone = Phone,
                    Email = Email,
                    Notes = noteText,
                    AppointmentDateTime = localTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = "approved",
                    TreatmentSequenceId = SequenceId,
                    SessionNumber = SessionNumber,
                    TotalSessions = TotalSessions
                };
                await _db.AddAppointmentEntry(localEntry);

                var supEntry = new SupabaseAppointmentEntry
                {
                    SupabaseBookingId = correlationId,
                    PatientName = PatientName,
                    PatientId = PatientId,
                    Phone = Phone,
                    Email = Email,
                    Notes = noteText,
                    AppointmentDateTime = utcTime,
                    Status = "approved",
                    TreatmentSequenceId = SequenceId,
                    SessionNumber = SessionNumber,
                    TotalSessions = TotalSessions
                };

                var created = await _supabase.AddAppointmentEntryAsync(supEntry);
                if (created == null)
                {
                    await Shell.Current.DisplayAlert("Error", "Unable to save the follow-up appointment.", "OK");
                    return;
                }

                await _supabase.LinkNextAppointmentToSequenceAsync(SequenceId, correlationId);

                try
                {
                    await _supabase.SyncToGoogleTasksAsync(
                        "",
                        PatientName,
                        $"{ServiceName} — Session {SessionNumber} of {TotalSessions} (Follow-up)",
                        localTime,
                        Phone,
                        noteText);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ScheduleNextAppointment] GoogleTasks: {ex.Message}");
                }

                await Shell.Current.DisplayAlert(
                    "✓ Follow-up Scheduled",
                    $"{PatientName}'s next session is booked for\n{SelectedSummary}",
                    "Done");


                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Booking failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        async Task Cancel() => await Shell.Current.GoToAsync("..");
    }
}