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
    }
}