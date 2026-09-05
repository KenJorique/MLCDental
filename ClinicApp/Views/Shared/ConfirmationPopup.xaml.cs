using CommunityToolkit.Maui.Views;
using static Android.Webkit.ConsoleMessage;

namespace ClinicApp.Views.Shared;

// Reusable Yes/No confirmation dialog with a dimmed backdrop.
// Closes with a bool result: true = confirmed, false = cancelled.
public partial class ConfirmationPopup : Popup
{
    // Fills in the title, message, and confirm-button text/color.
    public ConfirmationPopup(string title, string message, string confirmText = "Delete", Color? confirmColor = null)
    {
        InitializeComponent();
        TitleLabel.Text = title;
        MessageLabel.Text = message;
        ConfirmButton.Text = confirmText;
        if (confirmColor is not null)
            ConfirmButton.BackgroundColor = confirmColor;
    }

    // Closes the popup with a "no" result.
    void OnCancelClicked(object? sender, EventArgs e) => Close(false);

    // Closes the popup with a "yes" result.
    void OnConfirmClicked(object? sender, EventArgs e) => Close(true);
}
