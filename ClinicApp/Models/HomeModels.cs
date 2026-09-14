namespace ClinicApp.Models.HomeModels;

// One summary line in the Home page's "Needs Attention" card.
public class NeedsAttentionSummaryRow
{
    public string Text { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = string.Empty;
    public Color IconColor { get; set; } = Colors.Gray;
    public string Route { get; set; } = string.Empty; // page route (with ?filter=...) this row navigates to when tapped
}
