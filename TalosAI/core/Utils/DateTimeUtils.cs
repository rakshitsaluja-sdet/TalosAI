using System.Globalization;

namespace TalosAI.core.Utils
{

    class DateTimeUtils
    {
        static DateTime now = DateTime.Now; // Local time
        static DateTime utcNow = DateTime.UtcNow; // UTC time

        static void getCurrentDTTM()
        {
            // 1. CURRENT DATE & TIME

            Console.WriteLine($"Local Now: {now}");
            Console.WriteLine($"UTC Now: {utcNow}");
        }
        static void getDTTM()
        {
            // 2. SPECIFIC DATE & TIME CREATION
            DateTime specificDate = new DateTime(2026, 1, 13, 14, 30, 0); // yyyy, MM, dd, HH, mm, ss
            Console.WriteLine($"Specific Date: {specificDate}");
        }

        static void getYrMnDay()
        {
            // 3. DATE COMPONENTS
            Console.WriteLine($"Year: {now.Year}, Month: {now.Month}, Day: {now.Day}");
            Console.WriteLine($"Hour: {now.Hour}, Minute: {now.Minute}, Second: {now.Second}");
            Console.WriteLine($"Day of Week: {now.DayOfWeek}, Day of Year: {now.DayOfYear}");
        }

        static void addMinus()
        {
            // 4. ADDING & SUBTRACTING TIME
            DateTime tomorrow = now.AddDays(1);
            DateTime lastWeek = now.AddDays(-7);
            DateTime nextMonth = now.AddMonths(1);
            Console.WriteLine($"Tomorrow: {tomorrow}");
            Console.WriteLine($"Last Week: {lastWeek}");
            Console.WriteLine($"Next Month: {nextMonth}");
        }

        static void difDates()
        {
            // 5. DIFFERENCE BETWEEN DATES (TimeSpan)
            DateTime tomorrow = now.AddDays(1);
            TimeSpan diff = tomorrow - now;
            Console.WriteLine($"Difference in hours: {diff.TotalHours}");
        }

        static void formatDate()
        {
            // 6. FORMATTING DATES
            Console.WriteLine(now.ToString("dd/MM/yyyy")); // 13/01/2026
            Console.WriteLine(now.ToString("MMMM dd, yyyy")); // January 13, 2026
            Console.WriteLine(now.ToString("yyyy-MM-dd HH:mm:ss")); // 2026-01-13 14:30:00
        }

        static void pharseStrToDate()
        {
            // 7. PARSING STRINGS TO DATE
            string dateStr = "2026-01-13 14:30";
            if (DateTime.TryParse(dateStr, out DateTime parsedDate))
            {
                Console.WriteLine($"Parsed Date: {parsedDate}");
            }

            // Parsing with specific format
            string customDateStr = "13-01-2026";
            if (DateTime.TryParseExact(customDateStr, "dd-MM-yyyy", CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out DateTime exactDate))
            {
                Console.WriteLine($"Exact Parsed Date: {exactDate}");
            }
        }

        static void timeZoneConv()
        {
            // 8. TIME ZONE CONVERSION
            DateTime utcTime = DateTime.UtcNow;
            TimeZoneInfo indiaZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            DateTime indiaTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, indiaZone);
            Console.WriteLine($"India Time: {indiaTime}");
        }

        static void timeZoneConvWithOffset()
        {
            // 9. USING DateTimeOffset (with time zone offset)
            DateTimeOffset dto = DateTimeOffset.Now;
            Console.WriteLine($"DateTimeOffset: {dto}");
            Console.WriteLine($"UTC Offset: {dto.Offset}");
        }

        static void isLeap()
        {
            // 10. CHECKING LEAP YEAR
            Console.WriteLine($"Is 2024 Leap Year? {DateTime.IsLeapYear(2024)}");
        }
    }
}
