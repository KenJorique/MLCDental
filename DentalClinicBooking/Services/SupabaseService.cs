using DentalClinicBooking.Models;

namespace DentalClinicBooking.Services
{
    public class SupabaseService
    {
        private readonly Supabase.Client _client;

        public SupabaseService(IConfiguration config)
        {
            var url = config["Supabase:Url"];
            var key = config["Supabase:Key"];

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException(
                    "Supabase URL or Key is missing from appsettings.json. " +
                    "Check that Supabase:Url and Supabase:Key are set.");

            _client = new Supabase.Client(url, key);
            _client.InitializeAsync().Wait();
        }
        public Supabase.Client Client => _client;

        // Real source of truth for "is this slot actually taken" —
        // reads appointment_entries (the table the mobile app writes to
        // ONLY when staff approve a booking or create a walk-in), not
        // the bookings table. A booking sitting there as merely
        // "pending" hasn't been approved yet and must NOT block the slot
        // for other patients.
        //
        // IMPORTANT: appointment_datetime is stored as PH wall-clock time
        // DIRECTLY, not true UTC — confirmed via DebugAppointmentEntriesAsync
        // (a 10:00 AM booking is stored literally as "...T10:00:00", not
        // shifted to "...T02:00:00"). Likely because the mobile app's
        // .ToUniversalTime() call is a no-op whenever the device's own OS
        // timezone isn't set to Asia/Manila. So: read the value as-is, no
        // UTC->PH conversion — that conversion was the actual bug (it
        // double-shifted every entry by 8 hours, e.g. today's real 10 AM
        // and 11 AM bookings were being read as 18:00/19:00 and therefore
        // never matched any real slot).
        public async Task<List<int>> GetBookedHoursAsync(DateTime date)
        {
            try
            {
                var result = await _client.From<AppointmentEntry>().Get();

                return result.Models
                    .Where(e =>
                        e.Status != "cancelled" &&
                        e.Status != "completed" &&
                        e.Status != "rejected" &&
                        e.AppointmentDateTime != default &&
                        e.AppointmentDateTime.Date == date.Date)
                    .Select(e => e.AppointmentDateTime.Hour)
                    .Distinct()
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetBookedHours: {ex.Message}");
                return new List<int>();
            }
        }

        // ── TEMPORARY DIAGNOSTIC ──────────────────────────────────────
        // Safe to delete once slot availability is confirmed working.
        // Surfaces exactly what appointment_entries returned and how
        // each row got interpreted (raw value, timezone conversion,
        // which date/hour it landed on, whether its status excluded it)
        // — readable directly in a browser, no server log access needed.
        public async Task<object> DebugAppointmentEntriesAsync(DateTime date)
        {
            try
            {
                var result = await _client.From<AppointmentEntry>().Get();

                var rows = result.Models.Select(e =>
                {
                    var excludedByStatus =
                        e.Status == "cancelled" ||
                        e.Status == "completed" ||
                        e.Status == "rejected";

                    return new
                    {
                        id = e.Id,
                        status = e.Status,
                        rawValueFromSupabase = e.AppointmentDateTime.ToString("o"),
                        // No conversion applied — appointment_datetime is
                        // stored as PH wall-clock time directly. See the
                        // comment on GetBookedHoursAsync for why.
                        interpretedHour = e.AppointmentDateTime.Hour,
                        matchesRequestedDate = e.AppointmentDateTime.Date == date.Date,
                        excludedByStatus
                    };
                }).ToList();

                return new
                {
                    requestedDate = date.ToString("yyyy-MM-dd"),
                    totalEntriesFound = result.Models.Count,
                    entries = rows,
                    finalBookedHours = rows
                        .Where(r => r.matchesRequestedDate && !r.excludedByStatus)
                        .Select(r => r.interpretedHour)
                        .Distinct()
                        .OrderBy(h => h)
                        .ToList()
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    error = true,
                    message = ex.Message,
                    fullException = ex.ToString()
                };
            }
        }

        // Patient Name autocomplete (booking form). Returns matching full
        // names ONLY — no phone/email/other fields — since selecting a
        // suggestion must not autofill anything else. Same "fetch then
        // filter client-side" approach the mobile app's WalkInBooking
        // name search already uses, just server-side here since this is
        // a public, unauthenticated form.
        public async Task<List<string>> SearchPatientNamesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return new List<string>();

            try
            {
                var result = await _client.From<Patient>().Get();
                var q = query.Trim();

                return result.Models
                    .Select(p => p.FullName)
                    .Where(name =>
                        !string.IsNullOrWhiteSpace(name) &&
                        name.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .Take(8)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] SearchPatientNames: {ex.Message}");
                return new List<string>();
            }
        }
    }
}
