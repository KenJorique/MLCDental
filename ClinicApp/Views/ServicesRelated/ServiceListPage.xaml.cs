using ClinicApp.ViewModels;

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
        // This ensures the list updates every time you view the page
        _viewModel.LoadServicesCommand.Execute(null);
    }
}