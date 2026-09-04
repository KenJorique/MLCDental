using ClinicApp.Views;
using ClinicApp.Views.UsersRelated;
using ClinicApp.Services;
using ClinicApp.ViewModels.PatientsRelatedVM;

namespace ClinicApp
{
    public partial class App : Application
    {
        readonly SupabaseDataService _supabaseData;
        readonly DatabaseService _db;
        readonly SupabaseRealtimeService _realtime;
        readonly PatientListViewModel _patientListVm;
        readonly SessionService _session;
        readonly RememberMeService _rememberMe; // ── NEW ──

#if DEBUG
        // Dev-only convenience — see previous notes. Leave OFF (false) now
        // that "Remember me" gives you a real way to skip re-login during
        // normal use; only flip this on if you specifically need to
        // bypass even that.
        const bool DevSkipLogin = true;
#endif

        public App(SupabaseDataService supabaseData, DatabaseService db,
                   SupabaseRealtimeService realtime, PatientListViewModel patientListVm,
                   SessionService session, LoginPage loginPage, RememberMeService rememberMe)
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
            InitializeComponent();
            UserAppTheme = AppTheme.Light;

            _supabaseData = supabaseData;
            _db = db;
            _realtime = realtime;
            _patientListVm = patientListVm;
            _session = session;
            _rememberMe = rememberMe; // ── NEW ──

            // Always start on the plain LoginPage — if a valid "remember
            // this device" token exists, TryAutoSignInAsync() below swaps
            // to AppShell moments later. Brief flash of the login screen
            // on a remembered device is an acceptable trade-off for
            // keeping cold start fast and not blocking the UI on a DB
            // query before anything renders.
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

        // ── NEW: cold-start-only check for a valid "remember this
        // device" token. Intentionally never called from anywhere except
        // here — see RememberMeService.cs for why it must not also run
        // whenever AppShell redirects back to the login page mid-session. ──
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
