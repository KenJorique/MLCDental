using ClinicApp.ViewModels;

namespace ClinicApp.Views;

public partial class ServicePage : ContentPage
{
    ServiceViewModel _viewModel;

    public ServicePage(ServiceViewModel vm)
    {
        InitializeComponent();
        BindingContext = _viewModel = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Automatically refresh the list when the page is opened
        if (_viewModel.LoadServicesCommand.CanExecute(null))
        {
            _viewModel.LoadServicesCommand.Execute(null);
        }
    }
}