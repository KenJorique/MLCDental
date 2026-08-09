using The49.Maui.BottomSheet;

namespace ClinicApp.Views
{
    public partial class CreateBillSummarySheet : BottomSheet
    {
        public CreateBillSummarySheet()
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
