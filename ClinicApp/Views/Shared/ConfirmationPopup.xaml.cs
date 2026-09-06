using CommunityToolkit.Maui.Views;

namespace ClinicApp.Views.Shared;

// Reusable dialog with a dimmed backdrop. Works two ways:
// - Confirm/Cancel mode (default): returns true/false.
// - OK-only mode (showCancelButton: false): a plain notice, returns true when dismissed.
public partial class ConfirmationPopup : Popup
{
    // Fills in the title, message, and confirm-button text/color; hides Cancel for a plain notice.
    public ConfirmationPopup(string title, string message, string confirmText = "Delete",
        Color? confirmColor = null, bool showCancelButton = true)
    {
        InitializeComponent();
        TitleLabel.Text = title;
        MessageLabel.Text = message;
        ConfirmButton.Text = confirmText;
        if (confirmColor is not null)
            ConfirmButton.BackgroundColor = confirmColor;

        if (!showCancelButton)
        {
            CancelButton.IsVisible = false;
            Grid.SetColumnSpan(ConfirmButton, 2);
        }
    }

    // Closes the popup with a "no" result.
    void OnCancelClicked(object? sender, EventArgs e) => Close(false);

    // Closes the popup with a "yes" result.
    void OnConfirmClicked(object? sender, EventArgs e) => Close(true);
}
