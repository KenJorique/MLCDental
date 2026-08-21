using DentalClinicBooking.Models;
using DentalClinicBooking.Services;
using DentalClinicBooking.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinicBooking.Controller
{
    public class BookingController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly SupabaseService _supabase;

        private static readonly int[] SlotHours = { 10, 11, 13, 14, 15, 16 };

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
        public async Task<IActionResult> Index(
            BookingViewModel model, string? selectedDateStr, int? selectedHour)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                if (!string.IsNullOrEmpty(selectedDateStr) &&
                    DateTime.TryParse(selectedDateStr, out var selectedDate) &&
                    selectedHour.HasValue)
                {
                    var bookedHours = await _supabase.GetBookedHoursAsync(selectedDate);
                    if (bookedHours.Contains(selectedHour.Value))
                    {
                        ModelState.AddModelError("",
                            "Sorry, this time slot was just booked by someone else. " +
                            "Please choose another time.");
                        return View(model);
                    }
                }

                // Check if patient already exists
                var existingResult = await _supabase.Client
                    .From<DentalClinicBooking.Models.Patient>()
                    .Where(p => p.Phone == model.Phone)
                    .Get();

                var existingPatient = existingResult.Models.FirstOrDefault();

                // Insert booking only — patient created on approval if new
                var booking = new Booking
                {
                    FullName = model.FullName,
                    Phone = model.Phone,
                    Email = model.Email ?? "",
                    AppointmentDate = model.AppointmentDate,
                    Notes = model.Notes,
                    Status = "pending",
                    IsExistingPatient = existingPatient != null,
                    ExistingPatientId = existingPatient?.Id ?? ""
                };

                await _supabase.Client.From<Booking>().Insert(booking);

                TempData["PatientName"] = model.FullName;
                TempData["AppointmentDate"] = model.AppointmentDate.ToLocalTime().ToString("MMMM dd, yyyy h:mm tt");
                TempData["IsExisting"] = existingPatient != null;

                return RedirectToAction("Confirmation");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("",
                    "Booking failed. Please try again. " + ex.Message);
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

            try
            {
                // Reads appointment_entries — a slot is only unavailable
                // once staff have actually approved a booking for it (or
                // created a walk-in). See SupabaseService.GetBookedHoursAsync.
                var bookedHours = await _supabase.GetBookedHoursAsync(selectedDate);

                System.Diagnostics.Debug.WriteLine(
                    $"[Availability] Date={selectedDate:yyyy-MM-dd} " +
                    $"BookedHours=[{string.Join(",", bookedHours)}]");

                var allSlots = SlotHours
                    .Select(h => new
                    {
                        time = $"{h:00}:00",
                        display = h > 12
                            ? $"{h - 12}:00 PM"
                            : h == 12 ? "12:00 PM" : $"{h}:00 AM",
                        full = bookedHours.Contains(h)
                    })
                    .ToList();

                var allFull = allSlots.All(s => s.full);

                return Json(new { allFull, slots = allSlots });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Availability] Error: {ex.Message}");
                return Json(new
                {
                    allFull = false,
                    slots = Array.Empty<object>()
                });
            }
        }

        // Patient Name autocomplete
        [HttpGet]
        public async Task<IActionResult> SearchPatients(string query)
        {
            var names = await _supabase.SearchPatientNamesAsync(query);
            return Json(names);
        }
    }
}
