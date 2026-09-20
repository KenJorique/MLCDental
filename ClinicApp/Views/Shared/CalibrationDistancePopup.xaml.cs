using CommunityToolkit.Maui.Views;

namespace ClinicApp.Views.Shared;

// Numeric-entry popup for the calibration-distance prompt; closes with the parsed mm value, or null if cancelled.
public partial class CalibrationDistancePopup : Popup
{
    // Sets up the popup.
    public CalibrationDistancePopup()
    {
        InitializeComponent();
    }

    // Validates the entry and closes with the parsed value; shows an inline message instead of closing if it's invalid.
    void OnOkClicked(object? sender, EventArgs e)
    {
        if (float.TryParse(DistanceEntry.Text, out var value) && value > 0)
        {
            Close(value);
            return;
        }

        ValidationLabel.IsVisible = true;
    }

    // Cancels with no value.
    void OnCancelClicked(object? sender, EventArgs e) => Close(null);
}
