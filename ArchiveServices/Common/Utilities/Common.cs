using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ArchiveServices.Common.Utilities
{
    public class Common
    {

        public static string PersistSqlString(string inputString)
        {
            var retValue = "";
            if (!String.IsNullOrEmpty(inputString))
            {
                retValue = inputString.Replace("'", "''");
            }
            return retValue;
        }

        public static string RetrieveSqlString(string inputString)
        {
            return inputString.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        }

        public static string OmitBrackets(string inputString)
        {
            return inputString.Replace("[", "").Replace("]", "");
        }

        public static DateTime ConvertE1Date(Decimal DateId)
        {
            DateTime dateBase = new DateTime(1900, 1, 1);
            int dateDays = 0;
            int dateYears = 0;
            try
            {
                String dateString = Convert.ToString(DateId, CultureInfo.InvariantCulture);
                int dateStringLength = dateString.Length;
                if (dateStringLength == 5 | dateStringLength == 6)
                {
                    dateDays = Convert.ToInt16(dateString.Substring(dateStringLength - 3, 3)) - 1;
                    dateYears = Convert.ToInt16(dateString.Substring(0, dateStringLength - 3));
                }
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
            return dateBase.AddYears(dateYears).AddDays(dateDays);
        }

        public static Decimal CastToE1Date(DateTime inputDate)
        {
            return (inputDate.Year - 1900)*1000 + inputDate.DayOfYear;
        }

        public static Decimal CastToE1Time(DateTime inputDate)
        {
            return (inputDate.Hour*10000) + (inputDate.Minute*100) + inputDate.Second;
        }

        public static DateTime CastToDateTime(decimal dateId, decimal timeId = 0)
        {
            DateTime dt = ConvertE1Date(dateId);
            CastDbNull.To<Decimal>(timeId, 0);
            try
            {
                var timeString = Convert.ToString(timeId, CultureInfo.InvariantCulture).PadLeft(6).Replace(" ", "0");
                var hours = Convert.ToInt16(timeString.Substring(0, 2));
                var minutes = Convert.ToInt16(timeString.Substring(2, 2));
                var seconds = Convert.ToInt16(timeString.Substring(4, 2));
                TimeSpan ts = new TimeSpan(hours, minutes, seconds);
                return dt.Add(ts);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

        public static DateTime CastSttmToDateTime(string sttm)
        {
            try
            {
                var years = Convert.ToInt16(sttm.Substring(0, 4));
                var months = Convert.ToInt16(sttm.Substring(5, 2));
                var days = Convert.ToInt16(sttm.Substring(8, 2));
                var hours = Convert.ToInt16(sttm.Substring(11, 2));
                var minutes = Convert.ToInt16(sttm.Substring(14, 2));
                var seconds = Convert.ToInt16(sttm.Substring(17, 2));
                return new DateTime(years, months, days, hours, minutes, seconds);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

        public static Decimal CastStringToDecimal(string inputValue)
        {
            try
            {
                return Convert.ToDecimal(inputValue);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static String CastToFractionalDimension(decimal whole, decimal num, decimal den)
        {
            StringBuilder sb = new StringBuilder();
            if (den > 0)
            {
                sb.Append(Convert.ToString(whole, CultureInfo.InvariantCulture));
                sb.Append(" ");
                sb.Append(Convert.ToString(num, CultureInfo.InvariantCulture));
                sb.Append("/");
                sb.Append(Convert.ToString(den, CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(Convert.ToString(whole, CultureInfo.InvariantCulture));
            }

            if (sb.ToString() == "0")
            {
                return "";
            }
            else
            {
                return sb.ToString();
            }

        }

        public static TimeSpan GetTimeSpan(string days, string hours, string minutes)
        {
            TimeSpan timeSpan;
            try
            {
                int daysInt = (String.IsNullOrEmpty(days)) ? 0 : Convert.ToInt16(days);
                int hoursInt = (String.IsNullOrEmpty(hours)) ? 0 : Convert.ToInt16(hours);
                int minsInt = (String.IsNullOrEmpty(minutes)) ? 0 : Convert.ToInt16(minutes);
                timeSpan = new TimeSpan(daysInt, hoursInt, minsInt, 0);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.StackTrace);
            }
            return timeSpan;
        }

        public static Dictionary<String, Decimal> GetE1DateHash(Double days)
        {
            var hash = new Dictionary<String, Decimal>();
            DateTime adjDateTime = DateTime.Now.AddDays(days * -1);
            if (DateTime.Now.Date == adjDateTime.Date)
            {
                hash.Add("Date1", CastToE1Date(adjDateTime.Date));
                hash.Add("Time1", CastToE1Time(adjDateTime));
                hash.Add("Date2", 999999);
            }
            else
            {
                hash.Add("Date1", CastToE1Date(adjDateTime.Date));
                hash.Add("Time1", CastToE1Time(adjDateTime));
                hash.Add("Date2", CastToE1Date(adjDateTime.Date.AddDays(1)));
            }
            return hash;
        }

    }

}
