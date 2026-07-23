using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;
using ArchiveServices.Common.Types.Response;
using ArchiveServices.Common.Utilities;
using NLog;

// ReSharper disable InconsistentNaming

namespace ArchiveServices.Common.Managers.Enterprise
{
    public class ProcessLogManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static ProcessLogErrors GetProcesslogErrors(SqlConnection cnCorpInt, int lookBackMinutes)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info("BEGIN: " + DateTime.Now.ToLocalTime());
            var processLogErrors = new ProcessLogErrors();
            var filters = new List<string>();
            var parameters = new List<SqlParameter>();
            try
            {
                var lookBackDateTime = DateTime.Now.AddMinutes(-lookBackMinutes);
                filters.Add("[EVENT_DATE] >= @lookBackDateTime");
                parameters.Add(new SqlParameter("@lookBackDateTime", SqlDbType.DateTime));
                parameters.Last().Value = lookBackDateTime;
                var cmd = GetProcessLogErrors(filters);
                foreach (var parameter in parameters)
                {
                    cmd.Parameters.Add(parameter);
                }
                cmd.Connection = cnCorpInt;
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var processLogError = new ProcessLogError
                        {
                            EventDate = CastDbNull.To(reader["EVENT_DATE"], default(DateTime)),
                            PlantCode = CastDbNull.To(reader["PLANT_CD"], ""),
                            Process = CastDbNull.To(reader["PROCESS"], ""),
                            Status = CastDbNull.To(reader["STATUS"], ""),
                            StartDate = CastDbNull.To(reader["START_DATE"], default(DateTime)),
                            EndDate = CastDbNull.To(reader["END_DATE"], default(DateTime)),
                            Message = CastDbNull.To(reader["Message"], ""),
                            Source = CastDbNull.To(reader["Source"], ""),
                            StackTrace = CastDbNull.To(reader["StackTrace"], "")
                        };
                        //spacerConsumption.TransferDateString = spacerConsumption.TransferDate.ToString("M/dd/yyyy hh:mm:ss tt");
                        processLogErrors.Add(processLogError);
                    }

                    reader.Close();
                }

                Logger.Trace($"ROWS: {processLogErrors.Count}");
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnCorpInt.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnCorpInt.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return processLogErrors;
        }

        public static SqlCommand GetProcessLogErrors(List<string> filters)
        {
            var filterString = new StringBuilder();
            foreach (var filter in filters)
            {
                filterString.Append(filter);
            }
            var cmd = new SqlCommand();
            try
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    SELECT [EVENT_DATE]
                       , [PLANT_CD]
                       , [PROCESS]
                       , [STATUS]
                       , [START_DATE]
                       , [END_DATE]
                       , STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') AS Message
                       , STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Source)[1]', 'varchar(max)') AS Source
                       , STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/StackTrace)[1]', 'varchar(max)') AS StackTrace
                    FROM  SVC.PROCESS_LOG WITH ( NOLOCK )
                    WHERE STATUS = 'ERROR'
                         AND {filterString}
		                 AND STEPS.exist('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]') = 1
		                 AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%PING Failure%'
		                 AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%An exception occurred during a Ping request%'
		                 AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%NOT Pingable%'
		                 AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%Timeout expired%'
		                 AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%network-related%'
		                 AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%The wait operation timed out%'
		                 AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%Login failed for user%'
		                 AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%A transport-level error has occurred%'
		                 AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%Cannot open database%'
		                 AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%previously uploaded%'
		                 AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%has % units than BoxHold%'
		                 AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%not found in UnitBox%'
		                 AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%not found in IncomingOrders%'
                    ";
                    //AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%Cannot access destination table%'
                    //AND STEPS.value('(/ProcessResponse/ProcessSteps/ProcessStep/Errors/Error/Message)[1]', 'varchar(max)') NOT LIKE '%was deadlocked on lock resources%'
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.ToString());
            }
            return cmd;
        }
    }
}
