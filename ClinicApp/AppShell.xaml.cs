using ClinicApp.Views;
using ClinicApp.Views.CephalometricRelated;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.DentalChart;
using ClinicApp.Views.ServicesRelated;
using ClinicApp.Views.UsersRelated;
using ClinicApp.Views.SupplyRelated;
using ClinicApp.Views.AppointmentRelated;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Platform;
using Microsoft.Maui.Graphics;

namespace ClinicApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(AddPatientPage), typeof(AddPatientPage));
            Routing.RegisterRoute(nameof(PatientDetailsPage), typeof(PatientDetailsPage));
            Routing.RegisterRoute(nameof(DentalChartPage), typeof(DentalChartPage));
            Routing.RegisterRoute(nameof(TreatmentHistoryPage), typeof(TreatmentHistoryPage));
            Routing.RegisterRoute(nameof(CephalometricPage), typeof(CephalometricPage));

            Routing.RegisterRoute(nameof(ServiceListPage), typeof(ServiceListPage));
            Routing.RegisterRoute(nameof(AddServicePage), typeof(AddServicePage));

            Routing.RegisterRoute(nameof(UserListPage), typeof(UserListPage));
            Routing.RegisterRoute(nameof(AddUserPage), typeof(AddUserPage));

            Routing.RegisterRoute(nameof(SupplyListPage), typeof(SupplyListPage));
            Routing.RegisterRoute(nameof(AddSupplyPage), typeof(AddSupplyPage));
            Routing.RegisterRoute(nameof(SupplyInfoPage), typeof(SupplyInfoPage));
            Routing.RegisterRoute(nameof(AddStockPage), typeof(AddStockPage));
            Routing.RegisterRoute(nameof(ReduceStockPage), typeof(ReduceStockPage));
            Routing.RegisterRoute(nameof(StockHistoryPage), typeof(StockHistoryPage));

            Routing.RegisterRoute(nameof(AppointmentPage), typeof(AppointmentPage));
            Routing.RegisterRoute(nameof(ReschedulePage), typeof(ReschedulePage));

            Routing.RegisterRoute(nameof(GoogleSignInPage), typeof(GoogleSignInPage));

            Routing.RegisterRoute(nameof(TransactionPage), typeof(TransactionPage));
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
