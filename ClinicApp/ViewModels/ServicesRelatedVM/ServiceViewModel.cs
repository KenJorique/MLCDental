using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.ServicesRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ClinicApp.ViewModels.ServicesRelatedVM;

public partial class ServiceViewModel : ObservableObject
{
    readonly DatabaseService _db;

    // Wrapped service cards with expand/collapse state
    public ObservableCollection<ServiceCardViewModel> ServiceCards { get; set; } = new();

    // Wrapped package cards with expand/collapse state
    public ObservableCollection<PackageCardViewModel> PackageCards { get; set; } = new();

    // Controls Service Packages section visibility (hidden if no packages exist)
    [ObservableProperty]
    bool hasPackages;

    // True if there are 2 or more services — enables the Package tab on Add page
    [ObservableProperty]
    bool canAddPackage;

    public ServiceViewModel(DatabaseService db)
    {
        _db = db;
    }

    // Loads both services and packages, wraps them in card ViewModels
    [RelayCommand]
    async Task LoadServices()
    {
        try
        {
            ServiceCards.Clear();
            var serviceList = await _db.GetServices();
            foreach (var s in serviceList)
                ServiceCards.Add(new ServiceCardViewModel(s));

            // Package tab is only enabled when there are 2 or more services
            CanAddPackage = serviceList.Count >= 2;

            PackageCards.Clear();
            var packageList = await _db.GetServicePackages();
            foreach (var p in packageList)
                PackageCards.Add(new PackageCardViewModel(p));

            // Show the Service Packages section only if at least one package exists
            HasPackages = packageList.Count > 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Database Error: {ex.Message}");
        }
    }

    // Toggles a service card. Collapses ALL cards (both services and packages) first.
    [RelayCommand]
    void ToggleServiceCard(ServiceCardViewModel card)
    {
        if (card == null) return;

        bool wasExpanded = card.IsExpanded;

        // Collapse everything across both lists
        CollapseAll();

        // If it was collapsed before tapping, expand it now
        if (!wasExpanded)
            card.IsExpanded = true;
    }

    // Toggles a package card. Collapses ALL cards (both services and packages) first.
    [RelayCommand]
    void TogglePackageCard(PackageCardViewModel card)
    {
        if (card == null) return;

        bool wasExpanded = card.IsExpanded;

        // Collapse everything across both lists
        CollapseAll();

        // If it was collapsed before tapping, expand it now
        if (!wasExpanded)
            card.IsExpanded = true;
    }

    // Collapses all service and package cards
    private void CollapseAll()
    {
        foreach (var s in ServiceCards)
            s.IsExpanded = false;
        foreach (var p in PackageCards)
            p.IsExpanded = false;
    }

    // ─── Service swipe actions ────────────────────────────────

    // Navigates to AddServicePage pre-filled with the selected service
    [RelayCommand]
    async Task EditService(ServiceCardViewModel card)
    {
        if (card == null) return;
        await Shell.Current.GoToAsync($"{nameof(AddServicePage)}?ServiceId={card.Service.ServiceID}");
    }

    // Deletes a service after confirmation, then reloads the list
    [RelayCommand]
    async Task DeleteService(ServiceCardViewModel card)
    {
        if (card == null) return;

        bool answer = await Shell.Current.DisplayAlert("Confirm Delete",
            $"Are you sure you want to delete {card.Service.ServiceName}?", "Yes", "No");

        if (answer)
        {
            await _db.DeleteService(card.Service);
            await LoadServices();
        }
    }

    // ─── Package swipe actions ────────────────────────────────

    // Navigates to AddServicePage (Package tab) pre-filled with the selected package
    [RelayCommand]
    async Task EditPackage(PackageCardViewModel card)
    {
        if (card == null) return;
        await Shell.Current.GoToAsync($"{nameof(AddServicePage)}?PackageId={card.Package.PackageID}");
    }

    // Deletes a package after confirmation, then reloads the list
    [RelayCommand]
    async Task DeletePackage(PackageCardViewModel card)
    {
        if (card == null) return;

        bool answer = await Shell.Current.DisplayAlert("Confirm Delete",
            $"Are you sure you want to delete {card.Package.PackageName}?", "Yes", "No");

        if (answer)
        {
            await _db.DeleteServicePackage(card.Package);
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
