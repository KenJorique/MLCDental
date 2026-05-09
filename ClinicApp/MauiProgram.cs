using ClinicApp.Services;
using ClinicApp.ViewModels;
using ClinicApp.ViewModels.PatientsRelatedVM;
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
            builder.Services.AddSingleton<PatientListViewModel>();
            builder.Services.AddSingleton<PatientListPage>();
            builder.Services.AddSingleton<ServicePage>();
            builder.Services.AddTransient<AddUserPage>();
            builder.Services.AddTransient<AddUserViewModel>();
            builder.Services.AddSingleton<UserListPage>();
            builder.Services.AddSingleton<UserViewModel>();
            builder.Services.AddSingleton<ServiceListPage>();
            builder.Services.AddTransient<AddServicePage>();
            builder.Services.AddSingleton<ServiceViewModel>();
            builder.Services.AddTransient<AddPatientViewModel>();
            builder.Services.AddTransient<AddPatientPage>();
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
