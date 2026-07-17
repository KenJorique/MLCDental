//using ClinicApp.Views;

//namespace ClinicApp
//{
//    public partial class App : Application
//    {
//        public App()
//        {
//            InitializeComponent();

//            UserAppTheme = AppTheme.Light;

//            MainPage = new AppShell();
//        }
//    }
//}


using ClinicApp.Views;
using ClinicApp.Services;

namespace ClinicApp
{

    public partial class App : Application
    {

        readonly SupabaseDataService _supabaseData;
        readonly DatabaseService _db;

        public App(SupabaseDataService supabaseData, DatabaseService db)
        {
            MauiExceptions.Initialize();
            // ── Global crash handler — catches silent crashes ──────────
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                System.Diagnostics.Debug.WriteLine(
                    $"[FATAL] UnhandledException: {ex?.Message}");
                System.Diagnostics.Debug.WriteLine(
                    $"[FATAL] Stack: {ex?.StackTrace}");
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FATAL] UnobservedTask: {args.Exception?.Message}");
                args.SetObserved();
            };

            MauiExceptions.UnhandledException += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FATAL] MauiException: {args.ExceptionObject}");
            };

            InitializeComponent();
            UserAppTheme = AppTheme.Light;
            MainPage = new AppShell();

            _supabaseData = supabaseData;
            _db = db;

            _ = RunStartupCleanupAsync();
        }

        private async Task RunStartupCleanupAsync()
        {
            try
            {
                // Small delay so app finishes loading first
                await Task.Delay(3000);

                System.Diagnostics.Debug.WriteLine(
                    "[App] Running startup cleanup...");

                // Clean local SQLite first (instant)
                await _db.CleanupPastLocalAppointmentsAsync();

                // Then clean Supabase (network call)
                await _supabaseData.CleanupPastAppointmentsAsync();

                System.Diagnostics.Debug.WriteLine(
                    "[App] Startup cleanup complete.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[App] Startup cleanup error: {ex.Message}");
            }
            UserAppTheme = AppTheme.Light;
            MainPage = new AppShell();
        }
    }
}