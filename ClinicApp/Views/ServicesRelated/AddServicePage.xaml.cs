using ClinicApp.ViewModels.ServicesRelatedVM;

namespace ClinicApp.Views.ServicesRelated;

public partial class AddServicePage : ContentPage
{
    readonly AddServiceViewModel _viewModel;

    public AddServicePage(AddServiceViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = vm;
    }

    // Routes the back arrow through the same discard-confirmation as Cancel.
    protected override bool OnBackButtonPressed()
    {
        _viewModel.CancelCommand.Execute(null);
        return true; // tells the OS "I handled this — don't navigate back yet"
    }
}