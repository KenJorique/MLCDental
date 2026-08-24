using System.ComponentModel.DataAnnotations;

namespace DentalClinicBooking.ViewModels
{
    public class BookingViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^09\d{9}$",
      ErrorMessage = "Phone must start with 09 and contain 11 digits")]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Enter a valid email")]
        [Display(Name = "Email Address")]
        public string? Email { get; set; }

        // The two fields the form's hidden inputs actually bind to now
        // (PH-local date/time, set by JS when a slot is picked). The
        // controller parses these together ("yyyy-MM-dd" + "HH:mm") and
        // converts to true UTC via TimeZoneInfo, then sets AppointmentDate
        // below from that result — so these are the real input, and
        // AppointmentDate is a server-computed output, not a bound field.
        [Required(ErrorMessage = "Please choose an appointment date")]
        public string AppointmentDateStr { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose a time slot")]
        public string AppointmentTimeStr { get; set; } = string.Empty;

        // Set by the controller after parsing AppointmentDateStr +
        // AppointmentTimeStr above — not bound directly from the form,
        // so no [Required]/[DataType] here (those would be misleading
        // now that nothing posts to this field directly).
        public DateTime AppointmentDate { get; set; }

        [Display(Name = "Additional Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }


    }
}