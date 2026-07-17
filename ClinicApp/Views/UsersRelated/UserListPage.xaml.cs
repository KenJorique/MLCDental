using ClinicApp.ViewModels.UsersRelated;

namespace ClinicApp.Views.UsersRelated;

public partial class UserListPage : ContentPage
{
    UserViewModel _viewModel;

    // Tracks the currently open SwipeView so we can close it when another opens
    SwipeView? _currentOpenSwipe;

    public UserListPage(UserViewModel vm)
    {
        InitializeComponent();
        BindingContext = _viewModel = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await Task.Delay(100);

        if (BindingContext is UserViewModel vm)
        {
            _ = Task.Run(async () => await vm.LoadUsers());
        }
    }

    // Called when any SwipeView starts being swiped
    // Closes the previously open SwipeView before opening the new one
    private void OnSwipeStarted(object sender, SwipeStartedEventArgs e)
    {
        if (sender is SwipeView swipeView && swipeView != _currentOpenSwipe)
        {
            _currentOpenSwipe?.Close();
            _currentOpenSwipe = swipeView;
        }
    }
}
