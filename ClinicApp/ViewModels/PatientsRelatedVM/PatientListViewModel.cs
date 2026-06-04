using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.CephalometricRelated;
using ClinicApp.Views.DentalChart;
using ClinicApp.Views.PatientsRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.PatientsRelatedVM
{
    public partial class PatientListViewModel : ObservableObject
    {
        readonly DatabaseService _db;
        readonly SupabaseRealtimeService _realtime;
        readonly SupabaseDataService _supabaseData;

        public ObservableCollection<PatientCardViewModel> Patients { get; set; } = new();

        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private bool isRefreshing;

        // Badge count for new online bookings not yet reviewed
        [ObservableProperty] private int newBookingCount;
        [ObservableProperty] private bool hasNewBookings;

        public PatientListViewModel(DatabaseService db, SupabaseRealtimeService realtime, SupabaseDataService supabaseData)
        {
            _db = db;
            _realtime = realtime;
            _supabaseData = supabaseData;

            // When Supabase fires a new booking, reload the list
            _realtime.OnNewBookingReceived += async () =>
            {
                newBookingCount++;
                hasNewBookings = true;
                await LoadPatients();
            };
            _supabaseData = supabaseData;
        }

        private async Task LoadPatientsInternal()
        {
            try
            {
                var list = await _db.GetPatients();
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Patients.Clear();
                    foreach (var p in list)
                        Patients.Add(new PatientCardViewModel(p));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadPatientsInternal] {ex.Message}");
            }
        }
        // Pull-to-refresh — syncs from Supabase first then reloads local SQLite
        [RelayCommand]
        async Task Refresh()
        {
            if (isRefreshing) return;
            IsRefreshing = true;
            try
            {
                // Pull latest from Supabase into local SQLite
                await _realtime.SyncMissedPatientsAsync();
                // Then reload list from local SQLite
                await LoadPatientsInternal();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Refresh] {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }
        // Called once from PatientListPage.OnAppearing
        private bool _realtimeStarted = false;

        public async Task StartRealtimeAsync()
        {
            if (_realtimeStarted) return;
            _realtimeStarted = true;

            try
            {
                var url = "https://uxacdqkkocbjaiqszpyk.supabase.co";
                var key = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InV4YWNkcWtrb2NiamFpcXN6cHlrIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODA0NTExNTUsImV4cCI6MjA5NjAyNzE1NX0.Jt-Dsn6j3m9uL_R0A1Y0AVlUKBA_hmNI-NfHDBQYLUA";

                await _realtime.InitializeAsync(url, key);

                // Sync any patients/bookings missed while offline
                await _realtime.SyncMissedPatientsAsync();
                await _realtime.SyncMissedBookingsAsync();

                // Backfill SupabaseId for patients that don't have it yet
                var allSupabase = await _supabaseData.GetPatientsAsync();
                await _db.BackfillSupabaseIds(allSupabase);

                // Subscribe to live changes
                await _realtime.SubscribeToBookingsAsync();
                await _realtime.SubscribeToPatientsAsync();

                // When another device adds/edits a patient → sync + reload
                _realtime.OnPatientChanged += async () =>
                {
                    System.Diagnostics.Debug.WriteLine("[Realtime] Patient changed — reloading");
                    await LoadPatientsInternal();
                };

                // When another device makes a booking → update badge
                _realtime.OnNewBookingReceived += async () =>
                {
                    NewBookingCount++;
                    HasNewBookings = true;
                    await LoadPatientsInternal();
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartRealtime] {ex.Message}");
            }
        }

        [RelayCommand]
        async Task LoadPatients()
        {
            if (isBusy) return;
            isBusy = true;
            try
            {
                await LoadPatientsInternal();
            }
            finally
            {
                isBusy = false;
                isRefreshing = false;
            }
        }


        // Maps SupabasePatient → local Patient model
        private static Patient MapToPatient(SupabasePatient sp)
        {
            return new Patient
            {
                // Try to parse the Supabase UUID as a local ID fallback
                FirstName = sp.FirstName,
                LastName = sp.LastName ?? string.Empty,
                Nickname = sp.Nickname ?? string.Empty,
                Gender = sp.Gender ?? string.Empty,
                DateOfBirth = sp.DateOfBirth.HasValue
                                             ? sp.DateOfBirth.Value.ToString("yyyy-MM-dd")
                                             : string.Empty,
                Nationality = sp.Nationality ?? string.Empty,
                Religion = sp.Religion ?? string.Empty,
                Occupation = sp.Occupation ?? string.Empty,
                Address = sp.Address ?? string.Empty,
                MobileNo = sp.Phone ?? string.Empty,
                HomeNo = sp.HomeNo ?? string.Empty,
                OfficeNo = sp.OfficeNo ?? string.Empty,
                FaxNo = sp.FaxNo ?? string.Empty,
                Email = sp.Email ?? string.Empty,
                ReferredBy = sp.ReferredBy ?? string.Empty,
                ReasonForConsultation = sp.ReasonForConsultation ?? string.Empty,
                DentalInsurance = sp.DentalInsurance ?? string.Empty,
                InsuranceEffectiveDate = sp.InsuranceEffectiveDate.HasValue
                                             ? sp.InsuranceEffectiveDate.Value.ToString("yyyy-MM-dd")
                                             : string.Empty,
                DateRegistered = sp.DateRegistered.ToString("yyyy-MM-dd"),
                SupabaseId = sp.Id
            };
        }

        [RelayCommand]
        void ToggleExpand(PatientCardViewModel card)
        {
            if (card == null) return;
            bool wasExpanded = card.IsExpanded;
            foreach (var c in Patients)
                c.IsExpanded = false;
            if (!wasExpanded)
                card.IsExpanded = true;
        }

        [RelayCommand]
        async Task GoToAddPatient()
        {
            await Shell.Current.GoToAsync(nameof(AddPatientPage));
        }

        [RelayCommand]
        async Task ViewPatient(PatientCardViewModel card)
        {
            if (card == null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(PatientDetailsPage)}?id={card.Patient.PatientID}");
        }

        [RelayCommand]
        async Task EditPatient(PatientCardViewModel card)
        {
            if (card == null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(AddPatientPage)}?PatientId={card.Patient.PatientID}");
        }

        [RelayCommand]
        async Task DeletePatient(PatientCardViewModel card)
        {
            if (card == null) return;
            bool answer = await Shell.Current.DisplayAlert("Confirm Delete",
                $"Are you sure you want to delete {card.Patient.FirstName} {card.Patient.LastName}?",
                "Yes", "No");

            if (answer)
            {
                try
                {
                    // Delete from local SQLite
                    await _db.DeletePatient(card.Patient);

                    // Delete from Supabase if it has a cloud ID
                    if (!string.IsNullOrEmpty(card.Patient.SupabaseId))
                    {
                        var sp = new SupabasePatient { Id = card.Patient.SupabaseId };
                        await _supabaseData.DeletePatientAsync(sp);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Delete] {ex.Message}");
                }

                await MainThread.InvokeOnMainThreadAsync(async () => await LoadPatients());
            }
        }

        [RelayCommand]
        async Task ViewDentalChart(PatientCardViewModel card)
        {
            if (card == null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(DentalChartPage)}?patientId={card.Patient.PatientID}" +
                $"&patientName={Uri.EscapeDataString(card.Patient.FirstName + " " + card.Patient.LastName)}");
        }

        [RelayCommand]
        async Task GoToCephalometric(PatientCardViewModel card)
        {
            if (card == null) return;
            string fullName = Uri.EscapeDataString(
                $"{card.Patient.FirstName} {card.Patient.LastName}");
            await Shell.Current.GoToAsync(
                $"{nameof(CephalometricPage)}?PatientId={card.Patient.PatientID}&PatientName={fullName}");
        }

        [RelayCommand]
        async Task ViewTreatmentHistory(PatientCardViewModel card)
        {
            if (card == null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(TreatmentHistoryPage)}?patientId={card.Patient.PatientID}" +
                $"&patientName={Uri.EscapeDataString(card.Patient.FirstName + " " + card.Patient.LastName)}");
        }
    }
}