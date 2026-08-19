using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClinicApp.ViewModels.ServicesRelatedVM;

// Wraps a SupabaseService with expand/collapse UI state for the ServiceListPage card
public partial class ServiceCardViewModel : ObservableObject
{
    public SupabaseService Service { get; }

    // Controls whether the description is visible
    [ObservableProperty]
    bool isExpanded;

    public ServiceCardViewModel(SupabaseService service)
    {
        Service = service;
    }

    // Flat passthroughs so XAML bindings stay simple
    public string ServiceName => Service.Name; 
    public string PriceDisplay => Service.PriceDisplay;

    public string? Description => Service.Description;
}