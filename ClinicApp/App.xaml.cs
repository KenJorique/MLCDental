//using ClinicApp.Views;

//namespace ClinicApp
//{
//    public partial class App : Application
//    {
//        public App()
//        {
//            InitializeComponent();

//            UserAppTheme = AppTheme.Light;

//            MainPage = new AppShell();
//        }
//    }
//}


using ClinicApp.Views;
namespace ClinicApp
{
    public partial class App : Application
    {
        public App()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                // Keep expanding InnerException until Message is meaningful
                var inner = ex;
                while (inner.InnerException != null)
                    inner = inner.InnerException;

                System.Diagnostics.Debug.WriteLine("=== CRASH CAUSE ===");
                System.Diagnostics.Debug.WriteLine(inner.Message);
                System.Diagnostics.Debug.WriteLine(inner.StackTrace);
                throw;
            }

            UserAppTheme = AppTheme.Light;
            MainPage = new AppShell();
        }
    }
}
