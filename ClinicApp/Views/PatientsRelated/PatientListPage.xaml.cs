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

        // Show whatever's already in local SQLite immediately — don't make the user
        // wait on Supabase before they can see their patient list at all.
        _viewModel.LoadPatientsCommand.Execute(null);

        // Connects/syncs/subscribes, then reloads the list itself once it has fresh
        // data. Safe to call every time — it no-ops after the first successful run.
        await _viewModel.StartRealtimeAsync();
    }
}
