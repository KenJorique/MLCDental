using ClinicApp.Models.ReportModels;
using ClinicApp.ViewModels;

namespace ClinicApp.Views.ReportRelated;

public partial class ReportsPage : ContentPage
{
    readonly ReportsViewModel vm;
    CustomDateRangeSheet? openCustomSheet; // tracked so tab switches / leaving the page can auto-close it

    public ReportsPage(ReportsViewModel vm)
    {
        InitializeComponent();
        BindingContext = this.vm = vm;

        var palette = new List<Brush>
        {
            GetResourceBrush("Primary"),   // Completed / In Stock
            GetResourceBrush("StatusLow"), // Pending / Low Stock
            GetResourceBrush("StatusOut"), // Cancelled / Out of Stock
        };

        AppointmentSeries.PaletteBrushes = palette;
        SupplyChartSeries.PaletteBrushes = palette;

        // Tapping Daily/Weekly/Monthly while the range sheet is open should close it automatically.
        vm.PropertyChanged += async (s, e) =>
        {
            if (e.PropertyName == nameof(ReportsViewModel.SelectedPeriod) && vm.SelectedPeriod != ReportPeriod.Custom)
                await CloseCustomSheetIfOpenAsync();
        };
    }

    static SolidColorBrush GetResourceBrush(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
            return new SolidColorBrush(color);

        // Fallback so a missing resource key doesn't crash the page  
        return new SolidColorBrush(Colors.Gray);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.Dispatch(() => vm.OnAppearing());
    }

    // Leaving this page — back button, switching Shell tabs, etc. — closes the sheet if it's still open.
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _ = CloseCustomSheetIfOpenAsync();
    }

    // Custom tab tapped, OR the applied custom-range label tapped to pick a different range — both land here.
    async void OnCustomTabTapped(object? sender, TappedEventArgs e)
    {
        if (openCustomSheet != null) return; // already open, ignore a repeat tap

        var sheet = new CustomDateRangeSheet(vm);
        openCustomSheet = sheet;
        sheet.Dismissed += (s, args) => openCustomSheet = null; // covers Apply, Cancel, and backdrop-tap dismissal alike
        await sheet.ShowAsync(Window);
    }

    // Closes the tracked sheet if one is open; safe to call even when nothing is open.
    async Task CloseCustomSheetIfOpenAsync()
    {
        if (openCustomSheet == null) return;
        var sheet = openCustomSheet;
        openCustomSheet = null;
        await sheet.DismissAsync();
    }
}
