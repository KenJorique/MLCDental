using ClinicApp.Services;
using ClinicApp.ViewModels;
using ClinicApp.ViewModels.CephalometricVM;
using ClinicApp.ViewModels.DentalChart;
using ClinicApp.ViewModels.PatientsRelatedVM;
using ClinicApp.ViewModels.ServicesRelatedVM;
using ClinicApp.ViewModels.SupplyVM;
using ClinicApp.ViewModels.TransactionVM;
using ClinicApp.ViewModels.UsersRelated;
using ClinicApp.Views;
using ClinicApp.Views.AppointmentRelated;
using ClinicApp.Views.CephalometricRelated;
using ClinicApp.Views.DentalChart;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.ServicesRelated;
using ClinicApp.Views.SupplyRelated;
using ClinicApp.Views.TransactionRelated;
using ClinicApp.Views.UsersRelated;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using The49.Maui.BottomSheet;
using SkiaSharp.Views.Maui.Controls.Hosting;
using Syncfusion.Maui.Core.Hosting;

namespace ClinicApp
{
    public static class MauiProgram
    {
        private const string SupabaseUrl = "https://uxacdqkkocbjaiqszpyk.supabase.co";
        private const string SupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InV4YWNkcWtrb2NiamFpcXN6cHlrIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODA0NTExNTUsImV4cCI6MjA5NjAyNzE1NX0.Jt-Dsn6j3m9uL_R0A1Y0AVlUKBA_hmNI-NfHDBQYLUA";
        private const string SyncfusionLicenseKey = "Ngo9BigBOggjHTQxAR8/V1NNaF5cXmBCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdlWXdcdXRURWlYVERwW0BWYUA=";

        // Builds and configures the MAUI app: DI registrations, fonts, and platform handlers.
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(SyncfusionLicenseKey);

            // ── Google refresh token ──────────────────────────────
            Preferences.Set("google_refresh_token",
     "1//04lNOw9Ik3RmfCgYIARAAGAQSNwF-L9IrWCDoRUW-BrnhpvGtUQvPJykV5kJQT-epjT75UhGphOTNb1Xr7wVCRE3XuNKKE8vY458");
            // Clears the cached access token so a fresh one is fetched on next use.
            Preferences.Remove("google_access_token");


            // ── Core services ─────────────────────────────────────
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton(new SupabaseDataService(SupabaseUrl, SupabaseKey));
            builder.Services.AddSingleton<SupabaseRealtimeService>(sp =>
                new SupabaseRealtimeService(
                    sp.GetRequiredService<DatabaseService>()));
            builder.Services.AddSingleton<BillDraftService>();
            builder.Services.AddSingleton<BillingService>();
            builder.Services.AddSingleton<SessionService>();          // one session for the app's lifetime
            builder.Services.AddSingleton<AuthenticationService>();    // stateless-ish, but fine as singleton
            // DatabaseService is presumably already registered as a Singleton — leave as is.
            // ── App ───────────────────────────────────────────────
            // App now takes IServiceProvider instead of a resolved LoginPage, so LoginPage.xaml only
            // parses after App.xaml.cs's InitializeComponent() has populated Application.Resources.
            builder.Services.AddSingleton<App>(sp => new App(
                sp.GetRequiredService<SupabaseDataService>(),
                sp.GetRequiredService<DatabaseService>(),
                sp.GetRequiredService<SupabaseRealtimeService>(),
                sp.GetRequiredService<PatientListViewModel>(),
                sp.GetRequiredService<SessionService>(),
                sp,
                sp.GetRequiredService<RememberMeService>()
            ));
            builder.Services.AddSingleton<RememberMeService>();

            // ── Main pages ────────────────────────────────────────
            builder.Services.AddSingleton<HomeViewModel>(sp =>
               new HomeViewModel(
                   sp.GetRequiredService<SupabaseDataService>(),
                   sp.GetRequiredService<AppointmentScheduleViewModel>()
               ));
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

            builder.Services.AddTransient<RescheduleViewModel>(sp =>
                            new RescheduleViewModel(
                                sp.GetRequiredService<SupabaseDataService>(),
                                sp.GetRequiredService<DatabaseService>()
                            ));
            builder.Services.AddTransient<ReschedulePage>();
            builder.Services.AddTransient<InProcedurePage>(sp =>
                            new InProcedurePage(
                                sp.GetRequiredService<InProcedureViewModel>(),
                                sp.GetRequiredService<SupabaseRealtimeService>()
                            ));
            builder.Services.AddTransient<InProcedureViewModel>(sp =>
                new InProcedureViewModel(
                    sp.GetRequiredService<SupabaseDataService>()
                ));

            builder.Services.AddTransient<WalkInBookingViewModel>(sp =>
    new WalkInBookingViewModel(
        sp.GetRequiredService<DatabaseService>(),
        sp.GetRequiredService<SupabaseDataService>()
    ));
            builder.Services.AddTransient<WalkInBookingPage>();


