using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClinicApp.Helpers
{
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Safely converts a DateTime to local time regardless of what
        /// Kind the Postgrest/Supabase deserializer assigned it.
        /// - Utc       → convert normally
        /// - Local     → already correct, leave as-is
        /// - Unspecified → treat as UTC (Postgrest's usual default), then convert
        /// </summary>
        public static DateTime ToLocalSafe(this DateTime dt) => dt.Kind switch
        {
            DateTimeKind.Utc => dt.ToLocalTime(),
            DateTimeKind.Local => dt,
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime()
        };
    }
}
