using ClinicApp.Views;
using ClinicApp.Views.UsersRelated;
using ClinicApp.Services;
using ClinicApp.ViewModels.PatientsRelatedVM;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicApp
{
    public partial class App : Application
    {
        readonly SupabaseDataService _supabaseData;
        readonly DatabaseService _db;
        readonly SupabaseRealtimeService _realtime;
        readonly PatientListViewModel _patientListVm;
        readonly SessionService _session;
        readonly RememberMeService _rememberMe;

#if DEBUG
        // Dev-only auto-login bypass; keep false unless testing without login.
        const bool DevSkipLogin = true;
#endif

        // Builds the app, wires global crash handlers, then boots straight into LoginPage.
        public App(SupabaseDataService supabaseData, DatabaseService db,
                   SupabaseRealtimeService realtime, PatientListViewModel patientListVm,
                   SessionService session, IServiceProvider serviceProvider, RememberMeService rememberMe)
        {
            MauiExceptions.Initialize();
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                System.Diagnostics.Debug.WriteLine($"[FATAL] UnhandledException: {ex?.Message}");
                System.Diagnostics.Debug.WriteLine($"[FATAL] Stack: {ex?.StackTrace}");
            };
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[FATAL] UnobservedTask: {args.Exception?.Message}");
                args.SetObserved();
            };
            MauiExceptions.UnhandledException += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[FATAL] MauiException: {args.ExceptionObject}");
            };

            // Must run first so Application.Resources (Colors/Styles/etc.) exist before any page XAML parses.
            InitializeComponent();
            UserAppTheme = AppTheme.Light;

            _supabaseData = supabaseData;
            _db = db;
            _realtime = realtime;
            _patientListVm = patientListVm;
            _session = session;
            _rememberMe = rememberMe;

            // Resolved here (not as a constructor parameter) so LoginPage.xaml parses only after InitializeComponent() above has populated Application.Resources.
            var loginPage = serviceProvider.GetRequiredService<LoginPage>();

            // Always boots to LoginPage; TryRememberedSignInAsync swaps to AppShell moments later if a remembered device token is valid.
            MainPage = loginPage;

            _ = RunStartupCleanupAsync();
            _ = _patientListVm.StartRealtimeAsync();

#if DEBUG
            if (DevSkipLogin)
            {
                _ = DevAutoLoginAsync();
            }
            else
            {
                _ = TryRememberedSignInAsync();
            }
#else
            _ = TryRememberedSignInAsync();
#endif
        }

        // Cold-start-only: signs in automatically if a valid "remember this device" token exists.
        private async Task TryRememberedSignInAsync()
        {
            try
            {
                await Task.Delay(300); // let DatabaseService.Init() finish
                var user = await _rememberMe.TryAutoLoginAsync();
                if (user is null) return;

                _session.SignIn(user);
                NavigationHelper.ShowApp();
                System.Diagnostics.Debug.WriteLine($"[App] Auto-signed in remembered user '{user.Username}'.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] TryRememberedSignIn error: {ex.Message}");
            }
        }

#if DEBUG
        // Dev-only: auto-signs in as the first active Dentist account found, skipping the login screen.
        private async Task DevAutoLoginAsync()
        {
            try
            {
                await Task.Delay(500);
                var devUser = (await _db.GetUsers())
                    .FirstOrDefault(u => u.IsActive && string.Equals(u.Role, "Dentist", StringComparison.OrdinalIgnoreCase));
                if (devUser is null)
                {
                    System.Diagnostics.Debug.WriteLine("[App] DevAutoLogin: no active Dentist account found.");
                    return;
                }
                _session.SignIn(devUser);
                NavigationHelper.ShowApp();
                System.Diagnostics.Debug.WriteLine($"[App] DevAutoLogin: signed in as '{devUser.Username}'.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] DevAutoLogin error: {ex.Message}");
            }
        }
#endif

        // Runs a delayed background cleanup of past local and Supabase appointments.
        private async Task RunStartupCleanupAsync()
        {
            try
            {
                await Task.Delay(3000);
                System.Diagnostics.Debug.WriteLine("[App] Running startup cleanup...");
                await _db.CleanupPastLocalAppointmentsAsync();
                await _supabaseData.CleanupPastAppointmentsAsync();
                System.Diagnostics.Debug.WriteLine("[App] Startup cleanup complete.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Startup cleanup error: {ex.Message}");
            }
        }
    }
}
