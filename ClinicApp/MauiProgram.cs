using ClinicApp.Services;
using ClinicApp.ViewModels;
using ClinicApp.ViewModels.CephalometricVM;
using ClinicApp.ViewModels.PatientsRelatedVM;
using ClinicApp.ViewModels.DentalChart;
using ClinicApp.ViewModels.ServicesRelatedVM;
using ClinicApp.ViewModels.UsersRelated;
using ClinicApp.Views;
using ClinicApp.Views.CephalometricRelated;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.DentalChart;
using ClinicApp.Views.ServicesRelated;
using ClinicApp.Views.UsersRelated;
using Microsoft.Extensions.Logging;
using ClinicApp.Views.SupplyRelated;
using ClinicApp.ViewModels.SupplyVM;
using The49.Maui.BottomSheet;
using CommunityToolkit.Maui;

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

            builder.Services.AddTransient<DentalChartPage>();
            builder.Services.AddTransient<DentalChartViewModel>();
            builder.Services.AddTransient<TreatmentHistoryPage>();
            builder.Services.AddTransient<TreatmentHistoryViewModel>();
            builder.Services.AddTransient<CephalometricPage>();
            builder.Services.AddTransient<CephalometricViewModel>();

            builder.Services.AddTransient<ServiceListPage>();
            builder.Services.AddSingleton<ServiceViewModel>();
            builder.Services.AddTransient<AddServicePage>();
            builder.Services.AddTransient<AddServiceViewModel>();

            builder.Services.AddTransient<UserListPage>();
            builder.Services.AddSingleton<UserViewModel>();
            builder.Services.AddTransient<AddUserPage>();
            builder.Services.AddTransient<AddUserViewModel>();

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
                .UseBottomSheet()           // The49.Maui.BottomSheet
                .UseMauiCommunityToolkit()   // status bar
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

            return builder.Build();
        }
    }
}