            builder.Services.AddTransient<PendingFollowUpsViewModel>(sp =>
    new PendingFollowUpsViewModel(
        sp.GetRequiredService<SupabaseDataService>(),
         sp.GetRequiredService<DatabaseService>()));
            builder.Services.AddTransient<PendingFollowUpsPage>(sp =>
    new PendingFollowUpsPage(
        sp.GetRequiredService<PendingFollowUpsViewModel>(),
        sp.GetRequiredService<SupabaseRealtimeService>()));

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
            builder.Services.AddTransient<DentalChartViewModel>(sp =>
                    new DentalChartViewModel(
                        sp.GetRequiredService<DatabaseService>(),
                        sp.GetRequiredService<SupabaseRealtimeService>()));
            builder.Services.AddTransient<Views.PatientsRelated.TreatmentHistoryPage>();
            builder.Services.AddTransient<TreatmentHistoryViewModel>(sp =>
            new TreatmentHistoryViewModel(
                sp.GetRequiredService<DatabaseService>(),
                sp.GetRequiredService<SupabaseRealtimeService>()));
            builder.Services.AddTransient<CephalometricPage>();
            builder.Services.AddTransient<CephalometricViewModel>();

            // ── Services ──────────────────────────────────────────
            builder.Services.AddSingleton<ServiceViewModel>();
            builder.Services.AddTransient<ServiceListPage>();
            builder.Services.AddTransient<AddServicePage>();
            builder.Services.AddTransient<AddServiceViewModel>();

            // ── Users ─────────────────────────────────────────────
            builder.Services.AddSingleton<UserViewModel>(sp =>
             new UserViewModel(
                 sp.GetRequiredService<DatabaseService>(),
                 sp.GetRequiredService<SupabaseDataService>(),
                 sp.GetRequiredService<SupabaseRealtimeService>()));
            builder.Services.AddTransient<UserListPage>();
            builder.Services.AddTransient<AddUserPage>();
            builder.Services.AddTransient<AddUserViewModel>();

            builder.Services.AddTransient<LoginViewModel>(sp =>
                    new LoginViewModel(
                        sp.GetRequiredService<AuthenticationService>(),
                        sp.GetRequiredService<SessionService>(),
                        sp.GetRequiredService<RememberMeService>()));
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<AppShell>();

            // Transactions  ─────────────────────────────────────────────
            builder.Services.AddTransient<TransactionViewModel>(s =>
                new TransactionViewModel(
                    s.GetRequiredService<SupabaseDataService>(),
                    s.GetRequiredService<DatabaseService>()));
            builder.Services.AddTransient<Views.TransactionPage>();
            builder.Services.AddTransient<CreateBillViewModel>(sp =>
                    new CreateBillViewModel(
                 sp.GetRequiredService<SupabaseDataService>(),
                 sp.GetRequiredService<BillDraftService>()));
            builder.Services.AddTransient<Views.CreateBillPage>();

            builder.Services.AddTransient<ReceiptViewModel>(sp =>
                new ReceiptViewModel(
                    sp.GetRequiredService<SupabaseDataService>()));
            builder.Services.AddTransient<ReceiptPage>();

            builder.Services.AddTransient<BillSummaryPage>();
            builder.Services.AddTransient<BillSummaryViewModel>();
            builder.Services.AddTransient<PaymentViewModel>();
            builder.Services.AddTransient<PaymentPage>();
            builder.Services.AddTransient<BillDetailsViewModel>();
            builder.Services.AddTransient<BillDetailsPage>();
            builder.Services.AddTransient<BalanceManagementViewModel>();
            builder.Services.AddTransient<BalanceManagementPage>();

            builder.Services.AddTransient<AdditionalPaymentPage>();
            builder.Services.AddTransient<AdditionalPaymentViewModel>();

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

            // ── Cephalometric ─────────────────────────────
            builder.Services.AddTransient<Views.CephalometricRelated.CephalometricMeasurementsPage>();
            builder.Services.AddTransient<CephalometricMeasurementsViewModel>();

            // ── Reports ─────────────────────────────
            builder.Services.AddTransient<Views.ReportRelated.ReportsPage>();
            builder.Services.AddTransient<ReportsViewModel>(sp =>
            new ReportsViewModel(
                sp.GetRequiredService<SupabaseDataService>()));
            builder.Services.AddTransient<Views.ActivityLogPage>();
            // Log
            builder.Services.AddTransient<ActivityLogViewModel>(sp =>
                new ActivityLogViewModel(sp.GetRequiredService<SupabaseDataService>()));

            builder
                .UseMauiApp<App>()
                .UseBottomSheet()
                .UseSkiaSharp()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionCore()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialSymbolsRounded.ttf", "MaterialSymbolsRounded");
                    fonts.AddFont("MaterialSymbolsRoundedFilled.ttf", "MaterialSymbolsRoundedFilled");
                });
#if DEBUG
            builder.Logging.AddDebug();

#endif

            // Removes the native Android underline only from Pickers marked StyleId="NoUnderlinePicker".
#if ANDROID
            PickerHandler.Mapper.AppendToMapping("RemovePickerUnderline", (handler, view) =>
            {
                if (view is Picker picker && picker.StyleId == "NoUnderlinePicker")
                    handler.PlatformView.Background = null;
            });
#endif

            return builder.Build();
        }
    }
}
