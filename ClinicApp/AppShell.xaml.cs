using ClinicApp.Views;
using ClinicApp.Views.CephalometricRelated;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.DentalChart;
using ClinicApp.Views.ServicesRelated;
using ClinicApp.Views.UsersRelated;
using ClinicApp.ViewModels.PatientsRelatedVM;
using ClinicApp.Views.SupplyRelated;
using ClinicApp.Views.AppointmentRelated;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Platform;
using Microsoft.Maui.Graphics;
using ClinicApp.Views.TransactionRelated;

namespace ClinicApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            //Patients
            Routing.RegisterRoute(nameof(AddPatientPage), typeof(AddPatientPage));
            Routing.RegisterRoute(nameof(PatientDetailsPage), typeof(PatientDetailsPage));
            Routing.RegisterRoute(nameof(DentalChartPage), typeof(DentalChartPage));
            Routing.RegisterRoute(nameof(Views.PatientsRelated.TreatmentHistoryPage), typeof(Views.PatientsRelated.TreatmentHistoryPage));
            Routing.RegisterRoute(nameof(CephalometricPage), typeof(CephalometricPage));
            Routing.RegisterRoute( nameof(VisitDetailsPage), typeof(VisitDetailsPage));

            //Services
            Routing.RegisterRoute(nameof(ServiceListPage), typeof(ServiceListPage));
            Routing.RegisterRoute(nameof(AddServicePage), typeof(AddServicePage));

            //Users
            Routing.RegisterRoute(nameof(UserListPage), typeof(UserListPage));
            Routing.RegisterRoute(nameof(AddUserPage), typeof(AddUserPage));

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
            Routing.RegisterRoute(nameof(WalkInBookingPage),  typeof(WalkInBookingPage));
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
