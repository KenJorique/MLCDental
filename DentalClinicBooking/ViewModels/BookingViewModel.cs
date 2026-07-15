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

        //[Required(ErrorMessage = "Date of birth is required")]
        //[Display(Name = "Date of Birth")]
        //[DataType(DataType.Date)]
        //public DateTime? DateOfBirth { get; set; }

        // In BookingViewModel.cs, update AppointmentDate property:
        [Required(ErrorMessage = "Please choose an appointment date")]
        [Display(Name = "Preferred Appointment Date")]
        [DataType(DataType.DateTime)]
        public DateTime AppointmentDate { get; set; } = DateTime.Now.AddDays(1).Date.AddHours(10).AddMinutes(30);

        // Add these helper properties for JS validation:
        public string MinTime => "10:00";
        public string MaxTime => "15:00"; // 3:30 PM is last slot (30 min before 4PM closing)

        //[Required(ErrorMessage = "Please select a service")]
        //[Display(Name = "Service")]
        //public string Service { get; set; } = string.Empty;

        [Display(Name = "Additional Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }

        public List<string> AvailableServices => new()
        {
            "General Checkup",
            "Teeth Cleaning",
            "Tooth Extraction",
            "Dental Filling",
            "Orthodontics",
            "Teeth Whitening",
            "Dentures",
            "X-Ray"
        };
    }
}