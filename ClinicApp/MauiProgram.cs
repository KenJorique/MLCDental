using ClinicApp.Services;
using ClinicApp.ViewModels;
using ClinicApp.ViewModels.CephalometricVM;
using ClinicApp.ViewModels.DentalChart;
using ClinicApp.ViewModels.PatientsRelatedVM;
using ClinicApp.ViewModels.ServicesRelatedVM;
using ClinicApp.ViewModels.SupplyVM;
using ClinicApp.ViewModels.UsersRelated;
using ClinicApp.Views;
using ClinicApp.Views.CephalometricRelated;
using ClinicApp.Views.DentalChart;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.ServicesRelated;
using ClinicApp.Views.SupplyRelated;
using ClinicApp.Views.UsersRelated;
using Microsoft.Extensions.Logging;
using The49.Maui.BottomSheet;

namespace ClinicApp
{
    public static class MauiProgram
    {
        private const string SupabaseUrl = "https://uxacdqkkocbjaiqszpyk.supabase.co";
        private const string SupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InV4YWNkcWtrb2NiamFpcXN6cHlrIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODA0NTExNTUsImV4cCI6MjA5NjAyNzE1NX0.Jt-Dsn6j3m9uL_R0A1Y0AVlUKBA_hmNI-NfHDBQYLUA";

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            // ── Google refresh token ──────────────────────────────
            Preferences.Set("google_refresh_token",
     "1//0etnD-p20Px5wCgYIARAAGA4SNwF-L9IrRRqCR6LS1Egm5jBQzQycF9dM4KQ5KXD1wi8J9WHx6Yd4LWq9nd5aj0ZyZlOA1gP-wXM");
            // Clear cached token so fresh one is fetched
            Preferences.Remove("google_access_token");


            // ── Core services ─────────────────────────────────────
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton(new SupabaseDataService(SupabaseUrl, SupabaseKey));
            builder.Services.AddSingleton<SupabaseRealtimeService>(sp =>
                new SupabaseRealtimeService(
                    sp.GetRequiredService<DatabaseService>()));

            // ── App ───────────────────────────────────────────────
            builder.Services.AddSingleton<App>(sp => new App(
                sp.GetRequiredService<SupabaseDataService>(),
                sp.GetRequiredService<DatabaseService>()
            ));

            // ── Main pages ────────────────────────────────────────
            builder.Services.AddSingleton<HomePage>();
            builder.Services.AddSingleton<MenuViewModel>();
            builder.Services.AddSingleton<MenuPage>();

            // ── Google Sign-In ────────────────────────────────────
            builder.Services.AddTransient<GoogleSignInPage>(); 

            // ── Appointments ──────────────────────────────────────
            builder.Services.AddSingleton<AppointmentViewModel>(sp =>
                new AppointmentViewModel(
                    sp.GetRequiredService<DatabaseService>(),
                    sp.GetRequiredService<SupabaseDataService>()
                ));
            builder.Services.AddSingleton<AppointmentPage>();
            builder.Services.AddSingleton<AppointmentScheduleViewModel>(sp =>
                new AppointmentScheduleViewModel(
                    sp.GetRequiredService<DatabaseService>(),
                    sp.GetRequiredService<SupabaseDataService>()
                ));
           
            builder.Services.AddSingleton<AppointmentSchedulePage>(sp =>
                            new AppointmentSchedulePage(
                                sp.GetRequiredService<AppointmentScheduleViewModel>(),
                                sp.GetRequiredService<SupabaseRealtimeService>()
                            ));

            // ── Patients ──────────────────────────────────────────
            builder.Services.AddSingleton<PatientListViewModel>(sp =>
                new PatientListViewModel(
                    sp.GetRequiredService<DatabaseService>(),
                    sp.GetRequiredService<SupabaseRealtimeService>(),
                    sp.GetRequiredService<SupabaseDataService>()
                ));
            builder.Services.AddSingleton<PatientListPage>();
            builder.Services.AddTransient<AddPatientViewModel>(sp =>
                new AddPatientViewModel(
                    sp.GetRequiredService<DatabaseService>(),
                    sp.GetRequiredService<SupabaseDataService>()
                ));
            builder.Services.AddTransient<AddPatientPage>();
            builder.Services.AddTransient<PatientDetailsPage>();
            builder.Services.AddTransient<PatientDetailsViewModel>();
            builder.Services.AddTransient<DentalChartPage>();
            builder.Services.AddTransient<DentalChartViewModel>();
            builder.Services.AddTransient<TreatmentHistoryPage>();
            builder.Services.AddTransient<TreatmentHistoryViewModel>();
            builder.Services.AddTransient<CephalometricPage>();
            builder.Services.AddTransient<CephalometricViewModel>();

            // ── Services ──────────────────────────────────────────
            builder.Services.AddSingleton<ServiceViewModel>();
            builder.Services.AddTransient<ServiceListPage>();
            builder.Services.AddTransient<AddServicePage>();
            builder.Services.AddTransient<AddServiceViewModel>();

            // ── Users ─────────────────────────────────────────────
            builder.Services.AddSingleton<UserViewModel>();
            builder.Services.AddTransient<UserListPage>();
            builder.Services.AddTransient<AddUserPage>();
            builder.Services.AddTransient<AddUserViewModel>();

            // Transactions
            builder.Services.AddTransient<TransactionViewModel>(sp =>
                            new TransactionViewModel(
                                sp.GetRequiredService<SupabaseDataService>()
                            ));
            builder.Services.AddTransient<TransactionPage>();

            // ── Supply ────────────────────────────────────────────
            builder.Services.AddTransient<SupplyListPage>();
            builder.Services.AddTransient<SupplyListViewModel>();
            builder.Services.AddTransient<AddSupplyPage>();
            builder.Services.AddTransient<AddSupplyViewModel>();
            builder.Services.AddTransient<SupplyInfoPage>();
            builder.Services.AddTransient<SupplyInfoViewModel>();
            builder.Services.AddTransient<AddStockPage>();
            builder.Services.AddTransient<AddStockViewModel>();
            builder.Services.AddTransient<ReduceStockPage>();
            builder.Services.AddTransient<ReduceStockViewModel>();
            builder.Services.AddTransient<StockHistoryPage>();
            builder.Services.AddTransient<StockHistoryViewModel>();
            builder.Services.AddTransient<AdjustStockSheet>();

            builder
                .UseMauiApp<App>()
                .UseBottomSheet()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}