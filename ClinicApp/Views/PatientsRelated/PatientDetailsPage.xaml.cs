using ClinicApp.ViewModels.PatientsRelatedVM;

namespace ClinicApp.Views.PatientsRelated;

public partial class PatientDetailsPage : ContentPage
{
    public PatientDetailsPage(PatientDetailsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        // NavigatedTo fires every time this page becomes active,
        // including when navigating back from EditPatient.
        if (BindingContext is PatientDetailsViewModel vm && vm.PatientId > 0)
            MainThread.BeginInvokeOnMainThread(async () =>
                await vm.LoadPatientCommand.ExecuteAsync(null));
    }
}
