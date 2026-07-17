using ClinicApp.ViewModels.ServicesRelatedVM;

namespace ClinicApp.Views.ServicesRelated;

public partial class ServiceListPage : ContentPage
{
    ServiceViewModel _viewModel;

    public ServiceListPage(ServiceViewModel vm)
    {
        InitializeComponent();
        BindingContext = _viewModel = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Give the navigation animation 100ms to finish completely
        await Task.Delay(100);

        if (BindingContext is ServiceViewModel vm)
        {
            // Call the asynchronous method directly on a background thread task 
            // to prevent UI deadlock
            _ = Task.Run(async () => await vm.LoadServices());
        }
    }
}
