using ClinicApp.Services;
using ClinicApp.ViewModels;
using ClinicApp.ViewModels.PatientsRelatedVM;
using ClinicApp.ViewModels.ServicesRelatedVM;
using ClinicApp.ViewModels.UsersRelated;
using ClinicApp.Views;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.ServicesRelated;
using ClinicApp.Views.UsersRelated;
using Microsoft.Extensions.Logging;

namespace ClinicApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder.Services.AddSingleton<DatabaseService>();

            builder.Services.AddSingleton<HomePage>();
            builder.Services.AddSingleton<AppointmentPage>();
            builder.Services.AddSingleton<MenuPage>();
            builder.Services.AddSingleton<MenuViewModel>();
            builder.Services.AddSingleton<PatientListPage>();
            builder.Services.AddSingleton<PatientListViewModel>();
            builder.Services.AddTransient<AddPatientPage>();
            builder.Services.AddTransient<AddPatientViewModel>();
            builder.Services.AddTransient<PatientDetailsPage>();
            builder.Services.AddTransient<PatientDetailsViewModel>();
            builder.Services.AddTransient<ServiceListPage>();
            builder.Services.AddSingleton<ServiceViewModel>();
            builder.Services.AddTransient<AddServicePage>();
            builder.Services.AddTransient<AddServiceViewModel>();
            builder.Services.AddTransient<UserListPage>();
            builder.Services.AddSingleton<UserViewModel>();
            builder.Services.AddTransient<AddUserPage>();
            builder.Services.AddTransient<AddUserViewModel>();

            builder
                .UseMauiApp<App>()
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
