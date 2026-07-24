using DentalClinicBooking.Models;

namespace DentalClinicBooking.Services
{
    public class SupabaseService
    {
        private readonly Supabase.Client _client;

        // Philippines is UTC+8 year-round (no DST). "Asia/Manila" is the
        // IANA id (Linux/macOS); "Taipei Standard Time" is the Windows id
        // for the same fixed UTC+8 offset — used as a fallback if Manila
        // isn't registered on the host OS.
        private static readonly TimeZoneInfo PhTimeZone = GetPhTimeZone();

        private static TimeZoneInfo GetPhTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"); }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");
            }
        }

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

        // date is a PH calendar date (e.g. from the <input type=date>).
        // Builds the UTC window that corresponds to PH midnight → next PH midnight,
        // so the DB query is correct regardless of what timezone the server itself is in.
        private static (DateTime startUtc, DateTime endUtc) PhDayWindowUtc(DateTime date)
        {
            var phMidnight = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(phMidnight, PhTimeZone);
            var endUtc = startUtc.AddDays(1);
            return (startUtc, endUtc);
        }

        public async Task<int> GetBookingCountForDateAsync(DateTime date)
        {
            try
            {
                var (startOfDay, endOfDay) = PhDayWindowUtc(date);

                var result = await _client
                    .From<Booking>()
                    .Get();

                return result.Models.Count(b =>
                    b.AppointmentDate >= startOfDay &&
                    b.AppointmentDate < endOfDay &&
                    b.Status != "rejected" &&
                    b.Status != "cancelled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetBookingCountForDate: {ex.Message}");
                return 0;
            }
        }

        public async Task<List<DateTime>> GetBookedTimeSlotsAsync(DateTime date)
        {
            try
            {
                var (startOfDay, endOfDay) = PhDayWindowUtc(date);

                var result = await _client
                    .From<Booking>()
                    .Get();

                return result.Models
                    .Where(b =>
                        b.AppointmentDate >= startOfDay &&
                        b.AppointmentDate < endOfDay &&
                        b.Status != "rejected" &&
                        b.Status != "cancelled")
                    // Explicit PH conversion — not server-dependent .ToLocalTime()
                    .Select(b => TimeZoneInfo.ConvertTimeFromUtc(
                        DateTime.SpecifyKind(b.AppointmentDate, DateTimeKind.Utc),
                        PhTimeZone))
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Supabase] GetBookedTimeSlots: {ex.Message}");
                return new List<DateTime>();
            }
        }
    }
}