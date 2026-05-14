using ClinicApp.ViewModels.CephalometricVM;

namespace ClinicApp.Views.CephalometricRelated;

public partial class CephalometricPage : ContentPage
{
    public CephalometricPage(CephalometricViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
