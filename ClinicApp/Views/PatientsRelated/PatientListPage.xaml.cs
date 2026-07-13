using ClinicApp.ViewModels.PatientsRelatedVM;

namespace ClinicApp.Views.PatientsRelated;

public partial class PatientListPage : ContentPage
{
    readonly PatientListViewModel _viewModel;

    public PatientListPage(PatientListViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Refresh the list every time the user navigates back to this page
        _viewModel.LoadPatientsCommand.Execute(null);
    }
}
