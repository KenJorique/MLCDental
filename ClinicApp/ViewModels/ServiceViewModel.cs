using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views;
using ClinicApp.Views.ServicesRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ClinicApp.ViewModels;

public partial class ServiceViewModel : ObservableObject
{
    readonly DatabaseService _databaseService;

    [ObservableProperty]
    string serviceName;

    [ObservableProperty]
    double price;

    [ObservableProperty]
    string description;

    [ObservableProperty]
    ServiceModel selectedService;

    public ObservableCollection<ServiceModel> Services { get; set; }
        = new();

    public ServiceViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    [RelayCommand]
    async Task AddService()
    {
        // 1. Validation Restrictions
        if (string.IsNullOrWhiteSpace(ServiceName) || string.IsNullOrWhiteSpace(Description))
        {
            await Shell.Current.DisplayAlert("Validation Error", "All fields are required.", "OK");
            return;
        }

        if (Price <= 0)
        {
            await Shell.Current.DisplayAlert("Validation Error", "Please enter a valid price.", "OK");
            return;
        }

        try
        {
            var service = new ServiceModel
            {
                ServiceName = ServiceName,
                Price = Price,
                Description = Description
            };

            await _databaseService.AddService(service);

            // 2. Feedback and UI Reset
            await Shell.Current.DisplayAlert("Success", "Service added successfully!", "OK");

            // Clear inputs after saving
            ServiceName = string.Empty;
            Price = 0;
            Description = string.Empty;

            await LoadServices();
            await Shell.Current.GoToAsync("..");

        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    async Task LoadServices()
    {
        try
        {
            Services.Clear();
            var list = await _databaseService.GetServices();
            foreach (var s in list)
                Services.Add(s);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Database Error: {ex.Message}");
        }
    }

    [RelayCommand]
    async Task UpdateService(ServiceModel service)
    {
        if (service == null) return;

        // For now, this sets the fields so you can edit them in the Add page
        // Or you can navigate to a specific EditServicePage
        ServiceName = service.ServiceName;
        Price = service.Price;
        Description = service.Description;
        SelectedService = service;

        await Shell.Current.GoToAsync(nameof(AddServicePage));
    }

    [RelayCommand]
    async Task DeleteService(ServiceModel service)
    {
        if (service == null) return;

        // Optional: Add a confirmation dialog
        bool answer = await Shell.Current.DisplayAlert("Confirm Delete",
            $"Are you sure you want to delete {service.ServiceName}?", "Yes", "No");

        if (answer)
        {
            await _databaseService.DeleteService(service);
            await LoadServices(); // Refresh the list
        }
    }

    [RelayCommand]
    async Task GoToPatientPage()
    {
        await Shell.Current.GoToAsync(nameof(PatientPage));
    }

    [RelayCommand]
    async Task GoToAddService()
    {
        // This navigates to the Add page and hides the TabBar
        await Shell.Current.GoToAsync(nameof(AddServicePage));
    }

  

}