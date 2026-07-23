using System;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Types.Response;
using ArchiveServices.Common.Utilities;
using NLog;

namespace ArchiveServices.Common.Managers.Archie
{
    public class GetInitialStatisticsManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static Types.Archie.ProcessResponse Process(int batchID, string batchName, TailTableNoVerifyConfig tailTableConfig, SqlConnection cnCorpInt, SqlConnection cnArchConfig, SqlConnection cnDestination)
        {
            var startTime = DateTime.Now.ToLocalTime();

            var processName = $@"Tail Data Table: {tailTableConfig.ServerQualifiedSourceName}";
            tailTableConfig.LastRunStartDate = startTime;
            tailTableConfig.TestModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            if (tailTableConfig.TestModeNoDelete) processName = $@"{processName} - Test Mode No Delete";
            var minMovedRows = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieMinMovedRows"]);
            var nextRuntimeMoveAheadDays = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieNextRuntimeMoveAheadDays"]);
            var maxDeleteSeconds = Convert.ToDecimal(ConfigurationManager.AppSettings["ArchieMaxDeleteSeconds"]);
            var processID = LogProcessInsert(cnArchConfig, processName, batchID, tailTableConfig);
            tailTableConfig.ProcessID = processID;

            Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");

            var response = new ProcessResponse($@"BatchID [{batchID}] {batchName} ProcessID [{processID}] {processName}", tailTableConfig, true);

            response.BatchID = batchID;
            response.ProcessID = processID;
            response.ArchiveDate = tailTableConfig.LastRunStartDate;

            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnCorpInt", cnCorpInt.ConnectionString, batchID, processID));
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnArchConfig", cnArchConfig.ConnectionString, batchID, processID));
            var guid = Guid.NewGuid();

