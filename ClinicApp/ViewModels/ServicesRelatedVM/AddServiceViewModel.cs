using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.ServicesRelatedVM;

[QueryProperty(nameof(ServiceId), "ServiceId")]
public partial class AddServiceViewModel : ObservableObject
{
    readonly DatabaseService _db;

    public AddServiceViewModel(DatabaseService db) => _db = db;

    [ObservableProperty] string pageTitle = "Add Service";
    [ObservableProperty] int serviceId;
    [ObservableProperty] string? serviceName;
    [ObservableProperty] double servicePrice;
    [ObservableProperty] string? serviceDescription;

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
            ServicePrice = service.Price;
            ServiceDescription = service.Description;
        }
    }

    [RelayCommand]
    async Task Save()
    {
        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            await Shell.Current.DisplayAlert("Validation", "Service name is required.", "OK");
            return;
        }
        if (ServicePrice <= 0)
        {
            await Shell.Current.DisplayAlert("Validation", "Please enter a valid price.", "OK");
            return;
        }

        if (ServiceId > 0)
        {
            var list = await _db.GetServices();
            var service = list.FirstOrDefault(s => s.ServiceID == ServiceId);
            if (service != null)
            {
                service.ServiceName = ServiceName;
                service.Price = ServicePrice;
                service.Description = ServiceDescription;
                await _db.UpdateService(service);
            }
        }
        else
        {
            await _db.AddService(new ServiceModel
            {
                ServiceName = ServiceName,
                Price = ServicePrice,
                Description = ServiceDescription
            });
        }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    async Task Cancel() => await Shell.Current.GoToAsync("..");
}
