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

        public async Task<int> GetBookingCountForDateAsync(DateTime date)
        {
            try
            {
                var startOfDay = date.Date.ToUniversalTime();
                var endOfDay = startOfDay.AddDays(1);

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
                var startOfDay = date.Date.ToUniversalTime();
                var endOfDay = startOfDay.AddDays(1);

                var result = await _client
                    .From<Booking>()
                    .Get();

                return result.Models
                    .Where(b =>
                        b.AppointmentDate >= startOfDay &&
                        b.AppointmentDate < endOfDay &&
                        b.Status != "rejected" &&
                        b.Status != "cancelled")
                    .Select(b => b.AppointmentDate.ToLocalTime())
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