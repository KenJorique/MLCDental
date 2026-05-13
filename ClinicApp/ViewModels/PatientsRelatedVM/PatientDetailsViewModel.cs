using ClinicApp.Services;
using ClinicApp.Views.PatientsRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.PatientsRelatedVM
{
    // Receives the patient ID from navigation query parameter
    [QueryProperty(nameof(PatientId), "id")]
    public partial class PatientDetailsViewModel : ObservableObject
    {
        readonly DatabaseService _db;

        public PatientDetailsViewModel(DatabaseService db)
        {
            _db = db;
        }

        [ObservableProperty] int patientId;
        [ObservableProperty] string fullName;
        [ObservableProperty] string contactNumber;
        [ObservableProperty] string address;
        [ObservableProperty] string medicalHistory;
        [ObservableProperty] string email;
        [ObservableProperty] bool gender;
        [ObservableProperty] DateTime dateOfBirth;
        [ObservableProperty] DateTime dateRegistered;

        partial void OnPatientIdChanged(int value)
        {
            if (value > 0)
                LoadPatient(value);
        }

        // Loads patient data from the database and populates the page fields
        private async void LoadPatient(int id)
        {
            var patient = await _db.GetPatientById(id);
            if (patient != null)
            {
                FullName = $"{patient.FirstName} {patient.LastName}";
                Gender = patient.Gender;
                DateOfBirth = patient.DateOfBirth;
                ContactNumber = patient.ContactNumber;
                Address = patient.Address;
                Email = patient.Email;
                MedicalHistory = patient.HasNoMedicalHistory
                    ? "No prior medical history"
                    : patient.MedicalHistory;
               
               
                DateRegistered = patient.DateRegistered;
            }
        }

        // Shows the meatball menu (⋮) action sheet with Edit and Delete options
        [RelayCommand]
        async Task ShowMenu()
        {
            string action = await Shell.Current.DisplayActionSheet(
                "Options", "Cancel", null, "Edit", "Delete");

            if (action == "Edit")
            {
                // Navigate to AddPatientPage pre-filled with this patient's data
                await Shell.Current.GoToAsync($"{nameof(AddPatientPage)}?PatientId={PatientId}");
            }
            else if (action == "Delete")
            {
                // Confirm before deleting
                bool confirm = await Shell.Current.DisplayAlert(
                    "Confirm Delete",
                    $"Are you sure you want to delete {FullName}?",
                    "Yes", "No");

                if (confirm)
                {
                    var patient = await _db.GetPatientById(PatientId);
                    if (patient != null)
                    {
                        await _db.DeletePatient(patient);
                        // Go back to patient list after deletion
                        await Shell.Current.GoToAsync("..");
                    }
                }
            }
        }
    }
}
