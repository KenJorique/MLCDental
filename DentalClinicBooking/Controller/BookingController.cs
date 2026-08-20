using DentalClinicBooking.Models;
using DentalClinicBooking.Services;
using DentalClinicBooking.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinicBooking.Controller
{
    public class BookingController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly SupabaseService _supabase;

        // The only 6 bookable slots — matches the mobile app's own
        // WalkInBookingViewModel.InitializeEmptySlots hour list exactly,
        // so both booking forms offer identical slots.
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
                // Defensive re-check right before inserting: the slot the
                // patient picked may have been approved by staff in the
                // time between page load and Submit. selectedDateStr /
                // selectedHour are PH-local, set by the same slot-click
                // handler that builds the hidden AppointmentDate field —
                // reusing them here sidesteps any UTC/local ambiguity in
                // model.AppointmentDate and just reuses GetBookedHoursAsync,
                // the exact same check GetAvailability already uses.
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
                // created a walk-in), not just because someone submitted
                // a pending request. See SupabaseService.GetBookedHoursAsync.
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

                // Not an artificial daily cap — just the real fact that
                // every one of today's actual slots is already taken.
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

        // Patient Name autocomplete — the ONLY patient suggestion feature
        // on this form now. Returns names only; selecting one must not
        // autofill phone/email/notes, so nothing else is even sent back.
        [HttpGet]
        public async Task<IActionResult> SearchPatients(string query)
        {
            var names = await _supabase.SearchPatientNamesAsync(query);
            return Json(names);
        }

        // ── TEMPORARY DIAGNOSTIC — safe to delete once slot availability
        // is confirmed correct. Visit e.g.
        //     /Booking/DebugAvailability?date=2026-08-21
        // directly in a browser to see exactly what appointment_entries
        // returned and how each row was interpreted.
        [HttpGet]
        public async Task<IActionResult> DebugAvailability(string date)
        {
            if (!DateTime.TryParse(date, out var selectedDate))
                return BadRequest("Invalid date — use format yyyy-MM-dd");

            var info = await _supabase.DebugAppointmentEntriesAsync(selectedDate);
            return Json(info);
        }
    }
}


//using DentalClinicBooking.Models;
//using DentalClinicBooking.Services;
//using DentalClinicBooking.ViewModels;
//using Microsoft.AspNetCore.Mvc;

//namespace DentalClinicBooking.Controller
//{
//    public class BookingController : Microsoft.AspNetCore.Mvc.Controller
//    {
//        private readonly SupabaseService _supabase;

//        private static readonly int[] SlotHours = { 10, 11, 13, 14, 15, 16 };

//        public BookingController(SupabaseService supabase)
//        {
//            _supabase = supabase;
//        }

//        [HttpGet]
//        public IActionResult Index()
//        {
//            return View(new BookingViewModel());
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Index(
//            BookingViewModel model, string? selectedDateStr, int? selectedHour)
//        {
//            if (!ModelState.IsValid)
//                return View(model);

//            try
//            {
//                if (!string.IsNullOrEmpty(selectedDateStr) &&
//                    DateTime.TryParse(selectedDateStr, out var selectedDate) &&
//                    selectedHour.HasValue)
//                {
//                    var bookedHours = await _supabase.GetBookedHoursAsync(selectedDate);
//                    if (bookedHours.Contains(selectedHour.Value))
//                    {
//                        ModelState.AddModelError("",
//                            "Sorry, this time slot was just booked by someone else. " +
//                            "Please choose another time.");
//                        return View(model);
//                    }
//                }

//                // Check if patient already exists
//                var existingResult = await _supabase.Client
//                    .From<DentalClinicBooking.Models.Patient>()
//                    .Where(p => p.Phone == model.Phone)
//                    .Get();

//                var existingPatient = existingResult.Models.FirstOrDefault();

//                // Insert booking only — patient created on approval if new
//                var booking = new Booking
//                {
//                    FullName = model.FullName,
//                    Phone = model.Phone,
//                    Email = model.Email ?? "",
//                    AppointmentDate = model.AppointmentDate,
//                    Notes = model.Notes,
//                    Status = "pending",
//                    IsExistingPatient = existingPatient != null,
//                    ExistingPatientId = existingPatient?.Id ?? ""
//                };

//                await _supabase.Client.From<Booking>().Insert(booking);

//                TempData["PatientName"] = model.FullName;
//                TempData["AppointmentDate"] = model.AppointmentDate.ToLocalTime().ToString("MMMM dd, yyyy h:mm tt");
//                TempData["IsExisting"] = existingPatient != null;

//                return RedirectToAction("Confirmation");
//            }
//            catch (Exception ex)
//            {
//                ModelState.AddModelError("",
//                    "Booking failed. Please try again. " + ex.Message);
//                return View(model);
//            }
//        }

//        public IActionResult Confirmation()
//        {
//            return View();
//        }

//        [HttpGet]
//        public async Task<IActionResult> GetAvailability(string date)
//        {
//            if (!DateTime.TryParse(date, out var selectedDate))
//                return BadRequest("Invalid date");

//            try
//            {
//                // Reads appointment_entries — a slot is only unavailable
//                // once staff have actually approved a booking for it (or
//                // created a walk-in). See SupabaseService.GetBookedHoursAsync.
//                var bookedHours = await _supabase.GetBookedHoursAsync(selectedDate);

//                System.Diagnostics.Debug.WriteLine(
//                    $"[Availability] Date={selectedDate:yyyy-MM-dd} " +
//                    $"BookedHours=[{string.Join(",", bookedHours)}]");

//                var allSlots = SlotHours
//                    .Select(h => new
//                    {
//                        time = $"{h:00}:00",
//                        display = h > 12
//                            ? $"{h - 12}:00 PM"
//                            : h == 12 ? "12:00 PM" : $"{h}:00 AM",
//                        full = bookedHours.Contains(h)
//                    })
//                    .ToList();

//                var allFull = allSlots.All(s => s.full);

//                return Json(new { allFull, slots = allSlots });
//            }
//            catch (Exception ex)
//            {
//                System.Diagnostics.Debug.WriteLine(
//                    $"[Availability] Error: {ex.Message}");
//                return Json(new
//                {
//                    allFull = false,
//                    slots = Array.Empty<object>()
//                });
//            }
//        }

//        // Patient Name autocomplete
//        [HttpGet]
//        public async Task<IActionResult> SearchPatients(string query)
//        {
//            var names = await _supabase.SearchPatientNamesAsync(query);
//            return Json(names);
//        }
//    }
//}
