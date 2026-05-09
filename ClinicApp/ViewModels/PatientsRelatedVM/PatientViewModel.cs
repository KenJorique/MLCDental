using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels;

public partial class PatientViewModel : ObservableObject
{
    readonly DatabaseService _databaseService;

    [ObservableProperty]
    string firstName;

    [ObservableProperty]
    string lastName;

    [ObservableProperty]
    Patient selectedPatient;
    public ObservableCollection<Patient> Patients { get; set; }
        = new();

    public PatientViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    [RelayCommand]
    async Task AddPatient()
    {
        if (string.IsNullOrWhiteSpace(FirstName))
            return;

        Patient patient = new()
        {
            FirstName = FirstName,
            LastName = LastName
        };

        await _databaseService.AddPatient(patient);

        await LoadPatients();
    }

    [RelayCommand]
    async Task LoadPatients()
    {
        Patients.Clear();

        var patients = await _databaseService.GetPatients();

        foreach (var p in patients)
            Patients.Add(p);
    }

    [RelayCommand]
    async Task UpdatePatient()
    {
        if (selectedPatient == null)
            return;

        if (string.IsNullOrWhiteSpace(selectedPatient.FirstName))
            return;

        await _databaseService.UpdatePatient(selectedPatient);

        await LoadPatients();
    }

    [RelayCommand]
    async Task DeletePatient(Patient patient)
    {
        if (patient == null)
            return;

        await _databaseService.DeletePatient(patient);

        await LoadPatients();
    }

    [RelayCommand]
    async Task GoToServicePage()
    {
        await Shell.Current.GoToAsync(nameof(ServicePage));
    }
}