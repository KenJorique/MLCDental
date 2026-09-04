using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using ClinicApp.Views.AppointmentRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    public partial class PendingFollowUpsViewModel : ObservableObject
    {
        readonly SupabaseDataService _supabase;

        public ObservableCollection<SupabaseTreatmentSequence> FollowUps { get; } = new();
        [ObservableProperty] bool isBusy;
        [ObservableProperty] bool hasNone;

        public PendingFollowUpsViewModel(SupabaseDataService supabase)
        {
            _supabase = supabase;
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            IsBusy = true;
            try
            {
                var pending = await _supabase.GetPendingFollowUpsAsync();
                FollowUps.Clear();
                foreach (var p in pending) FollowUps.Add(p);
                HasNone = FollowUps.Count == 0;
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        async Task ScheduleNow(SupabaseTreatmentSequence sequence)
        {
            if (sequence == null) return;

            await Shell.Current.GoToAsync(
                $"{nameof(ScheduleNextAppointmentPage)}" +
                $"?sequenceId={Uri.EscapeDataString(sequence.Id)}" +
                $"&patientId={Uri.EscapeDataString(sequence.PatientId)}" +
                $"&patientName={Uri.EscapeDataString(sequence.PatientName)}" +
                $"&phone={string.Empty}" +
                $"&email={string.Empty}" +
                $"&serviceId={Uri.EscapeDataString(sequence.ServiceId)}" +
                $"&serviceName={Uri.EscapeDataString(sequence.ServiceName)}" +
                $"&sessionNumber={sequence.SessionNumber + 1}" +
                $"&totalSessions={sequence.TotalSessions}" +
                $"&recommendedDate={Uri.EscapeDataString(sequence.RecommendedDate?.ToString("o") ?? string.Empty)}");
        }
    }
}