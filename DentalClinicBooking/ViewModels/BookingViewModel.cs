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

        // In BookingViewModel.cs, update AppointmentDate property:
        [Required(ErrorMessage = "Please choose an appointment date")]
        [Display(Name = "Preferred Appointment Date")]
        [DataType(DataType.DateTime)]
        public DateTime AppointmentDate { get; set; } = DateTime.Now.AddDays(1).Date.AddHours(10).AddMinutes(30);

        [Display(Name = "Additional Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }


    }
}