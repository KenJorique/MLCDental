using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;

namespace ClinicApp.ViewModels;

[QueryProperty(nameof(ServiceId), "ServiceId")]
public partial class AddServiceViewModel : ObservableObject
{
    readonly DatabaseService _db;

    public AddServiceViewModel(DatabaseService db)
    {
        _db = db;
    }

    [ObservableProperty] int serviceId;
    [ObservableProperty] string pageTitle = "Add Service";
    [ObservableProperty] string serviceName;
    [ObservableProperty] double price;
    [ObservableProperty] string description;

    partial void OnServiceIdChanged(int value)
    {
        if (value > 0)
        {
            PageTitle = "Edit Service";
            LoadServiceData(value);
        }
    }

    private async void LoadServiceData(int id)
    {
        var list = await _db.GetServices();
        var service = list.FirstOrDefault(s => s.ServiceID == id);
        if (service != null)
        {
            ServiceName = service.ServiceName;
            Price = service.Price;
            Description = service.Description;
        }
    }

    [RelayCommand]
    async Task SaveService()
    {
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

        if (ServiceId > 0)
        {
            // Update existing
            var list = await _db.GetServices();
            var service = list.FirstOrDefault(s => s.ServiceID == ServiceId);
            if (service != null)
            {
                service.ServiceName = ServiceName;
                service.Price = Price;
                service.Description = Description;
                await _db.UpdateService(service);
            }
        }
        else
        {
            // Add new
            await _db.AddService(new ServiceModel
            {
                ServiceName = ServiceName,
                Price = Price,
                Description = Description
            });
        }

        await Shell.Current.GoToAsync("..");
    }
}
