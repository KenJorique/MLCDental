using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.PatientsRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClinicApp.ViewModels.PatientsRelatedVM
{
    public partial class PatientListViewModel : ObservableObject
    {
        readonly DatabaseService _db;

        public ObservableCollection<Patient> Patients { get; set; } = new();

        public PatientListViewModel(DatabaseService db)
        {
            _db = db;
            LoadPatients();
        }

        [RelayCommand]
        async Task LoadPatients()
        {
            Patients.Clear();

            var list = await _db.GetPatients();

            foreach (var p in list)
                Patients.Add(p);
        }

        [RelayCommand]
        async Task GoToAddPatient()
        {
            await Shell.Current.GoToAsync(nameof(AddPatientPage));
        }

        [RelayCommand]
        async Task DeletePatient(Patient p)
        {
            if (p == null) return;

            // 1. Confirm Message
            bool answer = await Shell.Current.DisplayAlert("Confirm Delete",
                $"Are you sure you want to delete {p.FirstName} {p.LastName}?", "Yes", "No");

            if (answer)
            {
                await _db.DeletePatient(p);
                await LoadPatients();
            }
        }

        [RelayCommand]
        async Task ViewPatient(Patient p)
        {
            await Shell.Current.GoToAsync($"{nameof(PatientDetailsPage)}?id={p.PatientID}");
        }

        [RelayCommand]
        async Task EditPatient(Patient p)
        {
            if (p == null) return;

            // Passing the ID as a query parameter
            // The AddPatientPage/ViewModel must be configured to handle this ID
            await Shell.Current.GoToAsync($"{nameof(AddPatientPage)}?PatientId={p.PatientID}");
        }
    }
}
