using The49.Maui.BottomSheet;

namespace ClinicApp.Views.Shared;

public partial class ItemActionSheet : BottomSheet
{
    public ItemActionSheet()
    {
        InitializeComponent();

        Showing += (s, e) =>
        {
#if ANDROID
            Controller?.Behavior?.DisableShapeAnimations();
#endif
        };
    }

    public void Configure(string title, string subtitle, IEnumerable<ActionSheetOption> options)
    {
        TitleLabel.Text = title;
        SubtitleLabel.Text = subtitle;
        SubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace(subtitle);

        ActionsContainer.Children.Clear();

        foreach (var option in options)
            ActionsContainer.Children.Add(BuildRow(option));
    }

    private Frame BuildRow(ActionSheetOption option)
    {
        // Icon — use MaterialSymbolsRounded font if Icon is a unicode string, else emoji
        var iconLabel = new Label
        {
            Text = option.Icon,
            FontSize = 22,
            FontFamily = option.UseMaterialFont ? "MaterialSymbolsRounded" : null,
            TextColor = option.IconColor ?? option.LabelColor,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        var iconFrame = new Frame
        {
            BackgroundColor = option.IconBackgroundColor,
            BorderColor = Colors.Transparent,
            CornerRadius = 22,
            Padding = new Thickness(0),
            WidthRequest = 44,
            HeightRequest = 44,
            HasShadow = false,
            Content = iconLabel,
        };

        var mainLabel = new Label
        {
            Text = option.Label,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = option.LabelColor,
        };

        var subtitleLabel = new Label
        {
            Text = option.Subtitle,
            FontSize = 12,
            TextColor = Colors.Gray,
            IsVisible = !string.IsNullOrWhiteSpace(option.Subtitle),
        };

        var textStack = new VerticalStackLayout
        {
            Margin = new Thickness(12, 0, 0, 0),
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children = { mainLabel, subtitleLabel },
        };

        var chevron = new Label
        {
            Text = "\ue5cc", // chevron_right in Material Symbols
            FontFamily = "MaterialSymbolsRounded",
            FontSize = 22,
            TextColor = Colors.Gray,
            VerticalOptions = LayoutOptions.Center,
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        grid.Add(iconFrame, 0);
        grid.Add(textStack, 1);
        grid.Add(chevron, 2);

        var frame = new Frame
        {
            Margin = new Thickness(16, 0, 16, 10),
            Padding = new Thickness(16, 14),
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            CornerRadius = 12,
            HasShadow = false,
            Content = grid,
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) =>
        {
            await DismissAsync();
            if (option.OnTapped is not null)
                await option.OnTapped();
        };
        frame.GestureRecognizers.Add(tap);

        return frame;
    }
}
