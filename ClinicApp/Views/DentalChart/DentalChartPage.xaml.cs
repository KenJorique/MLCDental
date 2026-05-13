using ClinicApp.ViewModels.DentalChart;

namespace ClinicApp.Views.DentalChart;

public partial class DentalChartPage : ContentPage
{
    private readonly DentalChartViewModel _vm;

    public DentalChartPage(DentalChartViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    // Reload chart every time page appears so the dentist
    // always sees the latest saved state after navigating away and back.
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        // LoadChart is also triggered by OnPatientIdChanged, but this
        // handles the case where the user navigates back to the same patient.
        if (_vm.PatientId > 0)
            await _vm.LoadChartCommand.ExecuteAsync(null);
    }
}
