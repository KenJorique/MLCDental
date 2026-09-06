using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    public partial class PendingFollowUpsViewModel : ObservableObject
    {
        readonly SupabaseDataService _supabase;
        readonly DatabaseService _db;

        public ObservableCollection<FollowUpDisplayItem> NeedsScheduling { get; } = new();
        public ObservableCollection<FollowUpDisplayItem> AlreadyScheduled { get; } = new();

        [ObservableProperty] bool isBusy;
        [ObservableProperty] bool hasNone;
        [ObservableProperty] bool hasNeedsScheduling;
        [ObservableProperty] bool hasAlreadyScheduled;

        bool _isLoading;
        bool _reloadRequested;

        public PendingFollowUpsViewModel(SupabaseDataService supabase, DatabaseService db)
        {
            _supabase = supabase;
            _db = db;
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            if (_isLoading)
            {
                _reloadRequested = true;
                return;
            }

            _isLoading = true;
            IsBusy = true;
            try
            {
                do
                {
                    _reloadRequested = false;
                    await LoadOnceAsync();
                }
                while (_reloadRequested);
            }
            finally
            {
                _isLoading = false;
                IsBusy = false;
            }
        }

        async Task LoadOnceAsync()
        {
            var awaiting = await _supabase.GetPendingFollowUpsAsync();
            var scheduled = await _supabase.GetScheduledFollowUpsAsync();

            var awaitingItems = new List<FollowUpDisplayItem>();
            foreach (var s in awaiting)
            {
                var item = new FollowUpDisplayItem(s, _supabase);
                await item.InitializeAsync();
                awaitingItems.Add(item);
            }

            var scheduledItems = new List<FollowUpDisplayItem>();
            foreach (var s in scheduled)
            {
                var item = new FollowUpDisplayItem(s, _supabase);

                if (!string.IsNullOrWhiteSpace(s.NextAppointmentId))
                {
                    var entry = await _supabase.GetAppointmentEntryByBookingIdAsync(s.NextAppointmentId);
                    if (entry != null)
                    {
                        var local = entry.AppointmentDateTime.Kind == DateTimeKind.Utc
                            ? entry.AppointmentDateTime.ToLocalTime()
                            : entry.AppointmentDateTime;

                        item.SetScheduledInfo(local, entry.AppointmentDateTime);
                    }
                }

                scheduledItems.Add(item);
            }

            NeedsScheduling.Clear();
            foreach (var item in awaitingItems) NeedsScheduling.Add(item);

            AlreadyScheduled.Clear();
            foreach (var item in scheduledItems) AlreadyScheduled.Add(item);

            HasNeedsScheduling = NeedsScheduling.Count > 0;
            HasAlreadyScheduled = AlreadyScheduled.Count > 0;
            HasNone = !HasNeedsScheduling && !HasAlreadyScheduled;
        }

        [RelayCommand]
        async Task CreateFollowUp(FollowUpDisplayItem item)
        {
            if (item == null || item.IsBusy || item.SelectedSlot == null) return;

            item.IsBusy = true;
            try
            {
                var patient = await _supabase.GetPatientByIdAsync(item.Sequence.PatientId);
                var phone = patient?.Phone ?? string.Empty;
                var email = patient?.Email ?? string.Empty;

                var success = await _supabase.CreateFollowUpAppointmentAsync(
                    _db, item.Sequence, phone, email,
                    item.SelectedSlotLocal!.Value, item.SelectedSlotUtc!.Value);

                if (!success)
                {
                    await Shell.Current.DisplayAlert(
                        "Error",
                        "That time slot may already be booked, or the appointment could not be saved. Please try a different time.",
                        "OK");
                    return;
                }

                await LoadAsync();
            }
            finally
            {
                item.IsBusy = false;
            }
        }

        [RelayCommand]
        async Task ToggleEditDate(FollowUpDisplayItem item)
        {
            if (item == null) return;
            await item.ToggleEditModeAsync();
        }

        [RelayCommand]
        async Task SaveDateChange(FollowUpDisplayItem item)
        {
            if (item == null || item.IsBusy || item.SelectedSlot == null
                || string.IsNullOrWhiteSpace(item.Sequence.NextAppointmentId))
                return;

            item.IsBusy = true;
            try
            {
                var success = await _supabase.UpdateAppointmentEntryDateTimeAsync(
                    item.Sequence.NextAppointmentId!, item.SelectedSlotUtc!.Value);

                if (!success)
                {
                    await Shell.Current.DisplayAlert("Error", "Could not update the appointment date. Please try again.", "OK");
                    return;
                }

                item.SetScheduledInfo(item.SelectedSlotLocal!.Value, item.SelectedSlotUtc!.Value);
                item.IsEditingDate = false;
            }
            finally
            {
                item.IsBusy = false;
            }
        }
    }
}