using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    // One row on the Follow-ups page / bill-completion sheet. Covers both
    // "needs a date" and "already scheduled, editable inline" using the same
    // day-picker + slot-chip UI as ReschedulePage, for a consistent picker
    // everywhere in the app.
    public partial class FollowUpDisplayItem : ObservableObject
    {
        readonly SupabaseDataService _supabase;

        public SupabaseTreatmentSequence Sequence { get; }

        [ObservableProperty] DateTime selectedDate;
        [ObservableProperty] bool isBusy;
        [ObservableProperty] bool isLoadingSlots;
        [ObservableProperty] bool hasNoSlots;
        [ObservableProperty] bool hasSelection;
        [ObservableProperty] string selectedSummary = string.Empty;

        // ── Already-scheduled display / edit toggle ──
        [ObservableProperty] bool isEditingDate;
        [ObservableProperty] string currentAppointmentDisplay = "—";
        DateTime? _currentAppointmentUtc;

        public ObservableCollection<TimeSlotItem> TimeSlots { get; } = new();
        public TimeSlotItem? SelectedSlot { get; private set; }

        public DateTime? SelectedSlotLocal => SelectedSlot?.SlotDateTime;
        public DateTime? SelectedSlotUtc =>
            SelectedSlot == null ? null : TimeZoneInfo.ConvertTimeToUtc(SelectedSlot.SlotDateTime, ManilaTz);

        static readonly TimeZoneInfo ManilaTz =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila") ?? TimeZoneInfo.Utc;

        public string ServiceName => Sequence.ServiceName;
        public string PatientName => Sequence.PatientName;
        public string SessionDisplay => $"Session {Sequence.SessionNumber + 1} of {Sequence.TotalSessions}";
        public string RecommendedDateDisplay => Sequence.RecommendedDateDisplay;
        public DateTime MinDate => DateTime.Today;
        public DateTime MaxDate => DateTime.Today.AddDays(180);

        // Seeds the recommended date (or a week out, if none) for this sequence.
        public FollowUpDisplayItem(SupabaseTreatmentSequence sequence, SupabaseDataService supabase)
        {
            Sequence = sequence;
            _supabase = supabase;
            SelectedDate = sequence.RecommendedDate?.ToLocalTime().Date ?? DateTime.Today.AddDays(7);
        }

        // Preloads slots for a not-yet-scheduled item — call right after construction.
        public async Task InitializeAsync() => await LoadSlotsAsync();

        // Records the current appointment's date/time for display, without opening the editor yet.
        public void SetScheduledInfo(DateTime localDateTime, DateTime utcDateTime)
        {
            _currentAppointmentUtc = utcDateTime;
            CurrentAppointmentDisplay = localDateTime.ToString("MMM dd, yyyy h:mm tt");
        }

        // Opens the date/slot picker for an already-scheduled item, keeping its own current slot selectable.
        public async Task ToggleEditModeAsync()
        {
            IsEditingDate = !IsEditingDate;

            if (IsEditingDate && _currentAppointmentUtc.HasValue)
            {
                SelectedDate = _currentAppointmentUtc.Value.ToLocalTime().Date;
                await LoadSlotsAsync();
            }
        }

        // Reloads slots whenever the picked date changes.
        partial void OnSelectedDateChanged(DateTime value) => _ = LoadSlotsAsync();

        // Tracks the most recent LoadSlotsAsync call, so a slower older request can't overwrite a newer one's result.
        int _loadRequestId;

        public async Task LoadSlotsAsync()
        {
            var requestedDate = SelectedDate; // snapshot once — never re-read the live property after an await
            var myRequestId = ++_loadRequestId;

            if (requestedDate.DayOfWeek == DayOfWeek.Sunday)
            {
                if (myRequestId != _loadRequestId) return;
                TimeSlots.Clear();
                HasNoSlots = true;
                HasSelection = false;
                SelectedSlot = null;
                return;
            }

            IsLoadingSlots = true;
            HasSelection = false;
            SelectedSlot = null;

            try
            {
                var bookedSlots = await _supabase.GetBookedTimeSlotsForDateAsync(requestedDate);

                // A newer date change started its own load while this one was in flight — drop this stale result.
                if (myRequestId != _loadRequestId) return;

                TimeSlots.Clear();

                var hours = new[] { 10, 11, 13, 14, 15, 16 };
                foreach (var h in hours)
                {
                    var slotTime = new DateTime(requestedDate.Year, requestedDate.Month, requestedDate.Day, h, 0, 0);
                    var slotUtc = TimeZoneInfo.ConvertTimeToUtc(slotTime, ManilaTz);

                    // Don't grey out this appointment's own current slot while editing it
                    var isOwnCurrentSlot = _currentAppointmentUtc.HasValue && _currentAppointmentUtc.Value == slotUtc;
                    var isTaken = !isOwnCurrentSlot && bookedSlots.Any(b => b == slotUtc);

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

                    if (isOwnCurrentSlot)
                        SelectSlot(item);
                }

                HasNoSlots = !TimeSlots.Any();
            }
            finally
            {
                if (myRequestId == _loadRequestId)
                    IsLoadingSlots = false;
            }
        }

        // Selects a slot (ignored if taken), refreshing every slot's color so only the new pick shows selected.
        [RelayCommand]
        void SelectSlot(TimeSlotItem slot)
        {
            if (slot == null || slot.IsTaken) return;

            foreach (var s in TimeSlots) { s.IsSelected = false; s.RefreshColors(); }
            slot.IsSelected = true;
            slot.RefreshColors();

            SelectedSlot = slot;
            HasSelection = true;
            SelectedSummary = $"{slot.SlotDateTime:MMMM dd, yyyy} at {slot.Display}";
        }
    }
}