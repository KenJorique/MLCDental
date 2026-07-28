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
            if (!ModelState.IsValid)
                return View(model);

            try
            {
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
                    AppointmentDate =  model.AppointmentDate,
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
                // Get ALL non-cancelled/rejected bookings for this date
                var allBookings = await _supabase.Client
                    .From<DentalClinicBooking.Models.Booking>()
                    .Get();

                // Date from picker is local Philippine time (no timezone)
                // Bookings are stored as UTC — convert both to same basis
                var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
                    "Asia/Manila") ??
                    TimeZoneInfo.CreateCustomTimeZone(
                        "PH", TimeSpan.FromHours(8), "PH", "PH");

                var bookedHours = allBookings.Models
                    .Where(b =>
                        b.Status != "rejected" &&
                        b.Status != "cancelled" &&
                        b.AppointmentDate != default)
                    .Select(b =>
                    {
                        // Convert stored UTC to Philippine time
                        var utc = DateTime.SpecifyKind(
                                        b.AppointmentDate, DateTimeKind.Utc);
                        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, phTimeZone);
                        return local;
                    })
                    .Where(local => local.Date == selectedDate.Date)
                    .Select(local => local.Hour)
                    .ToList();

                System.Diagnostics.Debug.WriteLine(
                    $"[Availability] Date={selectedDate:yyyy-MM-dd} " +
                    $"BookedHours=[{string.Join(",", bookedHours)}]");

                var allSlots = new[] { 10, 11, 12, 13, 14, 15 }
                    .Select(h => new
                    {
                        time = $"{h:00}:00",
                        display = h > 12
                            ? $"{h - 12}:00 PM"
                            : h == 12 ? "12:00 PM" : $"{h}:00 AM",
                        count = bookedHours.Count(bh => bh == h),
                        full = bookedHours.Any(bh => bh == h) // 1 per slot
                    });

                var dayCount = bookedHours.Distinct().Count();
                var dayFull = dayCount >= 6;

                return Json(new { dayCount, dayFull, slots = allSlots });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Availability] Error: {ex.Message}");
                return Json(new
                {
                    dayCount = 0,
                    dayFull = false,
                    slots = Array.Empty<object>()
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> LookupPatient(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone) || phone.Length < 11)
                return Json(new { found = false });

            try
            {
                // Check bookings table first for returning patients
                var result = await _supabase.Client
                    .From<DentalClinicBooking.Models.Patient>()
                    .Where(p => p.Phone == phone)
                    .Get();

                var patient = result.Models.FirstOrDefault();

                if (patient != null)
                {
                    return Json(new
                    {
                        found = true,
                        fullName = patient.FullName,
                        email = patient.Email ?? "",
                        phone = patient.Phone ?? "",
                        isExisting = true
                    });
                }

                return Json(new { found = false });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[LookupPatient] {ex.Message}");
                return Json(new { found = false });
            }
        }
    }
}