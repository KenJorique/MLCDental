using ClinicApp.ViewModels.CephalometricVM;

namespace ClinicApp.Views.CephalometricRelated;

public partial class CephalometricMeasurementsPage : ContentPage
{
    private readonly CephalometricMeasurementsViewModel _vm;

    public CephalometricMeasurementsPage(CephalometricMeasurementsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadPendingAsync();
    }
}