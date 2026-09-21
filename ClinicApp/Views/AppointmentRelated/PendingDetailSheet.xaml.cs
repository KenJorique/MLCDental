using The49.Maui.BottomSheet;

namespace ClinicApp.Views.AppointmentRelated
{
    public partial class PendingDetailSheet : BottomSheet
    {
        public PendingDetailSheet()
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
