using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.ServiceModel;

namespace ArchiveServices.Common.Utilities
{
    public class Timestamp
    {

        public static DateTime ConvertE1Date(decimal dateId)
        {
            DateTime dateBase = new DateTime(1900, 1, 1);
            int dateDays = 0;
            int dateYears = 0;
            try
            {
                string dateString = Convert.ToString(dateId, CultureInfo.CurrentCulture);
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

        public static decimal CastToE1Date(DateTime inputDate)
        {
            return (inputDate.Year - 1900) * 1000 + inputDate.DayOfYear;
        }

        public static decimal CastToE1Time(DateTime inputDate)
        {
            return (inputDate.Hour * 10000) + (inputDate.Minute * 100) + inputDate.Second;
        }

        public static DateTime CastToDateTime(decimal dateId, decimal timeId = 0)
        {
            DateTime dt = ConvertE1Date(dateId);
            Data.CastDbNull.To<decimal>(timeId, 0);
            try
            {
                var timeString = Convert.ToString(timeId, CultureInfo.CurrentCulture).PadLeft(6).Replace(" ", "0");
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

        public static TimeSpan GetTimeSpan(string days, string hours, string minutes)
        {
            TimeSpan timeSpan;
            try
            {
                int daysInt = (string.IsNullOrEmpty(days)) ? 0 : Convert.ToInt16(days);
                int hoursInt = (string.IsNullOrEmpty(hours)) ? 0 : Convert.ToInt16(hours);
                int minsInt = (string.IsNullOrEmpty(minutes)) ? 0 : Convert.ToInt16(minutes);
                timeSpan = new TimeSpan(daysInt, hoursInt, minsInt, 0);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.StackTrace);
            }
            return timeSpan;
        }

        public static Dictionary<string, decimal> GetE1DateHash(double days)
        {
            var hash = new Dictionary<string, decimal>();
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

        /*********************************************************************************
         * Requested Date
         *******************************************************************************/
        public static SqlCommand GetDateCodeRange()
        {
            var cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = @"
            SELECT
              MIN(CAL_DATE) AS MIN_CAL_DATE,
              MAX(CAL_DATE) AS MAX_CAL_DATE          
            FROM
              DW.CALENDAR_DATE_TYPE CDT
              INNER JOIN DW.CALENDAR_DATE_ATTRIBUTES CDA
              ON CDA.CAL_DATE_TYPE_ID = CDT.DATE_TYPE_ID
            WHERE
              CDT.DATE_TYPE_CD = @DateCode;";
            cmd.Parameters.Add(new SqlParameter("@DateCode", SqlDbType.VarChar, 30));
            return cmd;
        }

        public static Dictionary<string, DateTime> GetDateCodeRange(SqlConnection cn, string code)
        {

            var dt = new Dictionary<string, DateTime>();
            try
            {
                var cmd = GetDateCodeRange();
                cmd.Connection = cn;
                cmd.Parameters[0].Value = code;
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    while (reader.Read())
                    {
                        dt.Add("MIN", CastDbNull.To<DateTime>(reader["MIN_CAL_DATE"], new DateTime(1900, 1, 1)));
                        dt.Add("MAX", CastDbNull.To<DateTime>(reader["MAX_CAL_DATE"], new DateTime(1900, 1, 1)));
                    }

                    reader.Close();
                }
            }
            catch (Exception e)
            {
                throw new FaultException(e.Message);
            }

            return dt;
        }
    }
}
