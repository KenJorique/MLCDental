using ClinicApp.Services;
using ClinicApp.ViewModels.SupplyVM;

namespace ClinicApp.Views.SupplyRelated;

public partial class SupplyListPage : ContentPage
{
    readonly SupplyListViewModel _vm;
    readonly SupabaseRealtimeService _realtime;
    bool _subscribed = false;

    public SupplyListPage(SupplyListViewModel vm, SupabaseRealtimeService realtime)
    {
        InitializeComponent();
        _vm = vm;
        _realtime = realtime;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await Task.Delay(100);
        await _vm.LoadSuppliesAsync();

        if (!_subscribed)
        {
            _subscribed = true;
            _realtime.OnSupplyChanged += async () => await _vm.LoadSuppliesAsync();
            await _realtime.SubscribeToSuppliesAsync();
        }
    }
}