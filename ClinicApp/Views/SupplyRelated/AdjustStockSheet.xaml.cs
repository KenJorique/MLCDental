using The49.Maui.BottomSheet;

namespace ClinicApp.Views.SupplyRelated;

public partial class AdjustStockSheet : BottomSheet
{
    public Func<Task>? OnAddStock { get; set; }
    public Func<Task>? OnReduceStock { get; set; }

    public AdjustStockSheet()
    {
        InitializeComponent();

        // On Android: disable the corner-radius animation that plays when
        // the sheet reaches the top of the screen. Without this the sheet
        // briefly shows a gray/dark background behind the rounded corners,
        // which is what causes the gray bar at the bottom on some devices.
        Showing += (s, e) =>
        {
#if ANDROID
            Controller?.Behavior?.DisableShapeAnimations();
#endif
        };
    }

    private async void OnAddStockTapped(object? sender, TappedEventArgs e)
    {
        await DismissAsync();
        if (OnAddStock is not null)
            await OnAddStock();
    }

    private async void OnReduceStockTapped(object? sender, TappedEventArgs e)
    {
        await DismissAsync();
        if (OnReduceStock is not null)
            await OnReduceStock();
    }
}
