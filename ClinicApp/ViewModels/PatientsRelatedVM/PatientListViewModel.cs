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

        // Full unfiltered list — source for search and sort
        private List<PatientCardViewModel> _allPatients = new();

        // Displayed list bound to the UI
        public ObservableCollection<PatientCardViewModel> Patients { get; set; } = new();

        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private bool isRefreshing;

        // Search text — filters by patient full name as user types
        [ObservableProperty] private string searchText = string.Empty;

        // Label shown on the sort button
        [ObservableProperty] private string currentSort = "All";

        public PatientListViewModel(DatabaseService db)
        {
            _db = db;
            // NOTE: Do NOT call LoadPatients() here.
            // PatientListPage.OnAppearing() handles the initial load.
        }

        // Reloads from database then applies current filter/sort
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

        // Re-filter whenever search text changes
        partial void OnSearchTextChanged(string value) => ApplyFilterAndSort();

        // Applies search filter + current sort and updates the Patients collection
        private void ApplyFilterAndSort()
        {
            var filtered = _allPatients.AsEnumerable();

            // Filter by full name
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.Trim().ToLower();
                filtered = filtered.Where(c =>
                    c.Patient.FullName.ToLower().Contains(term));
            }

            // Apply sort
            filtered = CurrentSort switch
            {
                "A-Z" => filtered.OrderBy(c => c.Patient.LastName).ThenBy(c => c.Patient.FirstName),
                "Z-A" => filtered.OrderByDescending(c => c.Patient.LastName).ThenByDescending(c => c.Patient.FirstName),
                "Recently Added" => filtered.OrderByDescending(c => c.Patient.PatientID),
                _ => filtered // "All" = original DB insert order
            };

            Patients.Clear();
            foreach (var card in filtered)
                Patients.Add(card);
        }

        // Shows sort options and updates CurrentSort label
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

        // Toggles card expansion — only one open at a time
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

        // Navigates to AddPatientPage for a new patient
        [RelayCommand]
        async Task GoToAddPatient()
        {
            await Shell.Current.GoToAsync(nameof(AddPatientPage));
        }

        // Navigates to PatientDetailsPage (Patient Records)
        [RelayCommand]
        async Task ViewPatient(PatientCardViewModel card)
        {
            if (card == null) return;
            await Shell.Current.GoToAsync($"{nameof(PatientDetailsPage)}?id={card.Patient.PatientID}");
        }

        // Navigates to AddPatientPage pre-filled for editing
        [RelayCommand]
        async Task EditPatient(PatientCardViewModel card)
        {
            if (card == null) return;
            await Shell.Current.GoToAsync($"{nameof(AddPatientPage)}?PatientId={card.Patient.PatientID}");
        }

        // Deletes a patient after confirmation
        [RelayCommand]
        async Task DeletePatient(PatientCardViewModel card)
        {
            if (card == null) return;

            bool answer = await Shell.Current.DisplayAlert("Confirm Delete",
                $"Are you sure you want to delete {card.Patient.FirstName} {card.Patient.LastName}?",
                "Yes", "No");

            if (answer)
            {
                try { await _db.DeletePatient(card.Patient); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Delete] {ex.Message}"); }
                await MainThread.InvokeOnMainThreadAsync(async () => await LoadPatients());
            }
        }

        // Navigates to DentalChartPage
        [RelayCommand]
        async Task ViewDentalChart(PatientCardViewModel card)
        {
            if (card == null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(DentalChartPage)}?patientId={card.Patient.PatientID}&patientName={Uri.EscapeDataString(card.Patient.FullName)}");
        }

        // Navigates to CephalometricPage
        [RelayCommand]
        async Task GoToCephalometric(PatientCardViewModel card)
        {
            if (card == null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(CephalometricPage)}?PatientId={card.Patient.PatientID}&PatientName={Uri.EscapeDataString(card.Patient.FullName)}");
        }

        // Navigates to TreatmentHistoryPage
        [RelayCommand]
        async Task ViewTreatmentHistory(PatientCardViewModel card)
        {
            if (card == null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(TreatmentHistoryPage)}?patientId={card.Patient.PatientID}&patientName={Uri.EscapeDataString(card.Patient.FullName)}");
        }
    }
}
