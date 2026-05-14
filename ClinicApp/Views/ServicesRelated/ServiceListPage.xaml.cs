using ClinicApp.ViewModels.ServicesRelatedVM;

namespace ClinicApp.Views.ServicesRelated;

public partial class ServiceListPage : ContentPage
{
    ServiceViewModel _viewModel;

    // Tracks the currently open SwipeView so we can close it when another opens
    SwipeView? _currentOpenSwipe;

    public ServiceListPage(ServiceViewModel vm)
    {
        InitializeComponent();
        BindingContext = _viewModel = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Reload the list every time the page is shown
        _viewModel.LoadServicesCommand.Execute(null);
    }

    // Called when any SwipeView starts being swiped
    // Closes the previously open SwipeView before opening the new one
    private void OnSwipeStarted(object sender, SwipeStartedEventArgs e)
    {
        if (sender is SwipeView swipeView && swipeView != _currentOpenSwipe)
        {
            // Close the previously open swipe
            _currentOpenSwipe?.Close();
            _currentOpenSwipe = swipeView;
        }
    }
}
