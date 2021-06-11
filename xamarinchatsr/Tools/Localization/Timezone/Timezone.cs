using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tools.Localization.Timezone
{
    /// <summary>
    /// https://docs.microsoft.com/en-us/dotnet/standard/datetime/converting-between-time-zones
    /// </summary>
    public class Timezone
    {
        public static ReadOnlyCollection<TimeZoneInfo> zones = TimeZoneInfo.GetSystemTimeZones();

        public static DateTime GetUTCNow()
        {
            return DateTime.UtcNow;
        }
        public static DateTime GetUTCFromTime(DateTime time)
        {
            return TimeZoneInfo.ConvertTimeToUtc(time);
        }
        public static string GetCurrentTimezoneID()
        {
            return TimeZoneInfo.Local.Id;
        }
        public static DateTime GetCountryTimezone(string zoneID, DateTime timeUtc)
        {
            try
            {
                TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(zoneID);
                return TimeZoneInfo.ConvertTimeFromUtc(timeUtc, zone);
            }
            catch (TimeZoneNotFoundException)
            {
                 Debug.WriteLine("Unable to find the {0} zone in the registry.",
                                  zoneID);
                throw;
            }
            catch (InvalidTimeZoneException)
            {
                 Debug.WriteLine("Registry data on the {0} zone has been corrupted.",
                                  zoneID);
                throw;
            }
        }
    }
}
