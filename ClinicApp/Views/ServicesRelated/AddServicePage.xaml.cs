using ClinicApp.ViewModels.ServicesRelatedVM;

namespace ClinicApp.Views.ServicesRelated;

public partial class AddServicePage : ContentPage
{
    AddServiceViewModel _viewModel;

    public AddServicePage(AddServiceViewModel vm)
    {
        InitializeComponent();
        BindingContext = _viewModel = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Check if there are enough services to enable the Package tab
        _viewModel.CheckPackageEligibilityCommand.Execute(null);
    }
}
