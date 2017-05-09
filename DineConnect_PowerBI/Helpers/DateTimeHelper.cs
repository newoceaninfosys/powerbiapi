using System;

namespace DineConnect_PowerBI.Helpers
{
    public static class DateTimeHelper
    {
        public static DateTime ToDatetime(double timestamp)
        {
            return new DateTime(1970, 1, 1).AddSeconds(timestamp);
        }

        public static double ToTimestamp(DateTime time)
        {
            return time.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

    }
}