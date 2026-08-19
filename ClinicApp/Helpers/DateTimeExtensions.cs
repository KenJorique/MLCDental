using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClinicApp.Helpers
{
    public static class DateTimeExtensions
    {
        public static DateTime ToLocalSafe(this DateTime dt)
        {
            return dt.Kind switch
            {
                DateTimeKind.Utc => dt.ToLocalTime(),
                DateTimeKind.Local => dt,
                DateTimeKind.Unspecified => dt // don't convert again
            };
        }
    }
}
