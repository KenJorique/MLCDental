using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;
using SQLite;
using Table = Supabase.Postgrest.Attributes.TableAttribute;
using PrimaryKey = Supabase.Postgrest.Attributes.PrimaryKeyAttribute;
using Column = Supabase.Postgrest.Attributes.ColumnAttribute;
using ClinicApp.Helpers;

namespace ClinicApp.Models.SupabaseModels;

[Table("treatment_sequences")]
public class SupabaseTreatmentSequence : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("patient_id")]
    public string PatientId { get; set; } = string.Empty;

    [Column("patient_name")]
    public string PatientName { get; set; } = string.Empty;

    [Column("service_id")]
    public string ServiceId { get; set; } = string.Empty;

    [Column("service_name")]
    public string ServiceName { get; set; } = string.Empty;

    [Column("session_number")]
    public int SessionNumber { get; set; } = 1;

    [Column("total_sessions")]
    public int TotalSessions { get; set; } = 1;

    [Column("source_appointment_id")]
    public string? SourceAppointmentId { get; set; }

    [Column("next_appointment_id")]
    public string? NextAppointmentId { get; set; }

    // completed | awaiting_schedule | scheduled
    [Column("status")]
    public string Status { get; set; } = "completed";

    [Column("recommended_date")]
    public DateTime? RecommendedDate { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Ignore]
    [JsonIgnore]
    public bool IsFinalSession => SessionNumber >= TotalSessions;

    [Ignore]
    [JsonIgnore]
    public string SessionDisplay => $"Session {SessionNumber} of {TotalSessions}";

    [Ignore]
    [JsonIgnore]
    public string RecommendedDateDisplay =>
        RecommendedDate.HasValue
            ? RecommendedDate.Value.ToLocalTime().ToString("MMM dd, yyyy")
            : "—";

    [Ignore]
    [JsonIgnore]
    public Color StatusColor => Status switch
    {
        "scheduled" => Color.FromArgb("#2563EB"),
        "awaiting_schedule" => Color.FromArgb("#D97706"),
        "completed" => Color.FromArgb("#2E7D32"),
        _ => Color.FromArgb("#6B7280")
    };
}