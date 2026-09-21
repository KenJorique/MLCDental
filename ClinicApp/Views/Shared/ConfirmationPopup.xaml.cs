using CommunityToolkit.Maui.Views;
using System.Linq;

namespace ClinicApp.Views.Shared;

// Whether the confirm button reads as positive (green) or destructive (red) — callers should always state this explicitly.
public enum PopupAction
{
    Positive,
    Destructive
}

// Reusable dimmed-backdrop dialog: Confirm/Cancel mode returns true/false, OK-only mode (showCancelButton: false) is a plain notice.
public partial class ConfirmationPopup : Popup
{
    // Preferred constructor: action type decides the confirm button's color (green/red), not the button text.
    public ConfirmationPopup(string title, string message, string confirmText, PopupAction action,
        bool showCancelButton = true)
    {
        InitializeComponent();
        TitleLabel.Text = title;
        MessageLabel.Text = message;
        ConfirmButton.Text = confirmText;
        ConfirmButton.BackgroundColor = ResolveColor(action);

        if (!showCancelButton)
        {
            CancelButton.IsVisible = false;
            Grid.SetColumnSpan(ConfirmButton, 2);
        }
    }

    // Legacy constructor — infers Positive/Destructive from the button text instead of silently defaulting to green.
    public ConfirmationPopup(string title, string message, string confirmText = "Delete",
        Color? confirmColor = null, bool showCancelButton = true)
        : this(title, message, confirmText, InferAction(confirmText), showCancelButton)
    {
        // An explicit color always wins over the inferred one.
        if (confirmColor is not null)
            ConfirmButton.BackgroundColor = confirmColor;
    }

    // Maps an action type to the app's shared green/red resources.
    static Color ResolveColor(PopupAction action) => action switch
    {
        PopupAction.Destructive => (Color)Application.Current!.Resources["StatusOut"],
        _ => (Color)Application.Current!.Resources["PrimaryGreen"]
    };

    // Guesses destructive vs. positive from common button wording, for callers that haven't been updated yet.
    static PopupAction InferAction(string confirmText)
    {
        var text = confirmText.ToLowerInvariant();
        string[] destructiveWords = { "delete", "remove", "discard", "cancel", "deactivate" };
        return destructiveWords.Any(text.Contains) ? PopupAction.Destructive : PopupAction.Positive;
    }

    // Closes the popup with a "no" result.
    void OnCancelClicked(object? sender, EventArgs e) => Close(false);

    // Closes the popup with a "yes" result.
    void OnConfirmClicked(object? sender, EventArgs e) => Close(true);
}
