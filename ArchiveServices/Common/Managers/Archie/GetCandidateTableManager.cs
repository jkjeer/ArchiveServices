using System;
//using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
//using System.Text;
//using System.Threading;
using ArchiveServices.Common.Managers.Enterprise;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Types.Response;
using ArchiveServices.Common.Utilities;
using NLog;

namespace ArchiveServices.Common.Managers.Archie
{
    public class GetCandidateTableManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static Types.Archie.ProcessResponse Process(int batchID, string batchName, PlantServerConfig plantServerConfig, SqlConnection cnCorpInt, SqlConnection cnArchConfig, SqlConnection cnDestination)
        {
            var startTime = DateTime.Now.ToLocalTime();

            var processName = $@"Get Candidate Tables: {plantServerConfig.PlantCode}";
            plantServerConfig.LastRunStartDate = startTime;
            var processID = ArchieLoggingManager.LogProcessInsert(cnArchConfig, processName, batchID, plantServerConfig);
            plantServerConfig.ProcessID = processID;

            Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");

            var response = new ProcessResponse($@"BatchID [{batchID}] {batchName} ProcessID [{processID}] {processName}", plantServerConfig, true);

            response.BatchID = batchID;
            response.ProcessID = processID;
            response.StartDate = plantServerConfig.LastRunStartDate;

            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnCorpInt", cnCorpInt.ConnectionString, batchID, processID));
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnArchConfig", cnArchConfig.ConnectionString, batchID, processID));
            var guid = Guid.NewGuid();

