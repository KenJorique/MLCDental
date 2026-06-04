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
        try
        {
            await _viewModel.StartRealtimeAsync(); // safe — has _realtimeStarted guard
            await _viewModel.LoadPatientsCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PatientListPage] {ex.Message}");
        }
    }
}