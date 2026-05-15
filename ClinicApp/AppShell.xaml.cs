using ClinicApp.Views;
using ClinicApp.Views.CephalometricRelated;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.DentalChart;
using ClinicApp.Views.ServicesRelated;
using ClinicApp.Views.UsersRelated;

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
            Routing.RegisterRoute(nameof(CephalometricPage), typeof(CephalometricPage));

            Routing.RegisterRoute(nameof(ServiceListPage), typeof(ServiceListPage));
            Routing.RegisterRoute(nameof(AddServicePage), typeof(AddServicePage));

            Routing.RegisterRoute(nameof(UserListPage), typeof(UserListPage));
            Routing.RegisterRoute(nameof(AddUserPage), typeof(AddUserPage));
        }
    }
}
