using ClinicApp.ViewModels.PatientsRelatedVM;

namespace ClinicApp.Views.PatientsRelated;

public partial class PatientListPage : ContentPage
{
    PatientListViewModel _viewModel;

    public PatientListPage(PatientListViewModel vm)
    {
        InitializeComponent();
        BindingContext = _viewModel = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Refresh the list every time the user navigates back to this page
        _viewModel.LoadPatientsCommand.Execute(null);
    }
}