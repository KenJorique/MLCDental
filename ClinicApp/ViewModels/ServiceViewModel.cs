using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.ServicesRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ClinicApp.ViewModels;

public partial class ServiceViewModel : ObservableObject
{
    readonly DatabaseService _databaseService;

    public ObservableCollection<ServiceModel> Services { get; set; } = new();

    public ServiceViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
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
        await Shell.Current.GoToAsync($"{nameof(AddServicePage)}?ServiceId={service.ServiceID}");
    }

    [RelayCommand]
    async Task DeleteService(ServiceModel service)
    {
        if (service == null) return;

        bool answer = await Shell.Current.DisplayAlert("Confirm Delete",
            $"Are you sure you want to delete {service.ServiceName}?", "Yes", "No");

        if (answer)
        {
            await _databaseService.DeleteService(service);
            await LoadServices();
        }
    }

    [RelayCommand]
    async Task GoToAddService()
    {
        await Shell.Current.GoToAsync(nameof(AddServicePage));
    }
}
