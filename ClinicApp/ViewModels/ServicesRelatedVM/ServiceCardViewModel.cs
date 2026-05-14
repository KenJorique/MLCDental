using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClinicApp.ViewModels.ServicesRelatedVM;

// Wraps a ServiceModel with expand/collapse UI state for the ServiceListPage card
public partial class ServiceCardViewModel : ObservableObject
{
    public ServiceModel Service { get; }

    // Controls whether the description is visible
    [ObservableProperty]
    bool isExpanded;

    public ServiceCardViewModel(ServiceModel service)
    {
        Service = service;
    }
}
