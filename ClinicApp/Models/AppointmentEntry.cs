using SQLite;

namespace ClinicApp.Models
{
    [Table("AppointmentEntry")]
    public class AppointmentEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string SupabaseBookingId { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string GoogleTaskId { get; set; } = string.Empty;

        // Stored as "yyyy-MM-dd HH:mm:ss"
        public string AppointmentDateTime { get; set; } = string.Empty;

        // pending / approved / completed / cancelled / rescheduled
        public string Status { get; set; } = "pending";

        public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        [Ignore]
        public DateTime AppointmentDateTimeParsed =>
            DateTime.TryParse(AppointmentDateTime, out var dt) ? dt : DateTime.MinValue;

        [Ignore]
        public string TimeDisplay =>
            AppointmentDateTimeParsed == DateTime.MinValue
                ? "" : AppointmentDateTimeParsed.ToString("h:mm");

        [Ignore]
        public string AmPm =>
            AppointmentDateTimeParsed == DateTime.MinValue
                ? "" : AppointmentDateTimeParsed.ToString("tt");

        [Ignore]
        public string DateDisplay =>
            AppointmentDateTimeParsed == DateTime.MinValue
                ? "" : AppointmentDateTimeParsed.ToString("MMM dd, yyyy");

        [Ignore]
        public string Initials
        {
            get
            {
                var parts = PatientName.Trim().Split(' ');
                if (parts.Length >= 2)
                    return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
                return PatientName.Length > 0
                    ? PatientName[0].ToString().ToUpper() : "?";
            }
        }

        [Ignore]
        public Color StatusColor => Status switch
        {
            "pending" => Color.FromArgb("#E65100"),
            "approved" => Color.FromArgb("#1565C0"),
            "completed" => Color.FromArgb("#2E7D32"),
            "cancelled" => Color.FromArgb("#C62828"),
            "rescheduled" => Color.FromArgb("#6A1B9A"),
            _ => Color.FromArgb("#888780")
        };

        [Ignore]
        public Color StatusBgColor => Status switch
        {
            "pending" => Color.FromArgb("#FFF3E0"),
            "approved" => Color.FromArgb("#E3F2FD"),
            "completed" => Color.FromArgb("#E8F5E9"),
            "cancelled" => Color.FromArgb("#FCEAEA"),
            "rescheduled" => Color.FromArgb("#F3E5F5"),
            _ => Color.FromArgb("#F1EFE8")
        };

        [Ignore]
        public string StatusLabel => Status switch
        {
            "pending" => "Pending",
            "approved" => "Approved",
            "completed" => "Completed",
            "cancelled" => "Cancelled",
            "rescheduled" => "Rescheduled",
            _ => Status
        };
    }
}