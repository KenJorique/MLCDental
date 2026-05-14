using ClinicApp.ViewModels;

namespace ClinicApp.Views.ServicesRelated;

public partial class AddServicePage : ContentPage
{
    public AddServicePage(AddServiceViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