            try
            {
                var stepValidateTable = new ProcessStep(cnArchConfig, processID, $@"Validate configuration in [Archiver].[dbo].[PlantServer] for {plantServerConfig.PlantCode}");
                plantServerConfig.ValidationErrors = PlantManager.ValidateConfig(stepValidateTable.StepID, plantServerConfig, cnArchConfig, cnDestination);
                stepValidateTable.Done(cnArchConfig);
                response.ProcessSteps.Add(stepValidateTable);

                if (plantServerConfig.ValidationErrors.Any())
                {
                    //var stepUpdateplantServerConfigDisable = new ProcessStep(cnArchConfig, processID, $@"Set [Archiver].[dbo].[cfgTablesTail].[IsEnabled] = FALSE for {plantServerConfig.ServerQualifiedName} in [Archiver].[dbo].[cfgTablesTail]");
                    //try
                    //{
                    //    stepUpdateplantServerConfigDisable.RowsAffected = TailConfigTableManager.UpdateplantServerConfigIsEnabled(cnArchConfig, plantServerConfig, false);
                    //}
                    //catch (Exception e)
                    //{
                    //    stepUpdateplantServerConfigDisable.Errors.Add(new Error(e));
                    //}
                    //stepUpdateplantServerConfigDisable.Done(cnArchConfig);
                    //response.ProcessSteps.Add(stepUpdateplantServerConfigDisable);

                    foreach (var validationError in plantServerConfig.ValidationErrors)
                    {
                        response.Errors.Add(new Error(validationError.Message));
                    }
                    //ArchieLoggingManager.LogActivity(cnArchConfig, new ArchieLog("Error", "TailTable", "",
                    //    "", plantServerConfig.DateColumn, plantServerConfig.RetentionDays, plantServerConfig.InitialRowCount, stepValidateTable.RowsAffected, plantServerConfig.InitialRowCount - stepValidateTable.RowsAffected,
                    //    Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]),
                    //    "", "", plantServerConfig.ServerName, plantServerConfig.DatabaseName, plantServerConfig.SchemaName,
                    //    plantServerConfig.TableName, guid));
                }
                else
                {
                    var stepGetTargetSqlConnection = new ProcessStep(cnArchConfig, processID, $@"Get SQL connection for target plant {plantServerConfig.PlantCode}");
                    try
                    {
                        // Get Target Table connection
                        cnDestination = GetTargetSqlConnection(plantServerConfig.ServerName, "Master");
                        response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnDestination", cnDestination.ConnectionString, batchID, processID));
                        // See if target table exists
                        //if (!TableManager.CheckTargetTableExists(cnDestination, plantServerConfig))
                        //{
                        //    var validationError = new ValidationError($@"ERROR: {cnDestination.ConnectionString} Target table [{plantServerConfig.PlantCode}] not found.", "Validation", "Fatal", true);
                        //    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetTargetSqlConnection.StepID, validationError.Message, "");
                        //    plantServerConfig.ValidationErrors.Add(validationError);
                        //    plantServerConfig.ValidationStatus = validationError.Message;
                        //    Logger.Error(validationError.Message);
                        //}
                    }
                    catch (Exception e)
                    {
                        stepGetTargetSqlConnection.Errors.Add(new Error(e));
                    }
                    stepGetTargetSqlConnection.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepGetTargetSqlConnection);
                    if (!stepGetTargetSqlConnection.Errors.Any())
                    {


                        //Logger.Info("SLEEP 2 SECONDS...");
                        //Thread.Sleep(2000);

                        //if (plantServerConfig.MovedRows < minMovedRows)
                        //{
                        //    var stepUpdateConfigNextRunDate = new ProcessStep(cnArchConfig, processID, $@"Move Next Run Date ahead 7 days");
                        //    var cmdUpdateConfigNextRunDate = new SqlCommand
                        //    {
                        //        Connection = cnArchConfig,
                        //        CommandType = CommandType.Text,
                        //        CommandText = $@"
                        //            UPDATE xx
                        //            SET 
                        //                [NextRunDate] = GETDATE() + {nextRuntimeMoveAheadDays}
                        //            FROM [dbo].[cfgTablesTail] xx
                        //            WHERE [ConfigID] = {plantServerConfig.ID}
                        //        "
                        //    };
                        //    Logger.Info(Logging.DbCmdString(cmdUpdateConfigNextRunDate));
                        //    stepUpdateConfigNextRunDate.Statement = cmdUpdateConfigNextRunDate.CommandText;
                        //    stepUpdateConfigNextRunDate.RowsAffected = cmdUpdateConfigNextRunDate.ExecuteNonQuery();
                        //    stepUpdateConfigNextRunDate.Done(cnArchRepo);
                        //    response.ProcessSteps.Add(stepUpdateConfigNextRunDate);
                        //    Logger.Info($@"ROWS: {stepUpdateConfigNextRunDate.RowsAffected}");
                        //}


                        //if (plantServerConfig.DeletedRows < minMovedRows)
                        //{
                        //    var stepUpdateConfigNextRunDate = new ProcessStep(cnArchConfig, processID, $@"Move Next Run Date ahead 7 days");
                        //    var cmdUpdateConfigNextRunDate = new SqlCommand
                        //    {
                        //        Connection = cnArchConfig,
                        //        CommandType = CommandType.Text,
                        //        CommandText = $@"
                        //                        UPDATE xx
                        //                        SET 
                        //                            [NextRunDate] = GETDATE() + {nextRuntimeMoveAheadDays}
                        //                        FROM [dbo].[cfgTablesTail] xx
                        //                        WHERE [ConfigID] = {plantServerConfig.ID}
                        //                    "
                        //    };
                        //    Logger.Info(Logging.DbCmdString(cmdUpdateConfigNextRunDate));
                        //    stepUpdateConfigNextRunDate.Statement = cmdUpdateConfigNextRunDate.CommandText;
                        //    stepUpdateConfigNextRunDate.RowsAffected = cmdUpdateConfigNextRunDate.ExecuteNonQuery();
                        //    stepUpdateConfigNextRunDate.Done(cnArchConfig);
                        //    response.ProcessSteps.Add(stepUpdateConfigNextRunDate);
                        //    Logger.Info($@"ROWS: {stepUpdateConfigNextRunDate.RowsAffected}");
                        //}

                        //var stepGatherFinalStatistics = new ProcessStep(cnArchConfig, processID, $@"Gather final statistics for {plantServerConfig.ServerQualifiedName}");
                        //try
                        //{
                        //    plantServerConfig.SFMinDate = TableManager.GetMinDate(cnDestination, plantServerConfig);
                        //    response.SFMinDate = plantServerConfig.SFMinDate;
                        //    plantServerConfig.SFMaxDate = TableManager.GetMaxDate(cnDestination, plantServerConfig);
                        //    plantServerConfig.SFDays = TableManager.GetSFDays(cnDestination, plantServerConfig);
                        //    response.SFDays = plantServerConfig.SFDays;
                        //}
                        //catch (Exception e)
                        //{
                        //    stepGatherFinalStatistics.Errors.Add(new Error(e));
                        //}
                        //stepGatherFinalStatistics.Done(cnArchConfig);
                        //response.ProcessSteps.Add(stepGatherFinalStatistics);

                        //response.RowsAffected = plantServerConfig.DeletedRows;
                        //plantServerConfig.LastRunStatus = "Success";
                        //plantServerConfig.LastTailEndDate = DateTime.Now;
                        //plantServerConfig.FinalRowCountActual = TableManager.GetRowCount(cnDestination, plantServerConfig);

                        //ArchieLoggingManager.LogActivity(cnArchConfig, new ArchieLog("Event", "ArchiveTable",
                        //    processName, plantServerConfig.SelectCommand.Trim(), plantServerConfig.DateColumn, plantServerConfig.RetentionDays,
                        //    plantServerConfig.InitialRowCount, plantServerConfig.DeletedRows, plantServerConfig.FinalRowCountActual,
                        //    Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]),
                        //    "", "", plantServerConfig.ServerName, plantServerConfig.DatabaseName, plantServerConfig.SchemaName,
                        //    plantServerConfig.TableName, plantServerConfig.Guid));

                        //response.InitialRowCount = plantServerConfig.InitialRowCount;
                        //response.FinalRowCount = plantServerConfig.FinalRowCountActual;
                        //response.RetentionDays = plantServerConfig.RetentionDays;
                        response.SelectCommand = plantServerConfig.SelectCommand.Trim();
                        //response.DeleteCommand = plantServerConfig.DeleteCommand.Trim();

                        //ArchieLoggingManager.LogProcessUpdateDone(cnArchConfig, processID, plantServerConfig);

                        //var stepUpdateplantServerConfigDone = new ProcessStep(cnArchConfig, processID, $@"Set [Archiver].[dbo].[cfgTablesTail] DONE for {plantServerConfig.ServerQualifiedName}");
                        //try
                        //{
                        //    var statement = "";
                        //    stepUpdateplantServerConfigDone.RowsAffected = TailConfigTableManager.UpdateplantServerConfigDone(cnArchConfig, plantServerConfig, out statement);
                        //    stepUpdateplantServerConfigDone.Statement = statement;
                        //}
                        //catch (Exception e)
                        //{
                        //    stepUpdateplantServerConfigDone.Errors.Add(new Error(e));
                        //}
                        //stepUpdateplantServerConfigDone.Done(cnArchConfig);

                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                ArchieLoggingManager.LogProcessUpdateDone(cnArchConfig, processID, plantServerConfig);

                var stepUpdateplantServerConfigDone = new ProcessStep(cnArchConfig, processID, $@"Set [Archiver].[dbo].[cfgTablesTail] DONE for {plantServerConfig.PlantCode}");
                try
                {
                    var statement = "";
                    //stepUpdateplantServerConfigDone.RowsAffected = TailConfigTableManager.UpdateplantServerConfigDone(cnArchConfig, plantServerConfig, out statement);
                    stepUpdateplantServerConfigDone.Statement = statement;
                }
                catch (Exception e)
                {
                    stepUpdateplantServerConfigDone.Errors.Add(new Error(e));
                }
                stepUpdateplantServerConfigDone.Done(cnArchConfig);

                cnDestination.Close();
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return response;
        }

        //private static SqlCommand GetplantServerConfig(List<string> filters)
        //{
        //    var filterString = new StringBuilder();
        //    foreach (var filter in filters) filterString.Append(filter);
        //    var cmd = new SqlCommand();
        //    try
        //    {
        //        cmd.CommandType = CommandType.Text;
        //        cmd.CommandText = $@"
        //            SELECT [ID]
        //                  ,[GroupName]
        //                  ,[PlantCode]
        //                  ,[ServerName]
        //                  ,[DatabaseName]
        //                  ,[SchemaName]
        //                  ,[TableName]
        //                  ,[IsActive]
        //                  ,[IsEnabled]
        //                  ,[IsRunning]
        //                  ,[MaxDeleteRowsPerBatch]
        //                  ,[KeyColumn]
        //                  ,[IsKeyColumnUnique]
        //                  ,[DateColumn]
        //                  ,[RetentionDays]
        //                  ,[OtherCondition]
        //                  ,[NextRunDate]
        //                  ,[ProcessID]
        //                  ,[InitialRowCount]
        //                  ,[SFRows]
        //                  ,[SFDataSize]
        //                  ,[SFIndexSize]
        //                  ,[SFDays]
        //                  ,[CreatedDate]
        //                  ,[CreatedBy]
        //                  ,[LastEditBy]
        //                  ,[BITimeStamp]
        //            FROM [dbo].[cfgTablesTail]
        //            WHERE
        //                {filterString}
        //            ORDER BY [GroupName]
        //                ,[PlantCode]
        //                ,[ServerName]
        //                ,[DatabaseName]
        //                ,[SchemaName]
        //                ,[TableName]
        //            ";
        //    }
        //    catch (Exception e)
        //    {
        //        throw new Exception(e.ToString());
        //    }
        //    return cmd;
        //}

        private static SqlConnection GetTargetSqlConnection(string serverName, string databaseName)
        {
            SqlConnection cnDestination = new SqlConnection();
            try
            {
                cnDestination = ConnectionManager.GetSqlConnectionFromParts(serverName, databaseName);
                cnDestination.Open();
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($@"[{serverName}].[{databaseName}]  {e.Message}: {e.StackTrace}");
            }
            return cnDestination;
        }
    }
}
