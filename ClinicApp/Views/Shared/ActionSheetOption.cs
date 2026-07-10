namespace ClinicApp.Views.Shared;

public class ActionSheetOption
{
    public string Icon { get; set; } = string.Empty;

    /// <summary>Set true to render Icon using MaterialSymbolsRounded font.</summary>
    public bool UseMaterialFont { get; set; } = true;

    public Color? IconColor { get; set; }

    public string Label { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public Color LabelColor { get; set; } = Colors.Black;

    public Color IconBackgroundColor { get; set; } = Color.FromArgb("#F0F0F0");

    /// <summary>Callback invoked when the row is tapped.</summary>
    public Func<Task>? OnTapped { get; set; }
}
