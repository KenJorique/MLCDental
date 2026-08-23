using ClinicApp.ViewModels.PatientsRelatedVM;

namespace ClinicApp.Views.PatientsRelated;

public partial class TreatmentHistoryPage : ContentPage
{
    public TreatmentHistoryPage(TreatmentHistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is TreatmentHistoryViewModel vm)
            vm.LoadHistoryCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is TreatmentHistoryViewModel vm)
            vm.Cleanup();
    }
}