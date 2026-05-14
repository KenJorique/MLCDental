using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.PatientsRelatedVM
{
    [QueryProperty(nameof(PatientId), "PatientId")]
    public partial class AddPatientViewModel : ObservableObject
    {
        readonly DatabaseService _db;

        public AddPatientViewModel(DatabaseService db)
        {
            _db = db;
        }

        [ObservableProperty] int patientId;
        [ObservableProperty] string pageTitle = "Add New Patient";
        [ObservableProperty] string firstName;
        [ObservableProperty] string lastName;
        [ObservableProperty] string email;
        [ObservableProperty] bool gender;
        [ObservableProperty] string dateOfBirth;
        [ObservableProperty] string dateRegistered;
        [ObservableProperty] string address;
        [ObservableProperty] string contactNumber;  // fixed: string to match Patient model
        [ObservableProperty] string medicalHistory;
        [ObservableProperty] bool hasNoMedicalHistory;

        partial void OnPatientIdChanged(int value)
        {
            if (value > 0)
            {
                PageTitle = "Edit Patient";
                LoadPatientData(value);
            }
        }

        private async void LoadPatientData(int id)
        {
            var patient = await _db.GetPatientById(id);
            if (patient != null)    
            {
                FirstName = patient.FirstName;
                LastName = patient.LastName;
                Address = patient.Address;
                ContactNumber = patient.ContactNumber;
                MedicalHistory = patient.MedicalHistory;
                HasNoMedicalHistory = patient.HasNoMedicalHistory;
                Email = patient.Email;
                Gender = patient.Gender;
                DateOfBirth = patient.DateOfBirth;
                DateRegistered = patient.DateRegistered;
            }
        }

        [RelayCommand]
        async Task SavePatient()
        {
            if (PatientId > 0)
            {
                // Update existing
                var p = await _db.GetPatientById(PatientId);
                p.FirstName = FirstName;
                p.LastName = LastName;
                p.Address = Address;
                p.ContactNumber = ContactNumber;
                p.MedicalHistory = MedicalHistory;
                p.HasNoMedicalHistory = HasNoMedicalHistory;
                p.Email = Email;
                p.Gender = Gender;
                p.DateOfBirth = DateOfBirth;
                p.DateRegistered = DateRegistered;
                await _db.UpdatePatient(p);
            }
            else
            {
                // Add new — all fields saved
                await _db.AddPatient(new Patient
                {
                    FirstName = FirstName,
                    LastName = LastName,
                    Address = Address,
                    ContactNumber = ContactNumber,
                    MedicalHistory = MedicalHistory,
                    HasNoMedicalHistory = HasNoMedicalHistory,
                    Email = Email,
                    Gender = Gender,
                    DateOfBirth = DateOfBirth,
                    DateRegistered = DateRegistered
                });
            }
            await Shell.Current.GoToAsync("..");
        }
    }
}