            try
            {
                var stepValidateTable = new ProcessStep(cnArchConfig, processID, $@"Validate configuration in [Archiver].[dbo].[cfgTablesTail] for {tailTableConfig.ServerQualifiedTargetName}");
                tailTableConfig.ValidationErrors = TableManager.ValidateConfig(stepValidateTable.StepID, tailTableConfig, cnArchConfig, cnDestination);
                stepValidateTable.Done(cnArchConfig);
                response.ProcessSteps.Add(stepValidateTable);

                if (tailTableConfig.ValidationErrors.Any())
                {
                    //var stepUpdatetailTableConfigDisable = new ProcessStep(cnArchConfig, processID, $@"Set [Archiver].[dbo].[cfgTablesTail].[IsEnabled] = FALSE for {tailTableConfig.ServerQualifiedName} in [Archiver].[dbo].[cfgTablesTail]");
                    //try
                    //{
                    //    stepUpdatetailTableConfigDisable.RowsAffected = TailConfigTableManager.UpdatetailTableConfigIsEnabled(cnArchConfig, tailTableConfig, false);
                    //}
                    //catch (Exception e)
                    //{
                    //    stepUpdatetailTableConfigDisable.Errors.Add(new Error(e));
                    //}
                    //stepUpdatetailTableConfigDisable.Done(cnArchConfig);
                    //response.ProcessSteps.Add(stepUpdatetailTableConfigDisable);

                    foreach (var validationError in tailTableConfig.ValidationErrors)
                    {
                        response.Errors.Add(new Error(validationError.Message));
                    }
                    ArchieLoggingManager.LogActivity(cnArchConfig, new ArchieLog("Error", "TailTable", "",
                        "", tailTableConfig.DateColumn, tailTableConfig.RetentionDays, tailTableConfig.InitialRowCount, stepValidateTable.RowsAffected, tailTableConfig.InitialRowCount - stepValidateTable.RowsAffected,
                        Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]),
                        "", "", tailTableConfig.TargetServerName, tailTableConfig.TargetDatabaseName, tailTableConfig.TargetSchemaName,
                        tailTableConfig.TargetTableName, guid));
                }
                else
                {
                    var stepGetTargetSqlConnection = new ProcessStep(cnArchConfig, processID, $@"Get SQL connection for target table {tailTableConfig.ServerQualifiedTargetName}");
                    try
                    {
                        // Get Target Table connection
                        cnDestination = GetTargetSqlConnection(tailTableConfig.TargetServerName, tailTableConfig.TargetDatabaseName);
                        response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnDestination", cnDestination.ConnectionString, batchID, processID));
                        // See if target table exists
                        if (!TableManager.CheckSourceTableExists(cnDestination, tailTableConfig))
                        {
                            var validationError = new ValidationError($@"ERROR: {cnDestination.ConnectionString} Target table [{tailTableConfig.ServerQualifiedTargetName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetTargetSqlConnection.StepID, validationError.Message, "");
                            tailTableConfig.ValidationErrors.Add(validationError);
                            tailTableConfig.ValidationStatus = validationError.Message;
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepGetTargetSqlConnection.Errors.Add(new Error(e));
                    }
                    stepGetTargetSqlConnection.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepGetTargetSqlConnection);
                    if (!stepGetTargetSqlConnection.Errors.Any())
                    {
                        var stepUpdatetailTableConfigIsRunningTrue = new ProcessStep(cnArchConfig, processID, $@"Set [Archiver].[dbo].[cfgTablesTail].[IsRunning] = TRUE for {tailTableConfig.ServerQualifiedTargetName}");
                        try
                        {
                            var statement = "";
                            stepUpdatetailTableConfigIsRunningTrue.RowsAffected = TailTableNoVerifyConfigManager.UpdateTailTableConfigIsRunningTrue(cnArchConfig, tailTableConfig, out statement);
                            stepUpdatetailTableConfigIsRunningTrue.Statement = statement;
                        }
                        catch (Exception e)
                        {
                            stepUpdatetailTableConfigIsRunningTrue.Errors.Add(new Error(e));
                        }
                        stepUpdatetailTableConfigIsRunningTrue.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepUpdatetailTableConfigIsRunningTrue);
                        if (!stepUpdatetailTableConfigIsRunningTrue.Errors.Any())
                        {
                            var stepCheckIsKeyColumnUnique = new ProcessStep(cnArchConfig, processID, $@"Check if key column is unique for {tailTableConfig.ServerQualifiedTargetName}");
                            try
                            {
                                tailTableConfig.IsKeyColumnUnique = TableManager.IsKeyColumnUnique(cnDestination, tailTableConfig);
                                ArchieLoggingManager.LogProcessUpdateIsKeyColumnUnique(cnArchConfig, processID, tailTableConfig);
                                TailTableNoVerifyConfigManager.UpdateTailTableConfigIsKeyColumnUnique(cnArchConfig, tailTableConfig, out string statement);
                                stepCheckIsKeyColumnUnique.Statement = statement;
                            }
                            catch (Exception e)
                            {
                                stepCheckIsKeyColumnUnique.Errors.Add(new Error(e));
                            }
                            stepCheckIsKeyColumnUnique.Done(cnArchConfig);
                            response.ProcessSteps.Add(stepCheckIsKeyColumnUnique);
                            if (!tailTableConfig.IsKeyColumnUnique)
                            {
                                var validationError = new ValidationError($@"ERROR: {cnDestination.ConnectionString} Target table {tailTableConfig.ServerQualifiedTargetName} Key Column [{tailTableConfig.KeyColumn}] is not unique.", "Validation", "Fatal", true);
                                var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetTargetSqlConnection.StepID, validationError.Message, "");
                                tailTableConfig.ValidationErrors.Add(validationError);
                                tailTableConfig.ValidationStatus = validationError.Message;
                                Logger.Error(validationError.Message);
                                response.Errors.Add(new Error(validationError.Message));
                            }
                            else
                            {
                                var stepGatherInitialStatistics = new ProcessStep(cnArchConfig, processID, $@"Gather initial statistics for {tailTableConfig.ServerQualifiedTargetName}");
                                try
                                {
                                    tailTableConfig.InitialRowCount = TableManager.GetRowCount(cnDestination, tailTableConfig);
                                    tailTableConfig.RowsToDelete = TableManager.GetRowCount(cnDestination, tailTableConfig, tailTableConfig.WhereClause);
                                    response.RowsToDelete = tailTableConfig.RowsToDelete;
                                    if (tailTableConfig.RowsToDelete < tailTableConfig.MaxDeleteRowsPerBatch) { tailTableConfig.FinalRowCountCalculated = tailTableConfig.InitialRowCount - tailTableConfig.RowsToDelete; }
                                    else { tailTableConfig.FinalRowCountCalculated = tailTableConfig.InitialRowCount - tailTableConfig.MaxDeleteRowsPerBatch; }
                                    tailTableConfig.IsKeyColumnUnique = TableManager.IsKeyColumnUnique(cnDestination, tailTableConfig);
                                    tailTableConfig.Guid = guid;
                                    tailTableConfig.LastRunStatus = "";
                                    ArchieLoggingManager.LogProcessUpdateInitialRowCount(cnArchConfig, processID, tailTableConfig);
                                    TailTableNoVerifyConfigManager.UpdateTailTableConfigInitialRowCount(cnArchConfig, tailTableConfig);
                                }
                                catch (Exception e)
                                {
                                    stepGatherInitialStatistics.Errors.Add(new Error(e));
                                }
                                stepGatherInitialStatistics.Done(cnArchConfig);
                                response.ProcessSteps.Add(stepGatherInitialStatistics);
                                if (!stepGatherInitialStatistics.Errors.Any() && tailTableConfig.RowsToDelete > 0)
                                {
                                    var stepDropTempTable = new ProcessStep(cnArchConfig, processID, $@"Drop temp table #TempKeyList");
                                    try
                                    {
                                        var cmdDropTemp1 = new SqlCommand
                                        {
                                            Connection = cnArchConfig,
                                            CommandType = CommandType.Text,
                                            CommandText = $@"DROP TABLE IF EXISTS #TempKeyList"
                                        };
                                        Logger.Info(Logging.DbCmdString(cmdDropTemp1));
                                        stepDropTempTable.Statement = cmdDropTemp1.CommandText;
                                        stepDropTempTable.RowsAffected = cmdDropTemp1.ExecuteNonQuery();
                                    }
                                    catch (Exception e)
                                    {
                                        stepDropTempTable.Errors.Add(new Error(e));
                                    }
                                    stepDropTempTable.Done(cnArchConfig);
                                    response.ProcessSteps.Add(stepDropTempTable);

                                    if (!stepDropTempTable.Errors.Any())
                                    {
                                        // Load the Key Values into the temp table.
                                        var stepCreateTempTable = new ProcessStep(cnArchConfig, processID, $@"Create temp table #TempKeyList with oldest {tailTableConfig.MaxDeleteRowsPerBatch} rows from {tailTableConfig.ServerQualifiedTargetName}");
                                        try
                                        {
                                            var cmdTemp = new SqlCommand
                                            {
                                                Connection = cnDestination,
                                                CommandType = CommandType.Text,
                                                CommandText = $@"
                                                        SELECT TOP ({tailTableConfig.MaxDeleteRowsPerBatch}) [{tailTableConfig.KeyColumn}]
                                                        INTO #TempKeyList
                                                        FROM [{tailTableConfig.TargetSchemaName}].[{tailTableConfig.TargetTableName}] WITH (NOLOCK)
                                                            {tailTableConfig.WhereClause} 
                                                        ORDER BY [{tailTableConfig.KeyColumn}]
                                                "
                                            };
                                            Logger.Info(Logging.DbCmdString(cmdTemp));
                                            stepCreateTempTable.Statement = cmdTemp.CommandText;
                                            tailTableConfig.SelectedRows = cmdTemp.ExecuteNonQuery();
                                            Logger.Info($@"ROWS: {tailTableConfig.SelectedRows}");
                                            stepCreateTempTable.RowsAffected = tailTableConfig.SelectedRows;
                                        }
                                        catch (Exception e)
                                        {
                                            stepCreateTempTable.Errors.Add(new Error(e));
                                        }
                                        stepCreateTempTable.Done(cnArchConfig);
                                        response.ProcessSteps.Add(stepCreateTempTable);

                                        if (!stepCreateTempTable.Errors.Any())
                                        {
                                            var stepValidateTempTable = new ProcessStep(cnArchConfig, processID, $@"Validate {tailTableConfig.SelectedRows} rows in #TempKeyList");
                                            try
                                            {

                                                // Verify that #Temp table has expected row count
                                                var cmdVerifyTemp = new SqlCommand
                                                {
                                                    Connection = cnDestination,
                                                    CommandType = CommandType.Text,
                                                    CommandText = $@"SELECT Count(*) FROM #TempKeyList"
                                                };
                                                Logger.Info(Logging.DbCmdString(cmdVerifyTemp));
                                                stepValidateTempTable.Statement = cmdVerifyTemp.CommandText;
                                                tailTableConfig.TempRows = Convert.ToInt32(cmdVerifyTemp.ExecuteScalar());
                                                Logger.Info($@"ROWS: {tailTableConfig.TempRows}");
                                                stepValidateTempTable.RowsAffected = tailTableConfig.TempRows;
                                            }
                                            catch (Exception e)
                                            {
                                                stepCreateTempTable.Errors.Add(new Error(e));
                                            }
                                            stepValidateTempTable.Done(cnArchConfig);
                                            response.ProcessSteps.Add(stepValidateTempTable);

                                            if (!stepValidateTempTable.Errors.Any())
                                            {
                                                if (!tailTableConfig.TestModeNoDelete)
                                                {
                                                    var stepDeleteSourceData = new ProcessStep(cnArchConfig, processID, $@"Delete {tailTableConfig.TempRows} rows from {tailTableConfig.ServerQualifiedTargetName}");
                                                    var cmdDelete = new SqlCommand
                                                    {
                                                        Connection = cnDestination,
                                                        CommandType = CommandType.Text,
                                                        CommandTimeout = 0,
                                                        CommandText = $@"
                                                            DELETE t FROM [{tailTableConfig.TargetSchemaName}].[{tailTableConfig.TargetTableName}] t
                                                                INNER JOIN #TempKeyList k 
                                                                    ON k.[{tailTableConfig.KeyColumn}] = t.[{tailTableConfig.KeyColumn}] 
                                                        "
                                                    };
                                                    Logger.Info(Logging.DbCmdString(cmdDelete));
                                                    stepDeleteSourceData.Statement = cmdDelete.CommandText;
                                                    tailTableConfig.DeleteCommand = cmdDelete.CommandText;
                                                    tailTableConfig.DeletedRows = cmdDelete.ExecuteNonQuery();
                                                    stepDeleteSourceData.RowsAffected = tailTableConfig.DeletedRows;
                                                    stepDeleteSourceData.Done(cnArchConfig);
                                                    response.ProcessSteps.Add(stepDeleteSourceData);
                                                    Logger.Info($@"Deleted ROWS: {tailTableConfig.DeletedRows}");

                                                    tailTableConfig.DeleteSeconds = stepDeleteSourceData.ElapsedSeconds;
                                                    ArchieLoggingManager.LogProcessUpdateDeleteSeconds(cnArchConfig, processID, tailTableConfig);
                                                };
                                                //Logger.Info("SLEEP 2 SECONDS...");
                                                //Thread.Sleep(2000);

                                                //if (tailTableConfig.MovedRows < minMovedRows)
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
                                                //            WHERE [ConfigID] = {tailTableConfig.ID}
                                                //        "
                                                //    };
                                                //    Logger.Info(Logging.DbCmdString(cmdUpdateConfigNextRunDate));
                                                //    stepUpdateConfigNextRunDate.Statement = cmdUpdateConfigNextRunDate.CommandText;
                                                //    stepUpdateConfigNextRunDate.RowsAffected = cmdUpdateConfigNextRunDate.ExecuteNonQuery();
                                                //    stepUpdateConfigNextRunDate.Done(cnArchRepo);
                                                //    response.ProcessSteps.Add(stepUpdateConfigNextRunDate);
                                                //    Logger.Info($@"ROWS: {stepUpdateConfigNextRunDate.RowsAffected}");
                                                //}

                                                if (tailTableConfig.DeleteSeconds > maxDeleteSeconds && tailTableConfig.MaxDeleteRowsPerBatch > 1000)
                                                {
                                                    var stepUpdateConfigMaxDeleteRows = new ProcessStep(cnArchConfig, processID, $@"Reduce Max Delete Rows by 10%");
                                                    var cmdUpdateConfigMaxDeleteRows = new SqlCommand
                                                    {
                                                        Connection = cnArchConfig,
                                                        CommandType = CommandType.Text,
                                                        CommandText = $@"
                                                                UPDATE xx
                                                                SET 
                                                                    [MaxDeleteRowsPerBatch] = [MaxDeleteRowsPerBatch] * .9
                                                                FROM [dbo].[cfgTablesTail] xx
                                                                WHERE [ConfigID] = {tailTableConfig.ID}
                                                            "
                                                    };
                                                    Logger.Info(Logging.DbCmdString(cmdUpdateConfigMaxDeleteRows));
                                                    stepUpdateConfigMaxDeleteRows.Statement = cmdUpdateConfigMaxDeleteRows.CommandText;
                                                    stepUpdateConfigMaxDeleteRows.RowsAffected = cmdUpdateConfigMaxDeleteRows.ExecuteNonQuery();
                                                    stepUpdateConfigMaxDeleteRows.Done(cnArchConfig);
                                                    response.ProcessSteps.Add(stepUpdateConfigMaxDeleteRows);
                                                    Logger.Info($@"ROWS: {stepUpdateConfigMaxDeleteRows.RowsAffected}");
                                                }
                                            }
                                        }
                                    }
                                }

                                if (tailTableConfig.DeletedRows < minMovedRows)
                                {
                                    var stepUpdateConfigNextRunDate = new ProcessStep(cnArchConfig, processID, $@"Move Next Run Date ahead 7 days");
                                    var cmdUpdateConfigNextRunDate = new SqlCommand
                                    {
                                        Connection = cnArchConfig,
                                        CommandType = CommandType.Text,
                                        CommandText = $@"
                                                UPDATE xx
                                                SET 
                                                    [NextRunDate] = GETDATE() + {nextRuntimeMoveAheadDays}
                                                FROM [dbo].[cfgTablesTail] xx
                                                WHERE [ConfigID] = {tailTableConfig.ID}
                                            "
                                    };
                                    Logger.Info(Logging.DbCmdString(cmdUpdateConfigNextRunDate));
                                    stepUpdateConfigNextRunDate.Statement = cmdUpdateConfigNextRunDate.CommandText;
                                    stepUpdateConfigNextRunDate.RowsAffected = cmdUpdateConfigNextRunDate.ExecuteNonQuery();
                                    stepUpdateConfigNextRunDate.Done(cnArchConfig);
                                    response.ProcessSteps.Add(stepUpdateConfigNextRunDate);
                                    Logger.Info($@"ROWS: {stepUpdateConfigNextRunDate.RowsAffected}");
                                }

                                var stepGatherFinalStatistics = new ProcessStep(cnArchConfig, processID, $@"Gather final statistics for {tailTableConfig.ServerQualifiedTargetName}");
                                try
                                {
                                    tailTableConfig.SFMinDate = TableManager.GetMinDate(cnDestination, tailTableConfig);
                                    response.SFMinDate = tailTableConfig.SFMinDate;
                                    tailTableConfig.SFMaxDate = TableManager.GetMaxDate(cnDestination, tailTableConfig);
                                    tailTableConfig.SFDays = TableManager.GetSFDays(cnDestination, tailTableConfig);
                                    response.SFDays = tailTableConfig.SFDays;
                                }
                                catch (Exception e)
                                {
                                    stepGatherFinalStatistics.Errors.Add(new Error(e));
                                }
                                stepGatherFinalStatistics.Done(cnArchConfig);
                                response.ProcessSteps.Add(stepGatherFinalStatistics);

                                response.RowsAffected = tailTableConfig.DeletedRows;
                                tailTableConfig.LastRunStatus = "Success";
                                tailTableConfig.LastTailEndDate = DateTime.Now;
                                tailTableConfig.FinalRowCountActual = TableManager.GetRowCount(cnDestination, tailTableConfig);

                                //ArchieLoggingManager.LogActivity(cnArchConfig, new ArchieLog("Event", "ArchiveTable",
                                //    processName, tailTableConfig.SelectCommand.Trim(), tailTableConfig.DateColumn, tailTableConfig.RetentionDays,
                                //    tailTableConfig.InitialRowCount, tailTableConfig.DeletedRows, tailTableConfig.FinalRowCountActual,
                                //    Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]),
                                //    "", "", tailTableConfig.ServerName, tailTableConfig.DatabaseName, tailTableConfig.SchemaName,
                                //    tailTableConfig.TableName, tailTableConfig.Guid));

                                response.InitialRowCount = tailTableConfig.InitialRowCount;
                                response.FinalRowCount = tailTableConfig.FinalRowCountActual;
                                response.RetentionDays = tailTableConfig.RetentionDays;
                                response.SelectCommand = tailTableConfig.SelectCommand.Trim();
                                response.DeleteCommand = tailTableConfig.DeleteCommand.Trim();

                                //ArchieLoggingManager.LogProcessUpdateDone(cnArchConfig, processID, tailTableConfig);

                                //var stepUpdatetailTableConfigDone = new ProcessStep(cnArchConfig, processID, $@"Set [Archiver].[dbo].[cfgTablesTail] DONE for {tailTableConfig.ServerQualifiedName}");
                                //try
                                //{
                                //    var statement = "";
                                //    stepUpdatetailTableConfigDone.RowsAffected = TailConfigTableManager.UpdatetailTableConfigDone(cnArchConfig, tailTableConfig, out statement);
                                //    stepUpdatetailTableConfigDone.Statement = statement;
                                //}
                                //catch (Exception e)
                                //{
                                //    stepUpdatetailTableConfigDone.Errors.Add(new Error(e));
                                //}
                                //stepUpdatetailTableConfigDone.Done(cnArchConfig);
                            }
                        }

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
                ArchieLoggingManager.LogProcessUpdateDone(cnArchConfig, processID, tailTableConfig);

                var stepUpdatetailTableConfigDone = new ProcessStep(cnArchConfig, processID, $@"Set [Archiver].[dbo].[cfgTablesTail] DONE for {tailTableConfig.ServerQualifiedTargetName}");
                try
                {
                    var statement = "";
                    stepUpdatetailTableConfigDone.RowsAffected = TailTableNoVerifyConfigManager.UpdateTailTableConfigDone(cnArchConfig, tailTableConfig, out statement);
                    stepUpdatetailTableConfigDone.Statement = statement;
                }
                catch (Exception e)
                {
                    stepUpdatetailTableConfigDone.Errors.Add(new Error(e));
                }
                stepUpdatetailTableConfigDone.Done(cnArchConfig);

                cnDestination.Close();
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return response;
        }

        //private static SqlCommand GetTailTableConfig(List<string> filters)
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
