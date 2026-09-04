using ClinicApp.Helpers;
using Newtonsoft.Json;
using SQLite;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Column = Supabase.Postgrest.Attributes.ColumnAttribute;
using PrimaryKey = Supabase.Postgrest.Attributes.PrimaryKeyAttribute;
using Table = Supabase.Postgrest.Attributes.TableAttribute;

namespace ClinicApp.Models.SupabaseModels;

[Table("activity_log")]
public class SupabaseActivityLog : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    // One of: NewPatient, PatientUpdated, Payment, AppointmentCompleted, NewBooking,
    // AppointmentCancelled, AppointmentRescheduled, StockChange, NewSupplyItem,
    // NewService, ServicePriceChanged.
    [Column("type")]
    public string Type { get; set; } = string.Empty;

    // Human-readable line, e.g. "Maria Santos paid ₱1,500".
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    // Optional patient/bill/supply id, for a future tap-to-open.
    [Column("related_id")]
    public string? RelatedId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Icon glyph per activity type, for the Recent Activity list.
    [Ignore]
    [JsonIgnore]
    public string IconGlyph => Type switch
    {
        "NewPatient" => "\ue7fe",
        "PatientUpdated" => "\ue3c9",
        "Payment" => "\ue8a1",
        "AppointmentCompleted" => "\ue614",
        "NewBooking" => "\ue878",
        "AppointmentCancelled" => "\ue615",
        "AppointmentRescheduled" => "\uf540",
        "StockChange" => "\ue1a1",
        "NewSupplyItem" => "\ue145",
        "NewService" => "\ue145",
        "ServicePriceChanged" => "\ue227",
        _ => "\ue88e"
    };

    // Icon color per activity type.
    [Ignore]
    [JsonIgnore]
    public Color IconColor => Type switch
    {
        "NewPatient" => Color.FromArgb("#1565C0"),
        "PatientUpdated" => Color.FromArgb("#6A1B9A"),
        "Payment" => Color.FromArgb("#2E7D32"),
        "AppointmentCompleted" => Color.FromArgb("#2E7D32"),
        "NewBooking" => Color.FromArgb("#C8A84B"),
        "AppointmentCancelled" => Color.FromArgb("#C62828"),
        "AppointmentRescheduled" => Color.FromArgb("#F57C00"),
        "StockChange" => Color.FromArgb("#6A1B9A"),
        "NewSupplyItem" => Color.FromArgb("#00897B"),
        "NewService" => Color.FromArgb("#3949AB"),
        "ServicePriceChanged" => Color.FromArgb("#F57C00"),
        _ => Color.FromArgb("#6E6E6E")
    };

    // Relative time under 30 minutes old ("Just now", "5m ago"); exact time/date beyond that.
    [Ignore]
    [JsonIgnore]
    public string TimeAgoDisplay
    {
        get
        {
            var local = CreatedAt.ToLocalSafe();
            var span = DateTime.Now - local;

            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 30) return $"{(int)span.TotalMinutes}m ago";

            if (local.Date == DateTime.Today) return local.ToString("h:mm tt");
            if (local.Year == DateTime.Today.Year) return local.ToString("MMM d, h:mm tt");
            return local.ToString("MMM d, yyyy");
        }
    }
}
