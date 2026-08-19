using ClinicApp.Services;
using ClinicApp.ViewModels;

namespace ClinicApp.Views.AppointmentRelated;

public partial class InProcedurePage : ContentPage
{
    readonly InProcedureViewModel _vm;
    readonly SupabaseRealtimeService _realtime;
    bool _subscribed = false;

    public InProcedurePage(InProcedureViewModel vm, SupabaseRealtimeService realtime)
    {
        InitializeComponent();
        _vm = vm;
        _realtime = realtime;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _vm.LoadAsync();

            if (!_subscribed)
            {
                _subscribed = true;
                _realtime.OnAppointmentChanged += async () => await _vm.LoadAsync();
                await _realtime.SubscribeToAppointmentEntriesAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InProcedurePage] {ex.Message}");
        }
    }
}