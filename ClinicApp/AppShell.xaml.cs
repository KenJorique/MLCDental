using ClinicApp.ViewModels.PatientsRelatedVM;
using ClinicApp.Views;
using ClinicApp.Views.AppointmentRelated;
using ClinicApp.Views.CephalometricRelated;
using ClinicApp.Views.DentalChart;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.ServicesRelated;
using ClinicApp.Views.SupplyRelated;
using ClinicApp.Views.TransactionRelated;
using ClinicApp.Views.UsersRelated;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Platform;
using Microsoft.Maui.Graphics;
using Supabase.Gotrue;
using ClinicApp.Services;

namespace ClinicApp
{
    public partial class AppShell : Shell
    {
        private readonly SessionService _session;
        public AppShell(SessionService session)
        {
            InitializeComponent();

            _session = session;

            //Patients
            Routing.RegisterRoute(nameof(AddPatientPage), typeof(AddPatientPage));
            Routing.RegisterRoute(nameof(PatientDetailsPage), typeof(PatientDetailsPage));
            Routing.RegisterRoute(nameof(DentalChartPage), typeof(DentalChartPage));
            Routing.RegisterRoute(nameof(Views.PatientsRelated.TreatmentHistoryPage), typeof(Views.PatientsRelated.TreatmentHistoryPage));
            Routing.RegisterRoute(nameof(CephalometricPage), typeof(CephalometricPage));
            Routing.RegisterRoute(nameof(VisitDetailsPage), typeof(VisitDetailsPage));

            //Services
            Routing.RegisterRoute(nameof(ServiceListPage), typeof(ServiceListPage));
            Routing.RegisterRoute(nameof(AddServicePage), typeof(AddServicePage));

            //Users
            Routing.RegisterRoute(nameof(UserListPage), typeof(UserListPage));
            Routing.RegisterRoute(nameof(AddUserPage), typeof(AddUserPage));
            // NOTE: LoginPage is intentionally NOT part of Shell routing
            // anymore — it's shown by swapping Application.MainPage (see
            // NavigationHelper.cs) instead of Shell.GoToAsync, because a
            // page registered only via RegisterRoute can't be the sole
            // page on the nav stack. Registering it here is harmless but
            // unused; removed to avoid implying it still works that way.

            //Supply
            Routing.RegisterRoute(nameof(SupplyListPage), typeof(SupplyListPage));
            Routing.RegisterRoute(nameof(AddSupplyPage), typeof(AddSupplyPage));
            Routing.RegisterRoute(nameof(SupplyInfoPage), typeof(SupplyInfoPage));
            Routing.RegisterRoute(nameof(AddStockPage), typeof(AddStockPage));
            Routing.RegisterRoute(nameof(ReduceStockPage), typeof(ReduceStockPage));
            Routing.RegisterRoute(nameof(StockHistoryPage), typeof(StockHistoryPage));

            //Appointments
            Routing.RegisterRoute(nameof(AppointmentPage), typeof(AppointmentPage));
            Routing.RegisterRoute(nameof(ReschedulePage), typeof(ReschedulePage));
            Routing.RegisterRoute(nameof(WalkInBookingPage), typeof(WalkInBookingPage));
            Routing.RegisterRoute(nameof(InProcedurePage), typeof(InProcedurePage));

            //Google Sign In
            Routing.RegisterRoute(nameof(GoogleSignInPage), typeof(GoogleSignInPage));

            //Cephalometric
            Routing.RegisterRoute(nameof(CephalometricPage), typeof(CephalometricPage));
            Routing.RegisterRoute("measurements", typeof(Views.CephalometricRelated.CephalometricMeasurementsPage));

            //Transactions
            Routing.RegisterRoute(nameof(TransactionPage), typeof(TransactionPage));
            Routing.RegisterRoute(nameof(CreateBillPage), typeof(CreateBillPage));
            Routing.RegisterRoute(nameof(ReceiptPage), typeof(ReceiptPage));
            Routing.RegisterRoute(nameof(ServiceSummaryPage), typeof(ServiceSummaryPage));
            Routing.RegisterRoute(nameof(BillSummaryPage), typeof(BillSummaryPage));
            Routing.RegisterRoute(nameof(PaymentPage), typeof(PaymentPage));
            Routing.RegisterRoute(nameof(BillDetailsPage), typeof(BillDetailsPage));
            Routing.RegisterRoute(nameof(BalanceManagementPage), typeof(BalanceManagementPage));

            //Reports
            Routing.RegisterRoute(nameof(Views.ReportRelated.ReportsPage), typeof(Views.ReportRelated.ReportsPage));

            Navigating += OnShellNavigating;
            _session.SessionEnded += OnSessionEnded;
        }

        // Defensive only now: by construction, AppShell is never
        // Application.MainPage unless login already succeeded (see
        // App.xaml.cs / NavigationHelper.ShowApp()). This still guards the
        // in-between moment where SessionEnded has fired (inactivity
        // timeout) but the MainPage swap hasn't completed yet, and it
        // still enforces the Secretary route restriction unconditionally.
        private void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
        {
            var target = e.Target?.Location?.OriginalString ?? "";

            if (!_session.IsAuthenticated)
            {
                e.Cancel();
                NavigationHelper.ShowLogin(); // ── CHANGED: was Shell.Current.GoToAsync("//LoginPage")
                return;
            }

            bool isSecretaryRestrictedRoute =
                target.Contains(nameof(DentalChartPage)) ||
                target.Contains(nameof(CephalometricPage)) ||
                target.Contains("measurements");

            if (_session.IsSecretary && isSecretaryRestrictedRoute)
            {
                e.Cancel();
                _ = Shell.Current.DisplayAlert(
                    "Not available",
                    "This section is only available to Dentist accounts.",
                    "OK");
                return;
            }

            _session.NotifyActivity();
        }

        // ── CHANGED: swap MainPage instead of Shell.GoToAsync ──
        private void OnSessionEnded()
        {
            MainThread.BeginInvokeOnMainThread(NavigationHelper.ShowLogin);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

#if ANDROID
            StatusBar.SetColor(Colors.White);
            StatusBar.SetStyle(CommunityToolkit.Maui.Core.StatusBarStyle.DarkContent);
#endif
        }
    }
}