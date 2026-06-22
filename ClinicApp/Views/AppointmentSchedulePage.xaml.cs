using ClinicApp.Services;
using ClinicApp.ViewModels;

namespace ClinicApp.Views
{
    public partial class AppointmentSchedulePage : ContentPage
    {
        readonly AppointmentScheduleViewModel _vm;
        readonly SupabaseRealtimeService _realtime;
        bool _subscribed = false;

        public AppointmentSchedulePage(
            AppointmentScheduleViewModel vm,
            SupabaseRealtimeService realtime)
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
                await _vm.LoadAppointments();

                if (!_subscribed)
                {
                    _subscribed = true;
                    _realtime.OnAppointmentChanged += async () =>
                        await _vm.LoadAppointments();

                    await _realtime.SubscribeToAppointmentEntriesAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AppointmentSchedulePage] {ex.Message}");
            }
        }
    }
}