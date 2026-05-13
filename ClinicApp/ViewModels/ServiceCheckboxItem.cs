using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClinicApp.ViewModels;

// Wraps a ServiceModel with a checkbox state for the Package "Included Services" list
public partial class ServiceCheckboxItem : ObservableObject
{
    public ServiceModel Service { get; }

    // Bound to the checkbox in the UI
    [ObservableProperty]
    bool isSelected;

    public ServiceCheckboxItem(ServiceModel service)
    {
        Service = service;
    }
}
