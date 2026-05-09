using ClinicApp.Views;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.ServicesRelated;
using ClinicApp.Views.UsersRelated;

namespace ClinicApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(PatientPage), typeof(PatientListPage));
            Routing.RegisterRoute(nameof(Views.UsersRelated.UserListPage), typeof(Views.UsersRelated.UserListPage));
            Routing.RegisterRoute(nameof(Views.UsersRelated.AddUserPage), typeof(Views.UsersRelated.AddUserPage));
            Routing.RegisterRoute(nameof(ServicePage), typeof(ServiceListPage));
            Routing.RegisterRoute(nameof(AddPatientPage), typeof(AddPatientPage));
            Routing.RegisterRoute(nameof(PatientDetailsPage), typeof(PatientDetailsPage));
            Routing.RegisterRoute(nameof(Views.ServicesRelated.AddServicePage), typeof(Views.ServicesRelated.AddServicePage));
            Routing.RegisterRoute(nameof(Views.PatientsRelated.AddPatientPage), typeof(Views.PatientsRelated.AddPatientPage));
            Routing.RegisterRoute("PatientDetailsPage", typeof(Views.PatientsRelated.PatientDetailsPage));
        }
    }
}
