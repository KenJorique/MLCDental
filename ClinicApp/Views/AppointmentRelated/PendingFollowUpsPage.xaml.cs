using ClinicApp.Services;
using ClinicApp.ViewModels;

namespace ClinicApp.Views.AppointmentRelated
{
    public partial class PendingFollowUpsPage : ContentPage
    {
        readonly PendingFollowUpsViewModel _vm;
        readonly SupabaseRealtimeService _realtime;
        bool _subscribed = false;
        Func<Task>? _onTreatmentSequenceChanged;

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
                _onTreatmentSequenceChanged = async () => await _vm.LoadAsync();
                _realtime.OnTreatmentSequenceChanged += Invoke;
                await _realtime.SubscribeToTreatmentSequencesAsync();
            }
        }

        protected override void OnDisappearing()
        {
            if (_subscribed && _onTreatmentSequenceChanged != null)
            {
                _realtime.OnTreatmentSequenceChanged -= Invoke;
                _subscribed = false;
            }
            base.OnDisappearing();
        }

        void Invoke() => _ = _onTreatmentSequenceChanged?.Invoke();
    }
}