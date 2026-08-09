using ClinicApp.ViewModels.CephalometricVM;

namespace ClinicApp.Views.CephalometricRelated;

public partial class CephalometricMeasurementsPage : ContentPage
{
    public CephalometricMeasurementsPage(CephalometricMeasurementsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}