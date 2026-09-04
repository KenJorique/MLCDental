namespace ClinicApp.Models.HomeModels;

// One summary line in the Home page's "Needs Attention" card, e.g. "30 Patients with Overdue Bills".
public class NeedsAttentionSummaryRow
{
    public string Text { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty; // page route (with ?filter=...) this row navigates to when tapped
}
