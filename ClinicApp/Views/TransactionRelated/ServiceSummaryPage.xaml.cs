using ClinicApp.ViewModels.TransactionVM;

namespace ClinicApp.Views.TransactionRelated;

public partial class ServiceSummaryPage : ContentPage
{
    readonly ServiceSummaryViewModel _vm;

    // Tracks whichever SwipeView is currently open, so opening a new one
    // can close the previous one instead of leaving multiple open at once.
    SwipeView? _openSwipe;

    public ServiceSummaryPage(ServiceSummaryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadDraft();
    }

    void OnSwipeEnded(object? sender, SwipeEndedEventArgs e)
    {
        if (sender is not SwipeView swipe) return;

        if (e.IsOpen)
        {
            if (_openSwipe != null && _openSwipe != swipe)
                _openSwipe.Close();

            _openSwipe = swipe;
        }
        else if (_openSwipe == swipe)
        {
            _openSwipe = null;
        }
    }
}