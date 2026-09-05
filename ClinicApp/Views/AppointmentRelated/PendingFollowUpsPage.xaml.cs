using ClinicApp.Services;
using ClinicApp.ViewModels;

namespace ClinicApp.Views.AppointmentRelated
{
    public partial class PendingFollowUpsPage : ContentPage
    {
        readonly PendingFollowUpsViewModel _vm;
        readonly SupabaseRealtimeService _realtime;
        bool _subscribed = false;

        public PendingFollowUpsPage(PendingFollowUpsViewModel vm, SupabaseRealtimeService realtime)
        {
            InitializeComponent();
            _vm = vm;
            _realtime = realtime;
            BindingContext = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadAsync();

            if (!_subscribed)
            {
                _subscribed = true;
                _realtime.OnTreatmentSequenceChanged += async () => await _vm.LoadAsync();
                await _realtime.SubscribeToTreatmentSequencesAsync();
            }
        }
    }
}