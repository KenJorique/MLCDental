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
    readonly DatabaseService _db;

    // List of individual services shown in the Services section
    public ObservableCollection<ServiceModel> Services { get; set; } = new();

    // List of service packages shown in the Service Packages section
    public ObservableCollection<ServicePackage> Packages { get; set; } = new();

    public ServiceViewModel(DatabaseService db)
    {
        _db = db;
    }

    // Loads both services and packages from the database
    [RelayCommand]
    async Task LoadServices()
    {
        try
        {
            Services.Clear();
            var serviceList = await _db.GetServices();
            foreach (var s in serviceList)
                Services.Add(s);

            Packages.Clear();
            var packageList = await _db.GetServicePackages();
            foreach (var p in packageList)
                Packages.Add(p);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Database Error: {ex.Message}");
        }
    }

    // ─── Service actions ─────────────────────────────────────

    // Navigates to AddServicePage for editing an existing service
    [RelayCommand]
    async Task UpdateService(ServiceModel service)
    {
        if (service == null) return;
        await Shell.Current.GoToAsync($"{nameof(AddServicePage)}?ServiceId={service.ServiceID}");
    }

    // Deletes a service after confirmation
    [RelayCommand]
    async Task DeleteService(ServiceModel service)
    {
        if (service == null) return;

        bool answer = await Shell.Current.DisplayAlert("Confirm Delete",
            $"Are you sure you want to delete {service.ServiceName}?", "Yes", "No");

        if (answer)
        {
            await _db.DeleteService(service);
            await LoadServices();
        }
    }

    // ─── Package actions ─────────────────────────────────────

    // Navigates to AddServicePage (Package tab) for editing an existing package
    [RelayCommand]
    async Task UpdatePackage(ServicePackage package)
    {
        if (package == null) return;
        await Shell.Current.GoToAsync($"{nameof(AddServicePage)}?PackageId={package.PackageID}");
    }

    // Deletes a package after confirmation
    [RelayCommand]
    async Task DeletePackage(ServicePackage package)
    {
        if (package == null) return;

        bool answer = await Shell.Current.DisplayAlert("Confirm Delete",
            $"Are you sure you want to delete {package.PackageName}?", "Yes", "No");

        if (answer)
        {
            await _db.DeleteServicePackage(package);
            await LoadServices();
        }
    }

    // Navigates to AddServicePage (opens on Service tab by default)
    [RelayCommand]
    async Task GoToAddService()
    {
        await Shell.Current.GoToAsync(nameof(AddServicePage));
    }
}
