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
        return true; // tells the OS "I handled this, don't navigate back yet"
    }

    // Corrects a directly-typed Total Sessions value below the minimum of 2, once the user leaves the field.
    private void OnTotalSessionsUnfocused(object? sender, FocusEventArgs e) => _viewModel.ClampTotalSessions();

    // Corrects a directly-typed Follow-up Interval value below the minimum of 1, once the user leaves the field.
    private void OnFollowupIntervalUnfocused(object? sender, FocusEventArgs e) => _viewModel.ClampFollowupInterval();
}
