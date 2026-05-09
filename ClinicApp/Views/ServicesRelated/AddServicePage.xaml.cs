using ClinicApp.ViewModels;

namespace ClinicApp.Views.ServicesRelated;

public partial class AddServicePage : ContentPage
{
    ServiceViewModel _viewModel;

    public AddServicePage(ServiceViewModel vm)
    {
        InitializeComponent();
        // FIX: Assign vm to _viewModel
        BindingContext = _viewModel = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Now _viewModel is no longer null
        if (_viewModel?.LoadServicesCommand != null && _viewModel.LoadServicesCommand.CanExecute(null))
        {
            _viewModel.LoadServicesCommand.Execute(null);
        }
    }
}