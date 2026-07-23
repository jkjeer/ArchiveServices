using System;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Types.Response;
using NLog;

namespace ArchiveServices.Common.Utilities
{
    public class Logging
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static string DbCmdString(SqlCommand cmd)
        {
            var sb = new StringBuilder();
            try
            {
                sb.Append(Environment.NewLine);
                sb.Append("************SQL SERVER STATEMENT*********");
                sb.Append(Environment.NewLine);
                sb.Append("[Connection String]");
                sb.Append(Environment.NewLine);
                sb.Append(cmd.Connection.ConnectionString);
                sb.Append(Environment.NewLine);
                sb.Append("[Command Text]");
                sb.Append(Environment.NewLine);
                sb.Append(cmd.CommandText);
                sb.Append(Environment.NewLine);
                sb.Append("[Command Parameters]");
                sb.Append(Environment.NewLine);
                sb.Append(string.Join(", ", cmd.Parameters.Cast<SqlParameter>().AsQueryable().Select(parm => parm.Value)));
                sb.Append(Environment.NewLine);
                sb.Append("*****************************************");
                sb.Append(Environment.NewLine);
            }
            catch (ApplicationException e)
            {
                throw new ApplicationException(e.Message);
            }
            return sb.ToString();
        }
        public static void InsertProcessLog(ProcessResponse response)
        {
            Logger ProcessLogger = LogManager.GetLogger("ProcessLog");
            try
            {
                var logEvent = new LogEventInfo();
                if (response.ProcessSteps.Any())
                {
                    logEvent.Level = LogLevel.Info;
                    logEvent.Properties.Add("EVENT_DATE", DateTime.Now);
                    logEvent.Properties.Add("PROCESS", response.ProcessName);
                    logEvent.Properties.Add("STATUS", (response.HasErrors()) ? "ERROR" : "SUCCESS");
                    logEvent.Properties.Add("START_DATE", response.ProcessSteps.Min(x => x.StartDate));
                    logEvent.Properties.Add("END_DATE", response.ProcessSteps.Max(x => x.EndDate));
                    logEvent.Properties.Add("ROWS_AFFECTED", response.RowsAffected);
                    logEvent.Properties.Add("CONNECTION_STRINGS",
                        XmlSerializationUtility.SerializeObject(Encoding.UTF8, response.ConnectionStrings, true));
                    logEvent.Properties.Add("STEPS",
                        XmlSerializationUtility.SerializeObject(Encoding.UTF8, response, true));
                }
                else
                {
                    logEvent.Level = LogLevel.Info;
                    logEvent.Properties.Add("EVENT_DATE", DateTime.Now);
                    logEvent.Properties.Add("PROCESS", response.ProcessName);
                    logEvent.Properties.Add("STATUS", (response.HasErrors()) ? "ERROR" : "SUCCESS");
                    logEvent.Properties.Add("START_DATE", response.StartDate);
                    logEvent.Properties.Add("END_DATE", response.EndDate);
                    logEvent.Properties.Add("ROWS_AFFECTED", response.RowsAffected);
                    logEvent.Properties.Add("CONNECTION_STRINGS",
                        XmlSerializationUtility.SerializeObject(Encoding.UTF8, response.ConnectionStrings, true));
                    logEvent.Properties.Add("STEPS",
                        XmlSerializationUtility.SerializeObject(Encoding.UTF8, response, true));
                }
                ProcessLogger.Log(logEvent);
            }
            catch (Exception e)
            {
                response.Errors.Add(new Error($"{e.Message} {e.StackTrace}"));
                Logger.Error($@"ERROR: {e.Message} {e.StackTrace}");
                throw new Exception($"{e.Message} {e.StackTrace}");
            }
        }

    }
}
