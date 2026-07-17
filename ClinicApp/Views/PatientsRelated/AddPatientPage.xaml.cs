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
        if (BindingContext is AddPatientViewModel vm)
            MainThread.BeginInvokeOnMainThread(async () => await vm.InitializeAsync());
    }
}
