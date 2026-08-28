using ClinicApp.ViewModels.ServicesRelatedVM;

namespace ClinicApp.Views.ServicesRelated;

public partial class AddServicePage : ContentPage
{
    // Wires the ViewModel as the page's BindingContext.
    public AddServicePage(AddServiceViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is AddServiceViewModel vm)
        {
            vm.CancelCommand.Execute(null);
            return true; // we own navigation now
        }

        return base.OnBackButtonPressed();
    }
}
