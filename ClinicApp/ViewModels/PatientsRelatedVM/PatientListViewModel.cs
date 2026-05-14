using ClinicApp.Models;
using ClinicApp.Services;
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

        // Holds the list of wrapped patient cards (with expand/collapse state)
        public ObservableCollection<PatientCardViewModel> Patients { get; set; } = new();

        public PatientListViewModel(DatabaseService db)
        {
            _db = db;
            // NOTE: Do NOT call LoadPatients() here.
            // PatientListPage.OnAppearing() handles the initial load,
            // calling it here too causes duplicates on startup.
        }

        // Clears and reloads the patient list from the database
        [RelayCommand]
        async Task LoadPatients()
        {
            Patients.Clear();
            var list = await _db.GetPatients();
            foreach (var p in list)
                Patients.Add(new PatientCardViewModel(p));
        }

        // Toggles the card expansion. Collapses all others first (only one open at a time).
        [RelayCommand]
        void ToggleExpand(PatientCardViewModel card)
        {
            if (card == null) return;

            bool wasExpanded = card.IsExpanded;

            // Collapse all cards
            foreach (var c in Patients)
                c.IsExpanded = false;

            // If it was collapsed before tapping, expand it now
            if (!wasExpanded)
                card.IsExpanded = true;
        }

        // Navigates to AddPatientPage for adding a new patient
        [RelayCommand]
        async Task GoToAddPatient()
        {
            await Shell.Current.GoToAsync(nameof(AddPatientPage));
        }

        // Navigates to PatientDetailsPage (Patient Records) with the patient's ID
        [RelayCommand]
        async Task ViewPatient(PatientCardViewModel card)
        {
            if (card == null) return;
            await Shell.Current.GoToAsync($"{nameof(PatientDetailsPage)}?id={card.Patient.PatientID}");
        }

        // Navigates to AddPatientPage pre-filled with the selected patient's data
        [RelayCommand]
        async Task EditPatient(PatientCardViewModel card)
        {
            if (card == null) return;
            await Shell.Current.GoToAsync($"{nameof(AddPatientPage)}?PatientId={card.Patient.PatientID}");
        }

        // Deletes a patient after a confirmation dialog
        [RelayCommand]
        async Task DeletePatient(PatientCardViewModel card)
        {
            if (card == null) return;

            bool answer = await Shell.Current.DisplayAlert("Confirm Delete",
                $"Are you sure you want to delete {card.Patient.FirstName} {card.Patient.LastName}?",
                "Yes", "No");

            if (answer)
            {
                await _db.DeletePatient(card.Patient);
                await LoadPatients();
            }
        }

        [RelayCommand]
        async Task ViewDentalChart(PatientCardViewModel card)
        {
            if (card == null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(DentalChartPage)}?patientId={card.Patient.PatientID}&patientName={Uri.EscapeDataString(card.Patient.FirstName + " " + card.Patient.LastName)}");
        }

    }
}
