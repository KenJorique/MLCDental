using ClinicApp.Services;
using ClinicApp.ViewModels.ServicesRelatedVM;

namespace ClinicApp.Views.ServicesRelated;

public partial class ServiceListPage : ContentPage
{
    ServiceViewModel _viewModel;
    readonly SupabaseRealtimeService _realtime;
    bool _subscribed = false;

    public ServiceListPage(ServiceViewModel vm, SupabaseRealtimeService realtime)
    {
        InitializeComponent();
        BindingContext = _viewModel = vm;
        _realtime = realtime;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(100);

        if (BindingContext is ServiceViewModel vm)
        {
            _ = Task.Run(async () => await vm.LoadServices());

            if (!_subscribed)
            {
                _subscribed = true;
                _realtime.OnServiceChanged += async () => await vm.LoadServices();
                await _realtime.SubscribeToServicesAsync();
            }
        }
    }
}