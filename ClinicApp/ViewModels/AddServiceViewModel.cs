using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels;

// QueryProperties handle both service edit (ServiceId) and package edit (PackageId)
[QueryProperty(nameof(ServiceId), "ServiceId")]
[QueryProperty(nameof(PackageId), "PackageId")]
public partial class AddServiceViewModel : ObservableObject
{
    readonly DatabaseService _db;

    public AddServiceViewModel(DatabaseService db)
    {
        _db = db;
    }

    // ─── Tab state ───────────────────────────────────────────
    // True = Service tab active, False = Package tab active
    [ObservableProperty] bool isServiceTabSelected = true;
    [ObservableProperty] bool isPackageTabSelected = false;
    [ObservableProperty] string pageTitle = "Add Service";

    // ─── Service tab fields ──────────────────────────────────
    [ObservableProperty] int serviceId;
    [ObservableProperty] string? serviceName;
    [ObservableProperty] double servicePrice;
    [ObservableProperty] string? serviceDescription;

    // ─── Package tab fields ──────────────────────────────────
    [ObservableProperty] int packageId;
    [ObservableProperty] string? packageName;
    [ObservableProperty] double packagePrice;
    [ObservableProperty] string? packageDescription;

    // List of existing services shown as checkboxes in Package tab
    public ObservableCollection<ServiceCheckboxItem> AvailableServices { get; set; } = new();

    // ─── Tab switching commands ──────────────────────────────

    [RelayCommand]
    void SelectServiceTab()
    {
        IsServiceTabSelected = true;
        IsPackageTabSelected = false;
        PageTitle = ServiceId > 0 ? "Edit Service" : "Add Service";
    }

    [RelayCommand]
    void SelectPackageTab()
    {
        IsServiceTabSelected = false;
        IsPackageTabSelected = true;
        PageTitle = PackageId > 0 ? "Edit Package" : "Add Package";

        // Load available services for checkboxes when switching to Package tab
        LoadAvailableServices();
    }

    // Loads existing services into the checkbox list for the Package tab
    private async void LoadAvailableServices()
    {
        AvailableServices.Clear();
        var services = await _db.GetServices();
        foreach (var s in services)
            AvailableServices.Add(new ServiceCheckboxItem(s));
    }

    // ─── Edit mode: pre-fill Service fields ─────────────────

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

    // ─── Edit mode: pre-fill Package fields ─────────────────

    partial void OnPackageIdChanged(int value)
    {
        if (value > 0)
        {
            PageTitle = "Edit Package";
            // Switch to package tab automatically when editing a package
            IsServiceTabSelected = false;
            IsPackageTabSelected = true;
            LoadPackageData(value);
        }
    }

    private async void LoadPackageData(int id)
    {
        var list = await _db.GetServicePackages();
        var package = list.FirstOrDefault(p => p.PackageID == id);
        if (package != null)
        {
            PackageName = package.PackageName;
            PackagePrice = package.Price;
            PackageDescription = package.Description;

            // Pre-tick the checkboxes for included services
            await LoadAvailableServicesAsync();
            var included = (package.IncludedServices ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in AvailableServices)
                item.IsSelected = included.Contains(item.Service.ServiceName);
        }
    }

    private async Task LoadAvailableServicesAsync()
    {
        AvailableServices.Clear();
        var services = await _db.GetServices();
        foreach (var s in services)
            AvailableServices.Add(new ServiceCheckboxItem(s));
    }

    // ─── Save command ────────────────────────────────────────

    [RelayCommand]
    async Task Save()
    {
        if (IsServiceTabSelected)
            await SaveService();
        else
            await SavePackage();
    }

    private async Task SaveService()
    {
        // Basic validation
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
            // Update existing service
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
            // Add new service
            await _db.AddService(new ServiceModel
            {
                ServiceName = ServiceName,
                Price = ServicePrice,
                Description = ServiceDescription
            });
        }

        await Shell.Current.GoToAsync("..");
    }

    private async Task SavePackage()
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(PackageName))
        {
            await Shell.Current.DisplayAlert("Validation", "Package name is required.", "OK");
            return;
        }
        if (PackagePrice <= 0)
        {
            await Shell.Current.DisplayAlert("Validation", "Please enter a valid price.", "OK");
            return;
        }

        // Combine selected service names into a comma-separated string
        var selected = AvailableServices
            .Where(s => s.IsSelected)
            .Select(s => s.Service.ServiceName)
            .ToList();
        string includedServices = string.Join(",", selected);

        if (PackageId > 0)
        {
            // Update existing package
            var list = await _db.GetServicePackages();
            var package = list.FirstOrDefault(p => p.PackageID == PackageId);
            if (package != null)
            {
                package.PackageName = PackageName;
                package.Price = PackagePrice;
                package.Description = PackageDescription;
                package.IncludedServices = includedServices;
                await _db.UpdateServicePackage(package);
            }
        }
        else
        {
            // Add new package
            await _db.AddServicePackage(new ServicePackage
            {
                PackageName = PackageName,
                Price = PackagePrice,
                Description = PackageDescription,
                IncludedServices = includedServices
            });
        }

        await Shell.Current.GoToAsync("..");
    }
}
