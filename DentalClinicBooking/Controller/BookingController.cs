using DentalClinicBooking.Models;
using DentalClinicBooking.Services;
using DentalClinicBooking.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinicBooking.Controller
{
    public class BookingController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly SupabaseService _supabase;

        public BookingController(SupabaseService supabase)
        {
            _supabase = supabase;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new BookingViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(BookingViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var booking = new Booking
                {
                    FullName = model.FullName,
                    Phone = model.Phone,
                    Email = model.Email ?? "",
                    DateOfBirth = model.DateOfBirth,
                    AppointmentDate = model.AppointmentDate.ToUniversalTime(), // Convert to UTC
                    Service = model.Service,
                    Notes = model.Notes,
                    Status = "pending"
                };

                await _supabase.Client.From<Booking>().Insert(booking);

                TempData["PatientName"] = model.FullName;
                TempData["AppointmentDate"] = model.AppointmentDate.ToString("MMMM dd, yyyy h:mm tt");
                TempData["Service"] = model.Service;

                return RedirectToAction("Confirmation");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Booking failed: " + ex.Message);
                return View(model);
            }
        }

        public IActionResult Confirmation()
        {
            return View();
        }
    }
}