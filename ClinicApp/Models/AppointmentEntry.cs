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
        public string Notes { get; set; } = string.Empty;
        public string GoogleTaskId { get; set; } = string.Empty;
        // Stored as "yyyy-MM-dd HH:mm:ss"
        public string AppointmentDateTime { get; set; } = string.Empty;
        // pending / approved / completed / cancelled / rescheduled
        public string Status { get; set; } = "pending";
        public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        public string PatientSupabaseId { get; set; } = string.Empty;

        [SQLite.Ignore]
        public DateTime AppointmentDateTimeParsed
        {
            get
            {
                if (string.IsNullOrEmpty(AppointmentDateTime))
                    return DateTime.MinValue;
                if (DateTime.TryParse(AppointmentDateTime,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var dt))
                    return dt;
                return DateTime.MinValue;
            }
        }

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
            "approved" => Color.FromArgb("#2E7D32"),
            "in-transit" => Color.FromArgb("#F59E0B"),
            "billing" => Color.FromArgb("#7C3AED"),
            "completed" => Color.FromArgb("#2563EB"),
            "pending" => Color.FromArgb("#D97706"),
            "rescheduled" => Color.FromArgb("#0284C7"),
            "cancelled" => Color.FromArgb("#DC2626"),
            _ => Color.FromArgb("#6B7280")
        };

        [Ignore]
        public Color StatusBgColor => Status switch
        {
            "pending" => Color.FromArgb("#FFF3E0"),
            "approved" => Color.FromArgb("#EEF5EE"), // changed: blue tint → pale green
            "completed" => Color.FromArgb("#E8F5E9"),
            "cancelled" => Color.FromArgb("#FCEAEA"),
            "rescheduled" => Color.FromArgb("#FBF4E0"), // changed: purple tint → pale gold
            _ => Color.FromArgb("#F1EFE8")
        };

        [Ignore]
        public string StatusLabel => Status switch
        {
            "approved" => "Approved",
            "in-transit" => "In Transit",
            "billing" => "Billing",
            "completed" => "Completed",
            "pending" => "Pending",
            "rescheduled" => "Rescheduled",
            "cancelled" => "Cancelled",
            _ => Status
        };
    }
}
