using ClinicApp.ViewModels.PatientsRelatedVM;

namespace ClinicApp.Views.PatientsRelated;

public partial class AddPatientPage : ContentPage
{
    public AddPatientPage(AddPatientViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Ensures medical conditions load for new patients.
        // OnPatientIdChanged never fires when PatientId stays at default 0.
        if (BindingContext is AddPatientViewModel vm)
            MainThread.BeginInvokeOnMainThread(async () => await vm.InitializeAsync());
    }
}
