using The49.Maui.BottomSheet;

namespace ClinicApp.Views.AppointmentRelated
{
    public partial class AppointmentDetailSheet : BottomSheet
    {
        public AppointmentDetailSheet()
        {
            InitializeComponent();

            Showing += (s, e) =>
            {
#if ANDROID
                Controller?.Behavior?.DisableShapeAnimations();
#endif
            };
        }
    }
}
