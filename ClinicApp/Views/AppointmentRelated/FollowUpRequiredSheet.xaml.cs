using The49.Maui.BottomSheet;

namespace ClinicApp.Views.AppointmentRelated
{
    public partial class FollowUpRequiredSheet : BottomSheet
    {
        // Sets up the sheet and disables shape animations on Android.
        public FollowUpRequiredSheet()
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