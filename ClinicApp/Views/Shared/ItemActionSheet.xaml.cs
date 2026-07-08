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

    private Border BuildRow(ActionSheetOption option)
    {
        var iconLabel = new Label
        {
            Text = option.Icon,
            FontSize = 22,
            FontFamily = option.UseMaterialFont ? "MaterialSymbolsRounded" : null,
            TextColor = option.IconColor ?? option.LabelColor,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        var iconContainer = new Border
        {
            BackgroundColor = option.IconBackgroundColor,
            StrokeThickness = 0,
            WidthRequest = 44,
            HeightRequest = 44,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
            Content = iconLabel,
        };

        var mainLabel = new Label
        {
            Text = option.Label,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = option.LabelColor,
            VerticalOptions = LayoutOptions.Center,
        };

        var subtitleLabel = new Label
        {
            Text = option.Subtitle,
            FontSize = 12,
            TextColor = Color.FromArgb("#9E9E9E"),
            IsVisible = !string.IsNullOrWhiteSpace(option.Subtitle),
            VerticalOptions = LayoutOptions.Center,
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
            Text = "\ue5cc",
            FontFamily = "MaterialSymbolsRounded",
            FontSize = 20,
            TextColor = Color.FromArgb("#BDBDBD"),
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
            ColumnSpacing = 0,
        };
        grid.Add(iconContainer, 0);
        grid.Add(textStack, 1);
        grid.Add(chevron, 2);

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

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) =>
        {
            await DismissAsync();
            if (option.OnTapped is not null)
                await option.OnTapped();
        };
        row.GestureRecognizers.Add(tap);

        return row;
    }
}
