using ClinicApp.ViewModels;

namespace ClinicApp.Views.ReportRelated;

public partial class ReportsPage : ContentPage
{
    readonly ReportsViewModel vm;

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
    }

    static SolidColorBrush GetResourceBrush(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
            return new SolidColorBrush(color);

        // Fallback so a missing resource key doesn't crash the page —
        // shows up as gray if this ever happens, easy to spot.
        return new SolidColorBrush(Colors.Gray);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.Dispatch(() => vm.OnAppearing());
    }
}
