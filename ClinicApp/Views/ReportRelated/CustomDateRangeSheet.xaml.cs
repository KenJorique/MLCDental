using ClinicApp.ViewModels;
using Syncfusion.Maui.Calendar;
using The49.Maui.BottomSheet;

namespace ClinicApp.Views.ReportRelated;

// Range-picker bottom sheet for the Reports page's "Custom" tab.
public partial class CustomDateRangeSheet : BottomSheet
{
    readonly ReportsViewModel vm;

    // Sets up the calendar's date limit and gray/green theme (done in code — Syncfusion.Maui 29.2.10 ignores these properties in XAML).
    public CustomDateRangeSheet(ReportsViewModel vm)
    {
        InitializeComponent();
        this.vm = vm;

        CustomCalendar.MaximumDate = DateTime.Today; // reports can't cover future dates

        // Range endpoints — green
        CustomCalendar.StartRangeSelectionBackground = GetResourceColor("Primary");
        CustomCalendar.EndRangeSelectionBackground = GetResourceColor("Primary");

        // In-between range fill — light green
        CustomCalendar.SelectionBackground = GetResourceColor("Secondary");

        // Whole grid, including adjacent-month and disabled dates — gray
        CustomCalendar.MonthView.Background = GetResourceColor("Gray100");
        CustomCalendar.MonthView.TrailingLeadingDatesBackground = GetResourceColor("Gray100");
        CustomCalendar.MonthView.DisabledDatesBackground = GetResourceColor("Gray100");

        // Today's date ring
        CustomCalendar.TodayHighlightBrush = new SolidColorBrush(GetResourceColor("Gray100"));
        CustomCalendar.MonthView.TodayBackground = GetResourceColor("Gray100");

        // Nav bar ("August 2026" + arrows) — gray
        CustomCalendar.HeaderView.Background = GetResourceColor("Gray100");

        // Month/Year/Decade picker grids (shown when tapping the header) — same gray theme as the day grid.
        // This is a separate style object from MonthView, so it needs its own setup, or it falls back to Syncfusion's default purple.
        CustomCalendar.YearView.Background = GetResourceColor("Gray100");
        CustomCalendar.YearView.TodayBackground = GetResourceColor("Gray100"); // highlights the currently-browsed month/year (e.g. "Sep" while viewing 2026)
        CustomCalendar.YearView.DisabledDatesBackground = GetResourceColor("Gray100"); // months/years past MaximumDate

        // Weekday row (Su Mo Tu...) — gray background, 3-letter day names
        CustomCalendar.MonthView.HeaderView = new CalendarMonthHeaderView
        {
            Background = GetResourceColor("Gray100"),
            TextFormat = "ddd"
        };
    }

    // Looks up a Color from the app's resources by key, falling back to gray if missing.
    static Color GetResourceColor(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Gray;

    // Updates the live preview label as the user taps start/end dates on the calendar.
    void OnCalendarSelectionChanged(object? sender, CalendarSelectionChangedEventArgs e)
    {
        var range = CustomCalendar.SelectedDateRange;
        if (range?.StartDate == null)
        {
            RangeSummaryLabel.Text = "Tap a start date, then an end date";
            return;
        }

        var start = range.StartDate.Value;

        if (range.EndDate == null)
        {
            RangeSummaryLabel.Text = $"{start:MMM d, yyyy} — pick an end date";
            return;
        }

        var end = range.EndDate.Value;
        var days = (end.Date - start.Date).Days + 1;
        RangeSummaryLabel.Text = $"{start:MMM d, yyyy} - {end:MMM d, yyyy} ({days} day{(days == 1 ? "" : "s")})";
    }

    // Dismisses the sheet without applying anything.
    async void OnCancelClicked(object? sender, EventArgs e) => await DismissAsync();

    // Validates the picked range, applies it to the ViewModel, then closes the sheet.
    async void OnApplyClicked(object? sender, EventArgs e)
    {
        var range = CustomCalendar.SelectedDateRange;
        if (range?.StartDate == null || range.EndDate == null)
        {
            await Shell.Current.DisplayAlert("Pick a Range", "Please select both a start and end date.", "OK");
            return;
        }

        await vm.ApplyCustomRange(range.StartDate.Value, range.EndDate.Value);
        await DismissAsync();
    }
}
