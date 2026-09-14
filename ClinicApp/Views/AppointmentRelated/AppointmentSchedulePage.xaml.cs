using ClinicApp.Services;
using ClinicApp.ViewModels;

namespace ClinicApp.Views.AppointmentRelated
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

            // Subscribe to redraw requests
            CalendarGraphics.Drawable = vm.CalendarDrawable;
            _vm.CalendarNeedsRedraw += () => MainThread.BeginInvokeOnMainThread(() => CalendarGraphics?.Invalidate());
        }

        private void OnCalendarNeedsRedraw()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CalendarGraphics?.Invalidate();
            });
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                // BUGFIX: ShowDetail only got reset to false when a sheet button
                // (Complete/Reschedule/Cancel/Close) was tapped. Swiping the sheet away
                // or tapping the backdrop instead skipped all of that, leaving ShowDetail
                // stuck true — which hid the FAB (bound to !ShowDetail) until something
                // else happened to reset it. Since no sheet can legitimately still be
                // open the moment this page becomes visible again, force it back to
                // false here so the FAB is reliably visible on every return to this page.
                _vm.ShowDetail = false;

                await _vm.LoadAppointments();
                await _vm.LoadPendingFollowUpsAsync();

                if (!_subscribed)
                {
                    _subscribed = true;
                    _realtime.OnAppointmentChanged += async () => await _vm.LoadAppointments();
                    await _realtime.SubscribeToAppointmentEntriesAsync();

                    _realtime.OnTreatmentSequenceChanged += async () => await _vm.LoadPendingFollowUpsAsync();
                    await _realtime.SubscribeToTreatmentSequencesAsync();
                }

                if (_vm.IsCalendarView)
                    OnCalendarNeedsRedraw();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppointmentSchedulePage] {ex.Message}");
            }
            await Task.Delay(100);
            CalendarGraphics?.Invalidate();
        }

        private void OnCalendarTapped(object? sender, TappedEventArgs e)
        {
            try
            {
                var pos = e.GetPosition(CalendarGraphics);
                if (pos == null) return;

                var entry = _vm.CalendarDrawable.HitTest((float)pos.Value.X, (float)pos.Value.Y);
                if (entry != null)
                    _vm.SelectWeekAppointmentCommand.Execute(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalendarTap] {ex.Message}");
            }
        }

        public void RefreshCalendar()
        {
            CalendarGraphics?.Invalidate();
        }
        protected override void OnDisappearing()
        {
            if (_vm != null)
                _vm.CalendarNeedsRedraw -= OnCalendarNeedsRedraw;
            base.OnDisappearing();
        }
    }
}