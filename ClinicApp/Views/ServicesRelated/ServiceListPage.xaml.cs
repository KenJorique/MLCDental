using ClinicApp.ViewModels.ServicesRelatedVM;

namespace ClinicApp.Views.ServicesRelated;

public partial class ServiceListPage : ContentPage
{
    ServiceViewModel _viewModel;

    public ServiceListPage(ServiceViewModel vm)
    {
        InitializeComponent();
        BindingContext = _viewModel = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadServicesCommand.Execute(null);
    }
}
