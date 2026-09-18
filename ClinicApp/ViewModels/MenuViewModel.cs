using ClinicApp.Services;
using ClinicApp.Views;
using ClinicApp.Views.ReportRelated;
using ClinicApp.Views.ServicesRelated;
using ClinicApp.Views.SupplyRelated;
using ClinicApp.Views.TransactionRelated;
using ClinicApp.Views.UsersRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels
{
    public partial class MenuViewModel : ObservableObject
    {
        private readonly SessionService _session;

        [ObservableProperty] private string fullName = "";
        [ObservableProperty] private string role = "";

        // Injects the session service.
        public MenuViewModel(SessionService session)
        {
            _session = session;
        }

        // Refreshes the hero header's name/role every time the page appears.
        public void OnAppearing()
        {
            try
            {
                FullName = _session.IsAuthenticated ? _session.FullName : "";
                Role = _session.IsAuthenticated ? _session.Role : "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MenuViewModel] OnAppearing error: {ex.Message}");
            }
        }

        // Opens the Profile page (tapping the hero header).
        [RelayCommand]
        async Task GoToProfile()
        {
            try { await Shell.Current.GoToAsync(nameof(ProfilePage)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MenuViewModel] GoToProfile: {ex.Message}"); }
        }

        // Opens the Services and Pricing page.
        [RelayCommand]
        async Task GoToServices()
        {
            try { await Shell.Current.GoToAsync(nameof(ServiceListPage)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MenuViewModel] GoToServices: {ex.Message}"); }
        }

        // Opens the User Management page.
        [RelayCommand]
        async Task GoToUsers()
        {
            try { await Shell.Current.GoToAsync(nameof(UserListPage)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MenuViewModel] GoToUsers: {ex.Message}"); }
        }

        // Opens the Add Staff page.
        [RelayCommand]
        async Task GoToAddStaff()
        {
            try { await Shell.Current.GoToAsync(nameof(AddUserPage)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MenuViewModel] GoToAddStaff: {ex.Message}"); }
        }

        // Opens the Medical Supply page.
        [RelayCommand]
        async Task GoToSupply()
        {
            try { await Shell.Current.GoToAsync(nameof(SupplyListPage)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MenuViewModel] GoToSupply: {ex.Message}"); }
        }

        // Opens the Balance Management page.
        [RelayCommand]
        async Task GoToPaymentManagement()
        {
            try { await Shell.Current.GoToAsync(nameof(BalanceManagementPage)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MenuViewModel] GoToPaymentManagement: {ex.Message}"); }
        }

        // Opens the Reports page.
        [RelayCommand]
        async Task GoToReports()
        {
            try { await Shell.Current.GoToAsync(nameof(ReportsPage)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MenuViewModel] GoToReports: {ex.Message}"); }
        }
    }
}
