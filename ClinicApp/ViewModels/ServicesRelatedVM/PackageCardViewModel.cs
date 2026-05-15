using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClinicApp.ViewModels.ServicesRelatedVM;

// Wraps a ServicePackage with expand/collapse UI state for the ServiceListPage card
public partial class PackageCardViewModel : ObservableObject
{
    public ServicePackage Package { get; }

    // Controls whether included services and description are visible
    [ObservableProperty]
    bool isExpanded;

    public PackageCardViewModel(ServicePackage package)
    {
        Package = package;
    }
}
