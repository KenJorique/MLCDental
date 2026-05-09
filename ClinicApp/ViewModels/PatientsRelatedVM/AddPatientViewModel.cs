using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ClinicApp.ViewModels.PatientsRelatedVM
{
    // 1. Add this attribute to catch the ID from the URL
    [QueryProperty(nameof(PatientId), "PatientId")]
    public partial class AddPatientViewModel : ObservableObject
    {
        readonly DatabaseService _db;

        public AddPatientViewModel(DatabaseService db)
        {
            _db = db;
        }

        [ObservableProperty]
        int patientId; // The ID caught from navigation

        [ObservableProperty]
        string firstName;

        [ObservableProperty]
        string lastName;

        [ObservableProperty] string address;

        [ObservableProperty] int contactNumber;

        [ObservableProperty] string medicalHistory;

        [ObservableProperty] bool hasNoMedicalHistory;
        // This runs automatically when PatientId is set via navigation
        partial void OnPatientIdChanged(int value)
        {
            if (value > 0)
            {
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
                ContactNumber = Convert.ToInt32(patient.ContactNumber);
                MedicalHistory = patient.MedicalHistory;
                HasNoMedicalHistory = Convert.ToBoolean(patient.HasNoMedicalHistory);
            }
        }

        [RelayCommand]
        async Task SavePatient()
        {
            if (PatientId > 0)
            {
                // Update existing logic
                var p = await _db.GetPatientById(PatientId);
                p.FirstName = FirstName;
                p.LastName = LastName;
                p.Address = Address;
                p.MedicalHistory = MedicalHistory;
                p.HasNoMedicalHistory = HasNoMedicalHistory;
                await _db.UpdatePatient(p);
            }
            else
            {
                // Add new logic
                await _db.AddPatient(new Patient { FirstName = FirstName, LastName = LastName });
            }
            await Shell.Current.GoToAsync("..");
        }
    }
}