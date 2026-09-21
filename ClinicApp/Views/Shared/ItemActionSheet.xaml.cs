using The49.Maui.BottomSheet;
namespace ClinicApp.Views.Shared;

public partial class ItemActionSheet : BottomSheet
{
    private bool _isFullyShown = false;

    // Sets up show/hide event guards; IsCancelable stays default (True) so drag works.
    public ItemActionSheet()
    {
        InitializeComponent();

        // Track open animation and disable Android shape animation.
        Showing += (s, e) =>
        {
            _isFullyShown = false;
#if ANDROID
            Controller?.Behavior?.DisableShapeAnimations();
#endif
        };

        Shown += (s, e) =>
        {
            _isFullyShown = true;
        };

        Dismissed += (s, e) =>
        {
            _isFullyShown = false;
        };
    }

    // Fills in title, subtitle, and action rows for this sheet.
    public void Configure(string title, string subtitle, IEnumerable<ActionSheetOption> options)
    {
        TitleLabel.Text = title;
        SubtitleLabel.Text = subtitle;
        SubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace(subtitle);
        ActionsContainer.Children.Clear();
        foreach (var option in options)
            ActionsContainer.Children.Add(BuildRow(option));
    }

    // Builds one tappable row (icon + label + subtitle + chevron) for an option.
    private Border BuildRow(ActionSheetOption option)
    {
        // Icon glyph shown inside the round icon background.
        var iconLabel = new Label
        {
            Text = option.Icon,
            FontSize = 22,
            FontFamily = option.UseMaterialFont ? "MaterialSymbolsRounded" : null,
            TextColor = option.IconColor ?? option.LabelColor,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        // Round colored background behind the icon.
        var iconContainer = new Border
        {
            BackgroundColor = option.IconBackgroundColor,
            StrokeThickness = 0,
            WidthRequest = 44,
            HeightRequest = 44,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
            Content = iconLabel,
        };

        // Main bold label text.
        var mainLabel = new Label
        {
            Text = option.Label,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = option.LabelColor,
            VerticalOptions = LayoutOptions.Center,
        };

        // Smaller gray subtitle text, hidden if empty.
        var subtitleLabel = new Label
        {
            Text = option.Subtitle,
            FontSize = 12,
            TextColor = Color.FromArgb("#9E9E9E"),
            IsVisible = !string.IsNullOrWhiteSpace(option.Subtitle),
            VerticalOptions = LayoutOptions.Center,
        };

        // Stacks main label and subtitle vertically.
        var textStack = new VerticalStackLayout
        {
            Margin = new Thickness(12, 0, 0, 0),
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children = { mainLabel, subtitleLabel },
        };

        // Right-side arrow indicator.
        var chevron = new Label
        {
            Text = "\ue5cc",
            FontFamily = "MaterialSymbolsRounded",
            FontSize = 20,
            TextColor = Color.FromArgb("#BDBDBD"),
            VerticalOptions = LayoutOptions.Center,
        };

        // Lays out icon, text, and chevron in a row.
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing = 0,
        };
        grid.Add(iconContainer, 0);
        grid.Add(textStack, 1);
        grid.Add(chevron, 2);

        // Card-style wrapper for the whole row.
        var row = new Border
        {
            Margin = new Thickness(16, 0, 16, 10),
            Padding = new Thickness(14, 12),
            BackgroundColor = Colors.White,
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#EEEEEE"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            Content = grid,
        };

        // Dismisses the sheet then runs the option's action on tap.
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) =>
        {
            if (!_isFullyShown) return; // ignore taps mid open-animation
            await DismissAsync();
            if (option.OnTapped is not null)
                await option.OnTapped();
        };
        row.GestureRecognizers.Add(tap);
        return row;
    }
}
