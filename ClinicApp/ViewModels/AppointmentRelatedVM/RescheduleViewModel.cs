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

        static readonly TimeZoneInfo ManilaTz =
     TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila") ?? TimeZoneInfo.Utc;

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
                var bookedSlots = await _supabaseData.GetBookedTimeSlotsForDateAsync(date);

                TimeSlots.Clear();

                var hours = new[] { 10, 11, 13, 14, 15, 16 };
                foreach (var h in hours)
                {
                    var slotTime = new DateTime(date.Year, date.Month, date.Day, h, 0, 0);

                    var slotUtc = TimeZoneInfo.ConvertTimeToUtc(slotTime, ManilaTz);

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
        async Task SelectSlot(TimeSlotItem slot)
        {
            if (slot == null) return;

            if (slot.IsTaken)
            {
                await Shell.Current.DisplayAlert(
                    "Slot Unavailable",
                    $"{slot.Display} is already booked. Please choose a different time.",
                    "OK");
                return;
            }

            // Deselect all
            foreach (var s in TimeSlots)
            {
                s.IsSelected = false;
                s.RefreshColors();
            }

            // Select this one
            slot.IsSelected = true;
            slot.RefreshColors();
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
            HasError = false;
            ErrorMessage = string.Empty;

            try
            {
                // Convert Philippine time to UTC for storage
                var utcTime = TimeZoneInfo.ConvertTimeToUtc(
                    _selectedSlot.SlotDateTime,
                    ManilaTz);

                // ── Final guard: re-check right before writing, in case another
                // booking took this slot after the page loaded. Excludes this
                // booking's own current appointment_entries row so rescheduling
                // "into" its existing slot never false-blocks. ──
                var stillFree = await _supabaseData.IsSlotAvailableAsync(utcTime, BookingId);
                if (!stillFree)
                {
                    HasError = true;
                    ErrorMessage = "This time slot was just booked by someone else. Please pick a different time.";

                    await Shell.Current.DisplayAlert(
                        "Slot No Longer Available",
                        "That time slot has just been taken. Please choose another time.",
                        "OK");

                    // Refresh so the grid reflects reality
                    HasSelection = false;
                    _selectedSlot = null;
                    await LoadSlotsForDateAsync(SelectedDate);
                    return;
                }

                // Update booking in Supabase with new date
                await _supabaseData.RescheduleBookingAsync(
                    BookingId, utcTime);

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
        public bool IsTaken { get; set; }

        [ObservableProperty] bool isSelected;

        public void RefreshColors()
        {
            OnPropertyChanged(nameof(BackgroundColor));
            OnPropertyChanged(nameof(BorderColor));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(StatusText));
        }

        public string StatusText =>
            IsTaken ? "Unavailable" :
            IsSelected ? "Selected ✓" : "Available";

        public Color BackgroundColor =>
            IsTaken ? Color.FromArgb("#F0F0F0") :
            IsSelected ? Color.FromArgb("#2E7D32") :
                         Colors.White;

        public Color BorderColor =>
            IsTaken ? Color.FromArgb("#CCCCCC") :
            IsSelected ? Color.FromArgb("#2E7D32") :
                         Color.FromArgb("#C8A84B");

        public Color TextColor =>
            IsTaken ? Color.FromArgb("#AAAAAA") :
            IsSelected ? Colors.White :
                         Color.FromArgb("#1A1A2E");

        public Color StatusColor =>
            IsTaken ? Color.FromArgb("#AAAAAA") :
            IsSelected ? Color.FromArgb("#A5D6A7") :
                         Color.FromArgb("#2E7D32");
    }
}