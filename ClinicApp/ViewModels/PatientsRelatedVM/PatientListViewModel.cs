using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views;
using ClinicApp.Views.CephalometricRelated;
using ClinicApp.Views.DentalChart;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelliJ.Lang.Annotations;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.PatientsRelatedVM
{
    public partial class PatientListViewModel : ObservableObject
    {
        readonly DatabaseService _db;
        readonly SupabaseRealtimeService _realtime;
        readonly SupabaseDataService _supabaseData;

        private List<PatientCardViewModel> _allPatients = new();
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

                // Temporary debug — check what's actually in Supabase bookings
                var allBookings = await _supabaseData.GetAllBookingsDebugAsync();
                System.Diagnostics.Debug.WriteLine(
                    $"[Debug] Total bookings in Supabase: {allBookings.Count}");
                foreach (var b in allBookings)
                    System.Diagnostics.Debug.WriteLine(
                        $"[Debug] Booking: {b.FullName} | Status={b.Status} | Date={b.AppointmentDate}");
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
        [ObservableProperty] private string searchText = string.Empty;
        [ObservableProperty] private string currentSort = "All";

        public PatientListViewModel(DatabaseService db) => _db = db;

        [RelayCommand]
        async Task LoadPatients()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var list = await _db.GetPatients();
                _allPatients = list.Select(p => new PatientCardViewModel(p)).ToList();
                ApplyFilterAndSort();
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
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

        partial void OnSearchTextChanged(string value) => ApplyFilterAndSort();

        private void ApplyFilterAndSort()
        {
            var filtered = _allPatients.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.Trim().ToLower();
                filtered = filtered.Where(c =>
                    c.Patient.FullName.ToLower().Contains(term));
            }

            filtered = CurrentSort switch
            {
                "A-Z" => filtered.OrderBy(c => c.Patient.LastName).ThenBy(c => c.Patient.FirstName),
                "Z-A" => filtered.OrderByDescending(c => c.Patient.LastName).ThenByDescending(c => c.Patient.FirstName),
                "Recently Added" => filtered.OrderByDescending(c => c.Patient.PatientID),
                _ => filtered
            };

            Patients.Clear();
            foreach (var card in filtered)
                Patients.Add(card);
        }

        [RelayCommand]
        async Task ShowSortOptions()
        {
            string result = await Shell.Current.DisplayActionSheet(
                "Sort Patients", "Cancel", null,
                "All", "A-Z", "Z-A", "Recently Added");

            if (!string.IsNullOrEmpty(result) && result != "Cancel")
            {
                CurrentSort = result;
                ApplyFilterAndSort();
            }
        }

        // Opens the bottom action sheet when a card is tapped
        [RelayCommand]
        async Task OpenActionSheet(PatientCardViewModel card)
        {
            if (card is null) return;

            var sheet = new ItemActionSheet();
            sheet.Configure(
                title: card.Patient.FullName,
                subtitle: $"Patient ID: {card.Patient.PatientID:D3}",
                options: new[]
                {
                    new ActionSheetOption
                    {
                        Icon = "\ue09e",
                        Label = "Patient Records",
                        Subtitle = "View full patient details",
                        IconBackgroundColor = Color.FromArgb("#E8F5E9"),
                        IconColor = Color.FromArgb("#1A6B2F"),
                        OnTapped = async () =>
                            await Shell.Current.GoToAsync(
                                $"{nameof(PatientDetailsPage)}?id={card.Patient.PatientID}"),
                    },
                    new ActionSheetOption
                    {
                        Icon = "\ue0a6",
                        Label = "Dental Chart",
                        Subtitle = "View dental chart",
                        IconBackgroundColor = Color.FromArgb("#E8F5E9"),
                        IconColor = Color.FromArgb("#1A6B2F"),
                        OnTapped = async () =>
                            await Shell.Current.GoToAsync(
                                $"{nameof(DentalChartPage)}?patientId={card.Patient.PatientID}&patientName={Uri.EscapeDataString(card.Patient.FullName)}"),
                    },
                    new ActionSheetOption
                    {
                        Icon = "\ue0aa",
                        Label = "Cephalometric",
                        Subtitle = "View cephalometric analysis",
                        IconBackgroundColor = Color.FromArgb("#E8F5E9"),
                        IconColor = Color.FromArgb("#1A6B2F"),
                        OnTapped = async () =>
                            await Shell.Current.GoToAsync(
                                $"{nameof(CephalometricPage)}?PatientId={card.Patient.PatientID}&PatientName={Uri.EscapeDataString(card.Patient.FullName)}"),
                    },
                    new ActionSheetOption
                    {
                        Icon = "\ue889",
                        Label = "Treatment History",
                        Subtitle = "View past treatments",
                        IconBackgroundColor = Color.FromArgb("#E8F5E9"),
                        IconColor = Color.FromArgb("#1A6B2F"),
                        OnTapped = async () =>
                            await Shell.Current.GoToAsync(
                                $"{nameof(TreatmentHistoryPage)}?patientId={card.Patient.PatientID}&patientName={Uri.EscapeDataString(card.Patient.FullName)}"),
                    },
                    new ActionSheetOption
                    {
                        Icon = "\ue8f1",
                        Label = "Transaction History",
                        Subtitle = "Coming soon",
                        IconBackgroundColor = Color.FromArgb("#F5F5F5"),
                        IconColor = Color.FromArgb("#9E9E9E"),
                        OnTapped = async () =>
                            await Shell.Current.DisplayAlert("Coming Soon",
                                "Transaction History is not available yet.", "OK"),
                    },
                    new ActionSheetOption
                    {
                        Icon = "\ue872",
                        Label = "Delete Patient",
                        Subtitle = "Remove from records",
                        LabelColor = Colors.Crimson,
                        IconBackgroundColor = Color.FromArgb("#FFEBEE"),
                        IconColor = Colors.Crimson,
                        OnTapped = async () => await DeletePatient(card),
                    },
                });

            await sheet.ShowAsync();
        }

        // Call button — opens phone dialer
        [RelayCommand]
        async Task CallPatient(PatientCardViewModel card)
        {
            if (card is null || string.IsNullOrWhiteSpace(card.Patient.MobileNo)) return;
            try
            {
                if (PhoneDialer.Default.IsSupported)
                    PhoneDialer.Default.Open(card.Patient.MobileNo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Call] {ex.Message}");
            }
        }

        [RelayCommand]
        async Task GoToAddPatient() =>
            await Shell.Current.GoToAsync(nameof(AddPatientPage));

        [RelayCommand]
        async Task ViewPatient(PatientCardViewModel card)
        {
            if (card is null) return;
            await Shell.Current.GoToAsync($"{nameof(PatientDetailsPage)}?id={card.Patient.PatientID}");
        }

        [RelayCommand]
        async Task EditPatient(PatientCardViewModel card)
        {
            if (card is null) return;
            await Shell.Current.GoToAsync($"{nameof(AddPatientPage)}?PatientId={card.Patient.PatientID}");
        }

        [RelayCommand]
        async Task DeletePatient(PatientCardViewModel card)
        {
            if (card is null) return;

            bool answer = await Shell.Current.DisplayAlert(
                "Confirm Delete",
                $"Are you sure you want to delete {card.Patient.FullName}?",
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
            if (card is null) return;
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
                $"{nameof(CephalometricPage)}?PatientId={card.Patient.PatientID}&PatientName={Uri.EscapeDataString(card.Patient.FullName)}");
        }

        [RelayCommand]
        async Task ViewTreatmentHistory(PatientCardViewModel card)
        {
            if (card is null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(TreatmentHistoryPage)}?patientId={card.Patient.PatientID}" +
                $"&patientName={Uri.EscapeDataString(card.Patient.FirstName + " " + card.Patient.LastName)}");
        }

        [RelayCommand]
        async Task ViewTransactions(PatientCardViewModel card)
        {
            if (card == null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(TransactionPage)}" +
                $"?patientId={card.Patient.SupabaseId}" +
                $"&patientName={Uri.EscapeDataString(card.Patient.FullName)}");
        }
    }
}