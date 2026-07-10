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
                    AppointmentDate = model.AppointmentDate, // Convert to UTC
                    Service = model.Service,
                    Notes = model.Notes,
                    Status = "pending",
                     CreatedAt = DateTime.UtcNow
                };

                await _supabase.Client.From<Booking>().Insert(booking);

                TempData["PatientName"] = model.FullName;
                TempData["AppointmentDate"] = model.AppointmentDate.ToLocalTime().ToString("MMMM dd, yyyy h:mm tt");
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

        [HttpGet]
        public async Task<IActionResult> GetAvailability(string date)
        {
            if (!DateTime.TryParse(date, out var selectedDate))
                return BadRequest("Invalid date");

            var bookedSlots = await _supabase.GetBookedTimeSlotsAsync(selectedDate);

            var allSlots = new[] { 10, 11, 12, 13, 14, 15 }
                .Select(h => new DateTime(
                    selectedDate.Year, selectedDate.Month,
                    selectedDate.Day, h, 0, 0));

            var slots = allSlots.Select(slot =>
            {
                var count = bookedSlots.Count(b =>
                    b.Hour == slot.Hour && b.Minute == slot.Minute);
                return new
                {
                    time = slot.ToString("HH:mm"),
                    display = slot.ToString("h:00 tt"),
                    count,
                    full = count >= 1  // ← 1 patient per slot max
                };
            });

            // Day is full when all 6 slots are taken (6 patients max per day)
            var dayCount = bookedSlots.Select(b => b.Hour).Distinct().Count();
            var dayFull = dayCount >= 6;

            return Json(new { dayCount, dayFull, slots });
        }
    }
}